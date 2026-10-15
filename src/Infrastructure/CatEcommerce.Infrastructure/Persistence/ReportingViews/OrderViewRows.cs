namespace CatEcommerce.Infrastructure.Persistence.ReportingViews;

public sealed class RvOrderRow
{
    public Guid OrderId { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public Guid BranchId { get; set; }
    public Guid? CustomerId { get; set; }
    public int Channel { get; set; }
    public int Status { get; set; }
    public int PaymentMethod { get; set; }
    public DateTimeOffset PlacedOn { get; set; }
    public DateTimeOffset? ConfirmedOn { get; set; }
    public DateTimeOffset? PackedOn { get; set; }
    public DateTimeOffset? DispatchedOn { get; set; }
    public DateTimeOffset? DeliveredOn { get; set; }
    public DateTimeOffset? CancelledOn { get; set; }
    public decimal GrandTotal { get; set; }
    public string CurrencyCode { get; set; } = string.Empty;
}

public sealed class RvOrderReturnRow
{
    public Guid ReturnId { get; set; }
    public Guid OrderId { get; set; }
    public Guid BranchId { get; set; }
    public int Status { get; set; }
    public DateTimeOffset RequestedOn { get; set; }
    public DateTimeOffset? ReceivedOn { get; set; }
}
