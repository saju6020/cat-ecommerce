using CatEcommerce.Domain.Aggregates.Administration;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Shared.Common.Constants;
using Platform.Infrastructure.Core.Domain;

namespace CatEcommerce.Infrastructure.Notifications;

/// <summary>HO-NTF-001 §8.14 -- loads <c>notifications.quietHours</c> for the notification's branch
/// (else the main branch, §8.2) and that branch's IANA zone, then calls the pure <c>QuietHours</c>.</summary>
public sealed class QuietHoursCalendar(IBusinessSettings businessSettings, IAggregateRootRepository<BranchAggregate> branchRepository)
    : IQuietHoursCalendar
{
    public async Task<DateTimeOffset?> NextAllowedInstantAsync(DateTimeOffset now, Guid? branchId, CancellationToken ct = default)
    {
        var effectiveBranchId = branchId ?? AdministrationSeed.MainBranchId;

        var quiet = businessSettings.GetTimeRange(SettingKeys.NotificationsQuietHours, effectiveBranchId);

        var branch = await branchRepository.GetByIdAsync(effectiveBranchId);
        var timeZoneId = branch?.TimeZoneId ?? "Asia/Dhaka";
        var zone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);

        return CatEcommerce.Domain.DomainServices.QuietHours.NextAllowedInstant(now, quiet, zone);
    }
}
