using CatEcommerce.Read.ViewModels;
using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Read.Queries.Clinic;

/// <summary>HO-APT-001 §12.6. Never computes availability in SQL -- loads through
/// <c>IAppointmentSchedulingInputs</c> and calls <c>AppointmentSlotFinder</c>.</summary>
public sealed class GetAvailableSlotsQuery : Query<AvailableSlotsViewModel>
{
    public required Guid BranchId { get; init; }
    public required Guid ServiceId { get; init; }
    public required DateOnly Date { get; init; }
    public Guid? PerformerId { get; init; }

    /// <summary>Only honoured with a customer session (§12.4) -- excludes the cat's own bookings.</summary>
    public Guid? CatId { get; init; }
}

public sealed class GetStaffAvailableSlotsQuery : Query<IReadOnlyList<StaffSlotViewModel>>
{
    public required Guid BranchId { get; init; }
    public required Guid ServiceId { get; init; }
    public required DateOnly Date { get; init; }
    public Guid? PerformerId { get; init; }
    public Guid? CatId { get; init; }
}

public sealed class GetBookingPerformersQuery : Query<IReadOnlyList<BookingPerformerViewModel>>
{
    public required Guid BranchId { get; init; }
    public required Guid ServiceId { get; init; }
}

public sealed class GetCustomerAppointmentsQuery : Query<PagedResult<AppointmentSummaryViewModel>>
{
    public required Guid CustomerId { get; init; }

    /// <summary>"upcoming" or "past".</summary>
    public required string Scope { get; init; }
}

public sealed class GetCustomerAppointmentDetailQuery : Query<CustomerAppointmentDetailViewModel?>
{
    public required Guid AppointmentId { get; init; }
    public required Guid CustomerId { get; init; }
}

public sealed class GetCustomerAppointmentSummaryQuery : Query<CustomerAppointmentSummaryViewModel>
{
    public required Guid CustomerId { get; init; }
}

public sealed class GetCustomerWaitlistQuery : Query<IReadOnlyList<WaitlistEntryViewModel>>
{
    public required Guid CustomerId { get; init; }
}

public sealed class GetAppointmentDetailQuery : Query<AppointmentDetailViewModel?>
{
    public required Guid AppointmentId { get; init; }

    /// <summary>The staff caller's active branch; an appointment of another branch is not found.</summary>
    public required Guid BranchId { get; init; }
}

public sealed class GetDayListQuery : Query<IReadOnlyList<CalendarAppointmentViewModel>>
{
    public required Guid BranchId { get; init; }
    public required DateOnly Date { get; init; }
}

public sealed class GetCalendarQuery : Query<CalendarViewModel>
{
    public required Guid BranchId { get; init; }
    public required DateOnly From { get; init; }
    public required DateOnly To { get; init; }

    /// <summary>"performer" or "room".</summary>
    public required string View { get; init; }
    public IReadOnlyList<Guid>? Ids { get; init; }
}

public sealed class GetClinicQueueQuery : Query<ClinicQueueViewModel>
{
    public required Guid BranchId { get; init; }

    /// <summary>"me" resolves to the caller in the controller.</summary>
    public Guid? PerformerId { get; init; }
}

public sealed class GetFlaggedAppointmentsQuery : Query<IReadOnlyList<FlaggedAppointmentViewModel>>
{
    public required Guid BranchId { get; init; }
}

/// <summary>Worker queries (§12.6) -- <c>ClinicWorkCycle</c>'s own reads.</summary>
public sealed class GetWaitlistOfferCandidatesQuery : Query<IReadOnlyList<Guid>>
{
    public required DateTimeOffset Now { get; init; }
}

public sealed class GetLapsedWaitlistOffersQuery : Query<IReadOnlyList<Guid>>
{
    public required DateTimeOffset Now { get; init; }
}

/// <summary>CODE_REVIEW gate fix (2026-09-28, HIGH): "today" is branch-local, not a single global
/// UTC date -- the handler compares <paramref name="Now"/> against each row's own branch's local
/// date (<c>BranchCalendar.LocalDateOf</c>), the same way <see cref="GetWaitlistOfferCandidatesQuery"/>
/// and <see cref="GetLapsedWaitlistOffersQuery"/> already carry <c>Now</c> rather than a
/// precomputed date.</summary>
public sealed class GetPastWaitlistEntriesQuery : Query<IReadOnlyList<Guid>>
{
    public required DateTimeOffset Now { get; init; }
}
