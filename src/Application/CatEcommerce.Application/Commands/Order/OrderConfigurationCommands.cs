using Platform.Infrastructure.Core.Commands;

namespace CatEcommerce.Application.Commands.Order;

/// <summary>A covered district of a delivery zone; no postal codes means the whole district.</summary>
public sealed record CoverageInput(string District, IReadOnlyList<string>? PostalCodes);

public sealed record WeightBandInput(int? UpToGrams, decimal Surcharge);

/// <summary>A slot window: <c>DayOfWeek</c> is a day name, <c>Start</c> and <c>End</c> are branch-local <c>HH:mm</c>.</summary>
public sealed record SlotWindowInput(string DayOfWeek, string Start, string End);

/// <summary>One service level (<c>Standard</c> or <c>Express</c>) of a zone.</summary>
public sealed record ZoneServiceInput(
    string ServiceLevel, bool IsActive, decimal BaseCharge, decimal? FreeOverOrderValue, int LeadTimeDays,
    IReadOnlyList<WeightBandInput>? WeightBands, IReadOnlyList<SlotWindowInput>? SlotWindows);

/// <summary>HO-ORD-001 §11.1 C1 -- create a delivery zone (US-ORD-06). Ids are minted in the controller; the caller is <c>UserContext</c>.</summary>
public sealed class CreateDeliveryZoneCommand : Command
{
    public required Guid ZoneId { get; init; }
    public required Guid BranchId { get; init; }
    public required string Name { get; init; }
    public int Priority { get; init; }
    public bool IsActive { get; init; } = true;
    public bool CodEnabled { get; init; }
    public decimal? CodMaxOrderValue { get; init; }
    public required IReadOnlyList<CoverageInput> Coverage { get; init; }
    public required IReadOnlyList<ZoneServiceInput> Services { get; init; }
}

/// <summary>C1 -- edit a zone in place (audited; the order snapshots what it was priced with, ADR-206).</summary>
public sealed class UpdateDeliveryZoneCommand : Command
{
    public required Guid ZoneId { get; init; }
    public required Guid BranchId { get; init; }
    public required string Name { get; init; }
    public int Priority { get; init; }
    public bool IsActive { get; init; } = true;
    public bool CodEnabled { get; init; }
    public decimal? CodMaxOrderValue { get; init; }
    public required IReadOnlyList<CoverageInput> Coverage { get; init; }
    public required IReadOnlyList<ZoneServiceInput> Services { get; init; }
}

public sealed class SetDeliveryZoneActivationCommand : Command
{
    public required Guid ZoneId { get; init; }
    public required bool IsActive { get; init; }
}

/// <summary>C2 -- create a courier (US-ORD-11). <c>TrackingUrlTemplate</c> is https and contains <c>{tracking}</c> once.</summary>
public sealed class CreateCourierCommand : Command
{
    public required Guid CourierId { get; init; }
    public required string Name { get; init; }
    public string? TrackingUrlTemplate { get; init; }
    public bool SupportsCod { get; init; }
    public bool IsActive { get; init; } = true;
}

public sealed class UpdateCourierCommand : Command
{
    public required Guid CourierId { get; init; }
    public required string Name { get; init; }
    public string? TrackingUrlTemplate { get; init; }
    public bool SupportsCod { get; init; }
    public bool IsActive { get; init; } = true;
}

/// <summary>C3 -- set the shop's one bank-transfer account (data, never configuration -- §20.8).</summary>
public sealed class SetBankTransferAccountCommand : Command
{
    public required string AccountName { get; init; }
    public required string AccountNumber { get; init; }
    public required string BankName { get; init; }
    public required string BranchName { get; init; }
    public string? RoutingNumber { get; init; }
    public bool IsActive { get; init; } = true;
}

/// <summary>C4 -- create a discount code (US-ORD-05). <c>Kind</c> is <c>Percentage</c> or <c>FixedAmount</c>; <c>Scope</c> is <c>Order</c>,
/// <c>Products</c> or <c>Categories</c>; <c>CategoryIds</c> are <c>ProductCategory</c> values.</summary>
public sealed class CreateDiscountCodeCommand : Command
{
    public required Guid DiscountCodeId { get; init; }
    public required string Code { get; init; }
    public string? Description { get; init; }
    public required string Kind { get; init; }
    public decimal? Percent { get; init; }
    public decimal? Amount { get; init; }
    public decimal? MinimumSpend { get; init; }
    public required string Scope { get; init; }
    public IReadOnlyList<Guid> ProductIds { get; init; } = [];
    public IReadOnlyList<int> CategoryIds { get; init; } = [];
    public bool FirstOrderOnly { get; init; }
    public int? UsageLimit { get; init; }
    public int? PerCustomerLimit { get; init; }
    public DateTimeOffset? ValidFrom { get; init; }
    public DateTimeOffset? ValidTo { get; init; }
    public bool IsActive { get; init; } = true;
}

/// <summary>C4 -- edit everything but the code's text.</summary>
public sealed class UpdateDiscountCodeCommand : Command
{
    public required Guid DiscountCodeId { get; init; }
    public string? Description { get; init; }
    public required string Kind { get; init; }
    public decimal? Percent { get; init; }
    public decimal? Amount { get; init; }
    public decimal? MinimumSpend { get; init; }
    public required string Scope { get; init; }
    public IReadOnlyList<Guid> ProductIds { get; init; } = [];
    public IReadOnlyList<int> CategoryIds { get; init; } = [];
    public bool FirstOrderOnly { get; init; }
    public int? UsageLimit { get; init; }
    public int? PerCustomerLimit { get; init; }
    public DateTimeOffset? ValidFrom { get; init; }
    public DateTimeOffset? ValidTo { get; init; }
    public bool IsActive { get; init; } = true;
}

public sealed class SetDiscountCodeActivationCommand : Command
{
    public required Guid DiscountCodeId { get; init; }
    public required bool IsActive { get; init; }
}
