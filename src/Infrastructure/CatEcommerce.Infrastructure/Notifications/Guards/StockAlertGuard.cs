using CatEcommerce.Domain.Aggregates.Product;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Core.Domain;

namespace CatEcommerce.Infrastructure.Notifications.Guards;

/// <summary>
/// HO-SHP-001 section 8.11 -- valid while the alert (<paramref name="subjectId"/>) is
/// <see cref="StockAlertStatus.Notified"/>, its SKU is <c>Active</c> and
/// its product is <c>Active</c> and listed. Stock is deliberately NOT re-checked at send time: a
/// quiet-hours hold may outlast a brief sell-out, and "it came back" is still true. Reads only.
/// </summary>
public sealed class StockAlertGuard(
    IAggregateRootRepository<StockAlertAggregate> alertRepository,
    IAggregateRootRepository<ProductAggregate> productRepository) : INotificationSubjectGuard
{
    public NotificationSubjectKind Kind => NotificationSubjectKind.StockAlert;

    public async Task<bool> IsStillValidAsync(Guid recipientId, Guid? subjectId, Guid? subjectRef, DateOnly? subjectDueOn, string? subjectStamp = null, CancellationToken ct = default)
    {
        if (subjectId is null) return false;

        var alert = await alertRepository.GetByIdAsync(subjectId.Value);
        // No owner check (section 8.11 names none): a merge between enqueue and delivery moves the
        // alert to the survivor while the request keeps the pre-merge recipient as history.
        if (alert is null || alert.Status != StockAlertStatus.Notified) return false;

        var product = await productRepository.GetByFilterAsync(p => p.Skus.Any(s => s.Id == alert.SkuId));
        if (product is null || product.Status != ProductStatus.Active || !product.ListedOnStorefront) return false;

        return product.Skus.Any(s => s.Id == alert.SkuId && s.Status == SkuStatus.Active);
    }
}
