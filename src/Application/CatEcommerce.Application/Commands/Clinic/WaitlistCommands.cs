using Platform.Infrastructure.Core.Commands;

namespace CatEcommerce.Application.Commands.Clinic;

/// <summary>HO-APT-001 §11.1/§8.11.</summary>
public sealed class JoinWaitlistCommand : Command
{
    public required Guid WaitlistEntryId { get; init; }
    public required Guid BranchId { get; init; }
    public required Guid ServiceId { get; init; }
    public required Guid CatId { get; init; }
    public required DateOnly LocalDate { get; init; }
    public Guid? PreferredPerformerId { get; init; }
}

public sealed class WithdrawFromWaitlistCommand : Command
{
    public required Guid WaitlistEntryId { get; init; }
}

/// <summary>Dispatched by <c>ClinicWorker</c>, never by a controller.</summary>
public sealed class OfferWaitlistSlotCommand : Command
{
    public required Guid WaitlistEntryId { get; init; }
    public required Guid AppointmentId { get; init; }
}

public sealed class LapseWaitlistOfferCommand : Command
{
    public required Guid WaitlistEntryId { get; init; }
}

/// <summary>One entry per call -- the worker calls this once per stale entry it finds (§11.1).</summary>
public sealed class CloseStaleWaitlistEntriesCommand : Command
{
    public required Guid WaitlistEntryId { get; init; }
}
