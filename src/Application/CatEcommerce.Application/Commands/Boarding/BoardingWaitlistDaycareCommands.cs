using Platform.Infrastructure.Core.Commands;

namespace CatEcommerce.Application.Commands.Boarding;

// HO-BRD-001 section 11.1 C3 (daycare rate card), C24 and C25 (Phase H), and the worker's daily update.

public sealed class JoinBoardingWaitlistCommand : Command
{
    public required Guid EntryId { get; init; }
    public required Guid BranchId { get; init; }
    public required Guid CustomerId { get; init; }
    public required DateOnly ArrivalOn { get; init; }
    public required DateOnly DepartureOn { get; init; }
    public required int CatCount { get; init; }

    /// <summary>A <c>BoardingUnitType</c> name, or <c>null</c> for any type.</summary>
    public string? UnitType { get; init; }
}

public sealed class WithdrawBoardingWaitlistCommand : Command
{
    public required Guid EntryId { get; init; }
    public required Guid CustomerId { get; init; }
}

/// <summary>Worker only: tells the owner of the oldest waiting entry that a unit is free for the range.</summary>
public sealed class NotifyWaitlistEntryCommand : Command
{
    public required Guid EntryId { get; init; }
}

/// <summary>Worker only: closes a waiting entry whose arrival date has passed.</summary>
public sealed class CloseStaleBoardingWaitlistCommand : Command
{
    public required Guid EntryId { get; init; }
}

public sealed class SetDaycareRateCardCommand : Command
{
    public required Guid RateCardId { get; init; }
    public required Guid BranchId { get; init; }
    public required decimal HourlyRate { get; init; }
    public required decimal DayRate { get; init; }
    public required Guid TaxClassId { get; init; }
    public required DateTimeOffset EffectiveFrom { get; init; }
}

public sealed class BookDaycareVisitCommand : Command
{
    public required Guid VisitId { get; init; }
    public required Guid BranchId { get; init; }
    public required Guid CustomerId { get; init; }
    public required Guid CatId { get; init; }
    public required DateOnly LocalDate { get; init; }

    /// <summary>A <c>DaycareBasis</c> name: Hourly or Day.</summary>
    public required string Basis { get; init; }
    public int? ExpectedHours { get; init; }
}

public sealed class CheckInDaycareVisitCommand : Command
{
    public required Guid VisitId { get; init; }
    public required Guid BranchId { get; init; }
}

public sealed class CheckOutDaycareVisitCommand : Command
{
    public required Guid VisitId { get; init; }
    public required Guid BranchId { get; init; }
}

/// <summary>The owner cancels their own visit (<c>CustomerId</c>), or staff cancel one of their branch (<c>BranchId</c>).</summary>
public sealed class CancelDaycareVisitCommand : Command
{
    public required Guid VisitId { get; init; }
    public Guid? CustomerId { get; init; }
    public Guid? BranchId { get; init; }
}

public sealed class RecordDaycareNoShowCommand : Command
{
    public required Guid VisitId { get; init; }
    public required Guid BranchId { get; init; }
}

/// <summary>Worker only: the stay's daily update for one local date, through HO-NTF-001 (deduplicated per stay and day, guarded while the stay is in house).</summary>
public sealed class SendBoardingDailyUpdateCommand : Command
{
    public required Guid StayId { get; init; }
    public required DateOnly LocalDate { get; init; }
}
