using CatEcommerce.Domain.Aggregates.Account;
using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Read.Queries.Account;
using CatEcommerce.Read.ViewModels;
using CatEcommerce.Shared.Common.Enums;
using CatEcommerce.Shared.Common.Helpers;
using Microsoft.EntityFrameworkCore;
using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Read.QueryHandlers.Account;

/// <summary>
/// Q-I1 / §8.9's match rule, applied as connected components over "shares a contact number or
/// email" among active customers. The match rule is a graph problem (two records can each match a
/// third without matching each other directly), which is not expressible as a single SQL
/// projection, so the union-find itself still runs in memory -- but only over the candidates
/// <see cref="FindCandidateCustomerIdsAsync"/> returns, not every active customer. That method now
/// runs the actual candidacy computation as SQL <c>GROUP BY ... HAVING COUNT(*) &gt; 1</c> (via EF
/// Core's translated <c>GroupBy</c>/<c>Where(g =&gt; g.Count() &gt; 1)</c>) to find which
/// identifier VALUES are shared by more than one active customer -- only those (typically few)
/// shared values, not per-customer rows, cross into memory before the final, still-SQL lookup of
/// which customers touch one of them. Grouping and paging the connected components themselves
/// fully in the database would need the graph expressed as a recursive query -- left as a named
/// follow-up if the duplicates report becomes slow in practice, not claimed as done here.
/// </summary>
public sealed class GetDuplicateCustomerGroupsQueryHandler(CatEcommerceDbContext db)
    : IQueryHandlerAsync<GetDuplicateCustomerGroupsQuery, PagedResult<DuplicateCustomerGroupViewModel>>
{
    public async Task<QueryResponse<PagedResult<DuplicateCustomerGroupViewModel>>> HandleAsync(GetDuplicateCustomerGroupsQuery query)
    {
        var candidateIds = await FindCandidateCustomerIdsAsync();

        if (candidateIds.Count == 0)
        {
            return new QueryResponse<PagedResult<DuplicateCustomerGroupViewModel>>
            {
                Result = new PagedResult<DuplicateCustomerGroupViewModel>([], query.PageNumber, query.PageSize, 0)
            };
        }

        var customers = await db.CustomerAccounts.AsNoTracking()
            .Where(c => c.Status == CustomerAccountStatus.Active && candidateIds.Contains(c.Id))
            .Select(c => new
            {
                c.Id, c.FullName,
                MobileNumber = c.MobileNumber == null ? null : c.MobileNumber.Value,
                EmailAddress = c.EmailAddress == null ? null : c.EmailAddress.Value,
                c.AdditionalPhoneNumbers,
                // HO-ACC-002 CR MEDIUM 2 (first-pass gate fix round, item 13) -- same derivation
                // GetStaffCustomerQueryHandler already uses for these two fields.
                HasPassword = c.PasswordCredential != null,
                c.RegistrationChannel
            })
            .ToListAsync();

        // Union-find over "shares a contact number or email" (§8.9's match rule -- two nulls
        // never match, so an email/number key is only ever indexed when present).
        var parent = customers.ToDictionary(c => c.Id, c => c.Id);

        Guid Find(Guid id) => parent[id] == id ? id : (parent[id] = Find(parent[id]));
        void Union(Guid a, Guid b) { var ra = Find(a); var rb = Find(b); if (ra != rb) parent[ra] = rb; }

        var byMobileOrAdditional = new Dictionary<string, List<Guid>>();
        var byEmail = new Dictionary<string, List<Guid>>();

        foreach (var c in customers)
        {
            var numbers = new List<string>();
            if (c.MobileNumber is not null) numbers.Add(c.MobileNumber);
            numbers.AddRange(c.AdditionalPhoneNumbers);

            foreach (var number in numbers.Distinct())
            {
                if (!byMobileOrAdditional.TryGetValue(number, out var list))
                    byMobileOrAdditional[number] = list = [];
                list.Add(c.Id);
            }

            if (c.EmailAddress is not null)
            {
                if (!byEmail.TryGetValue(c.EmailAddress, out var list))
                    byEmail[c.EmailAddress] = list = [];
                list.Add(c.Id);
            }
        }

        foreach (var group in byMobileOrAdditional.Values.Where(g => g.Count > 1))
            for (var i = 1; i < group.Count; i++) Union(group[0], group[i]);

        foreach (var group in byEmail.Values.Where(g => g.Count > 1))
            for (var i = 1; i < group.Count; i++) Union(group[0], group[i]);

        var groupsUnordered = customers
            .GroupBy(c => Find(c.Id))
            .Where(g => g.Count() > 1)
            .ToList();

        var totalCount = groupsUnordered.Count;

        // CODE_REVIEW gate MEDIUM (2026-09-26, integrator): there was no ORDER BY, so with paging
        // now capped at PagingRules.MaxPageSize (100), a group could silently fall off page 1 once
        // more than 100 duplicate groups accumulate in this shared, never-truncated database
        // (§16.1), and the two members of a page could shuffle between two separate page requests.
        // Ordered by each group's newest member's registration time (the earliest DataChange audit
        // row this handler's own database already carries for every CustomerAccountAggregate
        // insert -- no schema change needed) descending, so a freshly created group always sorts
        // ahead of every pre-existing one; a group with no audit row yet (should not happen in
        // practice, since the insert and its audit row commit in the same transaction, ADR-040)
        // sorts last rather than crashing. Ties (including every group predating this fix, whose
        // members share no orderable signal) break on the group's own smallest member id, which is
        // stable across requests regardless of union-find's arbitrary root choice or the database's
        // physical scan order.
        var memberIds = groupsUnordered.SelectMany(g => g.Select(c => c.Id)).ToList();
        var registeredOnByCustomerId = await db.AuditEntries.AsNoTracking()
            .Where(a => a.EntityType == nameof(CustomerAccountAggregate) && memberIds.Contains(a.EntityId))
            .GroupBy(a => a.EntityId)
            .Select(g => new { CustomerId = g.Key, RegisteredOn = g.Min(a => a.OccurredOn) })
            .ToDictionaryAsync(x => x.CustomerId, x => x.RegisteredOn);

        var groups = groupsUnordered
            .OrderByDescending(g => g.Max(c => registeredOnByCustomerId.GetValueOrDefault(c.Id, DateTimeOffset.MinValue)))
            .ThenBy(g => g.Min(c => c.Id))
            .Skip(query.Skip).Take(query.Take)
            .ToList();

        // One batched query for every cat owned by any member of the current page, instead of one
        // query per member (ARCHITECTURE gate, MEDIUM N+1).
        var pageMemberIds = groups.SelectMany(g => g.Select(c => c.Id)).ToList();
        var catsByOwner = (await db.Cats.AsNoTracking()
                .Where(cat => pageMemberIds.Contains(cat.OwnerId))
                .Select(cat => new { cat.OwnerId, cat.Id, cat.Name, IsDeceased = cat.LifeStatus == CatLifeStatus.Deceased })
                .ToListAsync())
            .GroupBy(cat => cat.OwnerId)
            .ToDictionary(g => g.Key, g => g.Select(cat => new DuplicateCustomerCatViewModel(cat.Id, cat.Name, cat.IsDeceased)).ToList());

        var result = groups
            .Select(group => new DuplicateCustomerGroupViewModel(group
                .Select(c => new DuplicateCustomerMemberViewModel(
                    c.Id, c.FullName, c.MobileNumber, c.EmailAddress,
                    catsByOwner.TryGetValue(c.Id, out var cats) ? cats : [],
                    MobileMatchedOn: MobileMatchType(byMobileOrAdditional, c.MobileNumber, c.AdditionalPhoneNumbers),
                    RecordStatus: CustomerRecordStatusRules.Derive(c.HasPassword).DisplayName,
                    RegistrationChannel: c.RegistrationChannel.DisplayName))
                .ToList()))
            .ToList();

        return new QueryResponse<PagedResult<DuplicateCustomerGroupViewModel>>
        {
            Result = new PagedResult<DuplicateCustomerGroupViewModel>(result, query.PageNumber, query.PageSize, totalCount)
        };
    }

    /// <summary>
    /// SECURITY gate LOW (second pass): "Primary" when <paramref name="mobileNumber"/> itself is
    /// one of the numbers shared by more than one active customer (<paramref name="byMobileOrAdditional"/>,
    /// built before grouping); "Additional" when the sharing is only via one of
    /// <paramref name="additionalPhoneNumbers"/>; <c>null</c> when this member's presence in its
    /// group is by email alone. See <see cref="DuplicateCustomerMemberViewModel.MobileMatchedOn"/>.
    /// </summary>
    private static string? MobileMatchType(
        Dictionary<string, List<Guid>> byMobileOrAdditional, string? mobileNumber, IReadOnlyList<string> additionalPhoneNumbers)
    {
        if (mobileNumber is not null && byMobileOrAdditional.TryGetValue(mobileNumber, out var primaryGroup) && primaryGroup.Count > 1)
            return "Primary";

        if (additionalPhoneNumbers.Any(n => byMobileOrAdditional.TryGetValue(n, out var group) && group.Count > 1))
            return "Additional";

        return null;
    }

    /// <summary>
    /// Every active customer id that shares a mobile number, additional phone number or email
    /// with at least one other active customer. Candidacy is computed as
    /// <c>GROUP BY ... HAVING COUNT(*) &gt; 1</c> in the database (EF Core translates
    /// <c>GroupBy(...).Where(g =&gt; g.Count() &gt; 1)</c> to exactly that), over the union of
    /// primary mobile numbers and additional numbers -- the match rule treats them as one set (a
    /// primary number matching another customer's additional number is still a match, §8.9), which
    /// is why they are combined with <c>Concat</c> before grouping rather than grouped separately.
    /// Only the resulting shared VALUES (typically a small set) cross into memory here; the
    /// customers touching one of them are then found with a second, still fully-SQL query. No
    /// per-customer row for the whole active base is loaded by this method.
    /// </summary>
    private async Task<HashSet<Guid>> FindCandidateCustomerIdsAsync()
    {
        var mobileLikeNumbers = db.CustomerAccounts.AsNoTracking()
            .Where(c => c.Status == CustomerAccountStatus.Active && c.MobileNumber != null)
            .Select(c => c.MobileNumber!.Value)
            .Concat(db.CustomerAccounts.AsNoTracking()
                .Where(c => c.Status == CustomerAccountStatus.Active)
                .SelectMany(c => c.AdditionalPhoneNumbers));

        var duplicateNumbers = await mobileLikeNumbers
            .GroupBy(number => number)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToListAsync();

        var duplicateEmails = await db.CustomerAccounts.AsNoTracking()
            .Where(c => c.Status == CustomerAccountStatus.Active && c.EmailAddress != null)
            .Select(c => c.EmailAddress!.Value)
            .GroupBy(email => email)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToListAsync();

        if (duplicateNumbers.Count == 0 && duplicateEmails.Count == 0)
            return [];

        var candidateIds = await db.CustomerAccounts.AsNoTracking()
            .Where(c => c.Status == CustomerAccountStatus.Active &&
                ((c.MobileNumber != null && duplicateNumbers.Contains(c.MobileNumber.Value)) ||
                 c.AdditionalPhoneNumbers.Any(n => duplicateNumbers.Contains(n)) ||
                 (c.EmailAddress != null && duplicateEmails.Contains(c.EmailAddress.Value))))
            .Select(c => c.Id)
            .ToListAsync();

        return candidateIds.ToHashSet();
    }
}
