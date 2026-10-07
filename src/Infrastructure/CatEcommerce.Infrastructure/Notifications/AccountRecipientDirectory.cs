using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Models;
using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Shared.Common.Enums;
using CatEcommerce.Shared.Common.Helpers;
using Microsoft.EntityFrameworkCore;

namespace CatEcommerce.Infrastructure.Notifications;

/// <summary>HO-NTF-001 §8.5 -- read-only over <c>account.*</c>. A merged customer resolves to the
/// survivor, followed at most 5 hops; an anonymised customer or an inactive staff account
/// resolves with <c>IsActive = false</c> (staff have no deactivation flag in this codebase yet, so
/// every existing staff account resolves active).</summary>
public sealed class AccountRecipientDirectory(CatEcommerceDbContext db) : INotificationRecipientDirectory
{
    private const int MaxMergeHops = 5;

    /// <summary>Gate fix 2026-09-27 (SECURITY/ARCHITECTURE/CODE_REVIEW HIGH). Breadth-first reverse
    /// walk of <c>MergedIntoCustomerId</c>, bounded the same way <see cref="ResolveCustomerAsync"/>
    /// bounds its forward walk.</summary>
    public async Task<IReadOnlyList<Guid>> FindMergedDuplicatesAsync(Guid survivorId, CancellationToken ct = default)
    {
        var found = new List<Guid>();
        var frontier = new List<Guid> { survivorId };

        for (var hop = 0; hop < MaxMergeHops && frontier.Count > 0; hop++)
        {
            var duplicates = await db.CustomerAccounts.AsNoTracking()
                .Where(c => c.MergedIntoCustomerId != null && frontier.Contains(c.MergedIntoCustomerId.Value))
                .Select(c => c.Id)
                .ToListAsync(ct);

            if (duplicates.Count == 0) break;

            found.AddRange(duplicates);
            frontier = duplicates;
        }

        return found;
    }

    public async Task<RecipientProfile?> ResolveAsync(NotificationRecipientKind kind, Guid id, CancellationToken ct = default)
    {
        return kind == NotificationRecipientKind.Customer
            ? await ResolveCustomerAsync(id, ct)
            : await ResolveStaffAsync(id, ct);
    }

    private async Task<RecipientProfile?> ResolveCustomerAsync(Guid id, CancellationToken ct)
    {
        var currentId = id;
        CustomerRow? current = null;

        for (var hop = 0; hop <= MaxMergeHops; hop++)
        {
            var row = await db.CustomerAccounts.AsNoTracking()
                .Where(c => c.Id == currentId)
                .Select(c => new CustomerRow(
                    c.Id, c.FullName,
                    c.MobileNumber == null ? null : c.MobileNumber.Value,
                    c.EmailAddress == null ? null : c.EmailAddress.Value,
                    c.Status, c.MergedIntoCustomerId))
                .FirstOrDefaultAsync(ct);

            if (row is null) return current is null ? null : ToProfile(current, isActive: false);

            current = row;
            if (row.MergedIntoCustomerId is null) break;
            currentId = row.MergedIntoCustomerId.Value;
        }

        if (current is null) return null;

        var isActive = current.Status == CustomerAccountStatus.Active;
        return await BuildCustomerProfileAsync(current, isActive, ct);
    }

    private async Task<RecipientProfile> BuildCustomerProfileAsync(CustomerRow current, bool isActive, CancellationToken ct)
    {
        var preferences = await db.CustomerNotificationPreferences.AsNoTracking()
            .Where(p => p.Id == current.Id)
            .Select(p => new
            {
                p.PreferredChannel,
                Consents = p.MarketingConsents.Select(c => new { c.Channel, c.IsGranted }).ToList()
            })
            .FirstOrDefaultAsync(ct);

        var effectivePreferred = preferences?.PreferredChannel
            ?? (current.MobileNumber is not null ? NotificationChannel.Sms : NotificationChannel.Email);

        bool MarketingGranted(NotificationChannel channel)
        {
            var granted = preferences?.Consents.FirstOrDefault(c => c.Channel == channel)?.IsGranted
                ?? NotificationPreferenceRules.DefaultMarketingGranted;
            return NotificationPreferenceRules.Permits(channel, NotificationCategory.Marketing, granted);
        }

        return new RecipientProfile(
            NotificationRecipientKind.Customer, current.Id, isActive, current.FullName,
            current.MobileNumber, current.EmailAddress, effectivePreferred, MarketingGranted);
    }

    private static RecipientProfile ToProfile(CustomerRow row, bool isActive) =>
        new(NotificationRecipientKind.Customer, row.Id, isActive, row.FullName, row.MobileNumber, row.EmailAddress,
            row.MobileNumber is not null ? NotificationChannel.Sms : NotificationChannel.Email, _ => false);

    private async Task<RecipientProfile?> ResolveStaffAsync(Guid id, CancellationToken ct)
    {
        var staff = await db.StaffAccounts.AsNoTracking()
            .Where(s => s.Id == id)
            .Select(s => new
            {
                s.Id, s.FullName,
                Mobile = s.MobileNumber == null ? null : s.MobileNumber.Value,
                Email = s.EmailAddress == null ? null : s.EmailAddress.Value
            })
            .FirstOrDefaultAsync(ct);

        if (staff is null) return null;

        var effectivePreferred = staff.Email is not null ? NotificationChannel.Email : NotificationChannel.Sms;

        // Staff have no marketing consent model -- transactional-only use in this slice.
        return new RecipientProfile(NotificationRecipientKind.Staff, staff.Id, true, staff.FullName, staff.Mobile, staff.Email, effectivePreferred, _ => true);
    }

    private sealed record CustomerRow(Guid Id, string FullName, string? MobileNumber, string? EmailAddress, CustomerAccountStatus Status, Guid? MergedIntoCustomerId);
}
