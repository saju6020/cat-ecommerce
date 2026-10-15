namespace CatEcommerce.Api.Contracts.Boarding;

// HO-BRD-001 section 12.4 -- stay request bodies. camelCase JSON; money as an amount string; times HH:mm.

public sealed class CreateBoardingEnquiryRequest
{
    public Guid BranchId { get; init; }
    public DateOnly ArrivalOn { get; init; }
    public DateOnly DepartureOn { get; init; }
    public string UnitType { get; init; } = default!;
    public IReadOnlyList<Guid> CatIds { get; init; } = [];
}

/// <summary>Staff books on a customer's behalf: the branch is the active branch.</summary>
public sealed class CreateStaffBoardingEnquiryRequest
{
    public Guid CustomerId { get; init; }
    public DateOnly ArrivalOn { get; init; }
    public DateOnly DepartureOn { get; init; }
    public string UnitType { get; init; } = default!;
    public IReadOnlyList<Guid> CatIds { get; init; } = [];
}

public sealed class ChangeBoardingEnquiryRequest
{
    public DateOnly ArrivalOn { get; init; }
    public DateOnly DepartureOn { get; init; }
    public string UnitType { get; init; } = default!;
    public IReadOnlyList<Guid> CatIds { get; init; } = [];
}

public sealed class FeedingRequest
{
    public string Time { get; init; } = default!;
    public string Portion { get; init; } = default!;
    public string FoodSource { get; init; } = default!;
    public string FoodDescription { get; init; } = default!;
    public Guid? HouseFoodSkuId { get; init; }
}

public sealed class MedicationRequest
{
    public string Name { get; init; } = default!;
    public string Dose { get; init; } = default!;
    public string Route { get; init; } = default!;
    public IReadOnlyList<string> Times { get; init; } = [];
    public string? Instructions { get; init; }
    public DateOnly? FromOn { get; init; }
    public DateOnly? ToOn { get; init; }
}

public sealed class EmergencyContactRequest
{
    public string Name { get; init; } = default!;
    public string Phone { get; init; } = default!;
    public string? Relationship { get; init; }
}

public sealed class CareProfileRequest
{
    public IReadOnlyList<FeedingRequest> Feedings { get; init; } = [];
    public IReadOnlyList<MedicationRequest> Medications { get; init; } = [];
    public string LitterType { get; init; } = default!;
    public string? LitterNote { get; init; }
    public bool VetVisitAuthorised { get; init; }
    public string VetSpendingLimit { get; init; } = "0.00";
    public EmergencyContactRequest EmergencyContact { get; init; } = default!;
    public string? Notes { get; init; }

    /// <summary>Staff only: required when a reserved stay vet authorisation or limit changes.</summary>
    public string? VetAuthorityChangeReason { get; init; }
}

public sealed class CancelBoardingStayRequest
{
    public string? Note { get; init; }
}

public sealed class OverrideVaccinationRequest
{
    public string Reason { get; init; } = default!;
}
