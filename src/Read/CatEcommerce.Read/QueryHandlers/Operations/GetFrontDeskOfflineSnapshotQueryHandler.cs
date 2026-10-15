using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Read.Queries.Operations;
using CatEcommerce.Read.ViewModels;
using CatEcommerce.Shared.Common.Constants;
using Microsoft.EntityFrameworkCore;
using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Read.QueryHandlers.Operations;

/// <summary>
/// HO-XCT-001 section 12.4/12.6 Q1, ADR-306 -- composes the offline snapshot from every registered section source. For each known section key: the caller
/// may not have its action (<c>NotPermitted</c>); no source is registered (<c>NotAvailable</c>, as for <c>boardingTasks</c> while Boarding is not built);
/// otherwise the source builds it. The ETag is the strong SHA-256 of the canonical content and excludes <c>generatedOn</c>, so an unchanged day gives 304.
/// </summary>
public sealed class GetFrontDeskOfflineSnapshotQueryHandler(
    CatEcommerceDbContext db,
    IEnumerable<IFrontDeskSnapshotSource> sources,
    IBusinessSettings settings,
    ISessionPolicy sessionPolicy,
    TimeProvider timeProvider)
    : IQueryHandlerAsync<GetFrontDeskOfflineSnapshotQuery, FrontDeskSnapshotViewModel?>
{
    /// <summary>Every section the schema can carry, in output order. A key with no registered source is reported <c>NotAvailable</c>.</summary>
    public static readonly IReadOnlyList<string> KnownSections = [ClinicDayListSnapshotSource.Key, "boardingTasks"];

    private static readonly JsonSerializerOptions CanonicalJson = new(JsonSerializerDefaults.Web);

    public async Task<QueryResponse<FrontDeskSnapshotViewModel?>> HandleAsync(GetFrontDeskOfflineSnapshotQuery query)
    {
        var branch = await db.Branches.AsNoTracking().Where(b => b.Id == query.BranchId)
            .Select(b => new SnapshotBranch(b.Id, b.Name, b.TimeZoneId)).FirstOrDefaultAsync();
        if (branch is null) return new QueryResponse<FrontDeskSnapshotViewModel?> { Result = null };

        var zone = TimeZoneInfo.FindSystemTimeZoneById(branch.TimeZoneId);
        var generatedOn = timeProvider.GetUtcNow();
        var localDate = BranchCalendar.LocalDateOf(generatedOn, branch.TimeZoneId);

        var sections = new SortedDictionary<string, object>(StringComparer.Ordinal);
        var counts = new SortedDictionary<string, int>(StringComparer.Ordinal);
        var omitted = new List<SnapshotOmittedSection>();

        foreach (var key in KnownSections)
        {
            var source = sources.FirstOrDefault(s => s.SectionKey == key);
            if (source is null) { omitted.Add(new SnapshotOmittedSection(key, "NotAvailable")); continue; }
            if (!IsAllowed(source.RequiredAction, query.CallerRoles)) { omitted.Add(new SnapshotOmittedSection(key, "NotPermitted")); continue; }

            var built = await source.BuildAsync(query.BranchId, localDate, zone, CancellationToken.None);
            if (built is null) { omitted.Add(new SnapshotOmittedSection(key, "NotAvailable")); continue; }

            sections[key] = built;
            counts[key] = built is SnapshotAppointmentsSection appointments ? appointments.Items.Count : 0;
        }

        var etag = ComputeETag(branch, localDate, sections, omitted);
        var validUntil = new DateTimeOffset(
            TimeZoneInfo.ConvertTimeToUtc(localDate.AddDays(1).ToDateTime(TimeOnly.MinValue), zone), TimeSpan.Zero).AddHours(OfflineSnapshotRules.ValidityGraceHours);

        var body = new FrontDeskSnapshotBody(
            OfflineSnapshotRules.SchemaVersion, branch, localDate.ToString("yyyy-MM-dd"),
            TimeZoneInfo.ConvertTime(generatedOn, zone), TimeZoneInfo.ConvertTime(validUntil, zone), query.StaffAccountId,
            (int)sessionPolicy.StaffIdleTimeout.TotalMinutes, settings.GetInt(SettingKeys.OfflineSnapshotRefreshSeconds, query.BranchId),
            sections, omitted);

        return new QueryResponse<FrontDeskSnapshotViewModel?> { Result = new FrontDeskSnapshotViewModel(etag, localDate, counts, body) };
    }

    private static bool IsAllowed(string? action, IReadOnlyList<string> callerRoles) =>
        action is null || (ActionRoles.TryGetRoles(action, out var roles) && roles.Intersect(callerRoles, StringComparer.Ordinal).Any());

    /// <summary>Strong ETag: quoted lower-case hex SHA-256 of the canonical JSON of branch, local date, sections and omitted sections. Public for the unit tests.</summary>
    public static string ComputeETag(
        SnapshotBranch branch, DateOnly localDate, IReadOnlyDictionary<string, object> sections, IReadOnlyList<SnapshotOmittedSection> omitted)
    {
        var canonical = JsonSerializer.Serialize(
            new { branch, localDate = localDate.ToString("yyyy-MM-dd"), sections, omittedSections = omitted }, CanonicalJson);
        return "\"" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant() + "\"";
    }
}
