using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Domain.ValueObjects;

/// <summary>
/// HO-BIL-001 §8.8 -- who physically took the money, required when the payment method's
/// <c>RequiresCollector</c> flag is set (cash on delivery). A staff collector names a staff account; a
/// courier collector carries the courier's code and a name snapshot (OQ-11 -- courier master data is
/// HO-ORD-001's).
/// </summary>
public sealed record PaymentCollector
{
    public CollectorKind Kind { get; init; }
    public Guid? StaffAccountId { get; init; }
    public string? CourierCode { get; init; }
    public string? CourierName { get; init; }

    public PaymentCollector(CollectorKind kind, Guid? staffAccountId, string? courierCode, string? courierName)
    {
        Kind = kind;
        StaffAccountId = staffAccountId;
        CourierCode = courierCode;
        CourierName = courierName;
    }

    private PaymentCollector() => Kind = default!;
}
