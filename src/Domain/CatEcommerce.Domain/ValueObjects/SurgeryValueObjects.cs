using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Domain.ValueObjects;

/// <summary>HO-SRG-001 §8.2 -- the procedure as it was when the surgery was booked (ADR-049: read at booking, never re-read). <c>IsSterilisation</c> is
/// <c>Category</c> in Spay or Neuter.</summary>
public sealed record ProcedureSnapshot
{
    public string ServiceName { get; init; }
    public ServiceCategory Category { get; init; }
    public int DurationMinutes { get; init; }
    public bool IsSterilisation { get; init; }

    public ProcedureSnapshot(string serviceName, ServiceCategory category, int durationMinutes)
    {
        ServiceName = serviceName;
        Category = category;
        DurationMinutes = durationMinutes;
        IsSterilisation = category == ServiceCategory.Spay || category == ServiceCategory.Neuter;
    }

    private ProcedureSnapshot()
    {
        ServiceName = default!;
        Category = default!;
    }
}

/// <summary>HO-SRG-001 §8.2 (ADR-170, ADR-171) -- the price a surgery was booked at and its basis. NOT a payment, deposit or balance: HO-BIL-001 invoices from it.</summary>
public sealed record SurgeryPriceSnapshot
{
    public Money Amount { get; init; }
    public SurgeryPricingBasis Basis { get; init; }
    public Guid? CampaignId { get; init; }
    public string? CampaignEligibilityNote { get; init; }

    public SurgeryPriceSnapshot(Money amount, SurgeryPricingBasis basis, Guid? campaignId, string? campaignEligibilityNote)
    {
        Amount = amount;
        Basis = basis;
        CampaignId = campaignId;
        CampaignEligibilityNote = campaignEligibilityNote;
    }

    private SurgeryPriceSnapshot()
    {
        Amount = default!;
        Basis = default!;
    }
}

/// <summary>HO-SRG-001 §8.7 (BR-5, ADR-161) -- a veterinarian's emergency override of a missing consent: who, when and why, stored on the surgery for good.</summary>
public sealed record ConsentOverride(Guid OverriddenBy, DateTimeOffset OverriddenOn, string Reason);
