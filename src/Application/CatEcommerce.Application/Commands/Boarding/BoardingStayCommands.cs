using Platform.Infrastructure.Core.Commands;

namespace CatEcommerce.Application.Commands.Boarding;

// HO-BRD-001 section 11.1 C5-C8. Data only. A CUSTOMER command carries CustomerId (from the session) and no branch scope; a STAFF command carries the
// active BranchId and (for enquiries) the customer being booked for. The handler resolves the stay by id AND caller: someone else's stay is "not found".

public sealed record FeedingInput(TimeOnly Time, string Portion, string FoodSource, string FoodDescription, Guid? HouseFoodSkuId);

public sealed record MedicationInput(string Name, string Dose, string Route, IReadOnlyList<TimeOnly> Times, string? Instructions, DateOnly? FromOn, DateOnly? ToOn);

public sealed record EmergencyContactInput(string Name, string Phone, string? Relationship);

public sealed record CareProfileInput(
    IReadOnlyList<FeedingInput> Feedings, IReadOnlyList<MedicationInput> Medications, string LitterType, string? LitterNote,
    bool VetVisitAuthorised, decimal VetSpendingLimit, EmergencyContactInput EmergencyContact, string? Notes);

/// <summary>Who is acting on a stay: a customer (own stays only) or staff (stays of the active branch).</summary>
public interface IStayScoped
{
    /// <summary>Set for a customer caller: the session's customer id.</summary>
    Guid? CustomerId { get; }

    /// <summary>Set for a staff caller: the active branch.</summary>
    Guid? BranchId { get; }
}

public sealed class CreateBoardingEnquiryCommand : Command
{
    public required Guid StayId { get; init; }
    public required Guid BranchId { get; init; }
    public required Guid CustomerId { get; init; }

    /// <summary>True when sent by staff on a customer's behalf (reception channel, no horizon, no cut-off).</summary>
    public bool IsStaff { get; init; }
    public required DateOnly ArrivalOn { get; init; }
    public required DateOnly DepartureOn { get; init; }
    public required string UnitType { get; init; }
    public required IReadOnlyList<Guid> CatIds { get; init; }
}

public sealed class ChangeBoardingEnquiryCommand : Command, IStayScoped
{
    public required Guid StayId { get; init; }
    public Guid? CustomerId { get; init; }
    public Guid? BranchId { get; init; }
    public required DateOnly ArrivalOn { get; init; }
    public required DateOnly DepartureOn { get; init; }
    public required string UnitType { get; init; }
    public required IReadOnlyList<Guid> CatIds { get; init; }
}

public sealed class SetStayCareProfileCommand : Command, IStayScoped
{
    public required Guid StayId { get; init; }
    public required Guid CatId { get; init; }
    public Guid? CustomerId { get; init; }
    public Guid? BranchId { get; init; }
    public required CareProfileInput Profile { get; init; }

    /// <summary>Staff only: why the vet authorisation or limit of a reserved stay changed (required when it does).</summary>
    public string? VetAuthorityChangeReason { get; init; }
}

public sealed class OverrideVaccinationGateCommand : Command, IStayScoped
{
    public required Guid StayId { get; init; }
    public required Guid CatId { get; init; }
    public Guid? CustomerId { get; init; }
    public Guid? BranchId { get; init; }
    public required string Reason { get; init; }
}

public sealed class ReserveBoardingStayCommand : Command, IStayScoped
{
    public required Guid StayId { get; init; }
    public Guid? CustomerId { get; init; }
    public Guid? BranchId { get; init; }
}
