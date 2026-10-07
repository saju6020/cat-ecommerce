using CatEcommerce.Application.Commands.Clinic;
using FluentValidation;

namespace CatEcommerce.Application.CommandHandlers.Validators;

/// <summary>HO-APT-001 §11.2. <paramref name="timeProvider"/> so "not in the past" never reads the
/// wall clock directly (§16.1, tests must not depend on it).</summary>
public sealed class JoinWaitlistCommandValidator : AbstractValidator<JoinWaitlistCommand>
{
    public JoinWaitlistCommandValidator(TimeProvider timeProvider)
    {
        RuleFor(x => x.WaitlistEntryId).NotEmpty();
        RuleFor(x => x.BranchId).NotEmpty();
        RuleFor(x => x.ServiceId).NotEmpty();
        RuleFor(x => x.CatId).NotEmpty();

        // "not in the past by UTC date minus one day" -- the precise "today" is branch-local and
        // the handler's; this is only a coarse, timezone-agnostic shape check (§11.2).
        RuleFor(x => x.LocalDate)
            .Must(date => date >= DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime).AddDays(-1))
            .WithMessage("must not be in the past.");
    }
}

public sealed class WithdrawFromWaitlistCommandValidator : AbstractValidator<WithdrawFromWaitlistCommand>
{
    public WithdrawFromWaitlistCommandValidator() => RuleFor(x => x.WaitlistEntryId).NotEmpty();
}

public sealed class OfferWaitlistSlotCommandValidator : AbstractValidator<OfferWaitlistSlotCommand>
{
    public OfferWaitlistSlotCommandValidator()
    {
        RuleFor(x => x.WaitlistEntryId).NotEmpty();
        RuleFor(x => x.AppointmentId).NotEmpty();
    }
}

public sealed class LapseWaitlistOfferCommandValidator : AbstractValidator<LapseWaitlistOfferCommand>
{
    public LapseWaitlistOfferCommandValidator() => RuleFor(x => x.WaitlistEntryId).NotEmpty();
}

public sealed class CloseStaleWaitlistEntriesCommandValidator : AbstractValidator<CloseStaleWaitlistEntriesCommand>
{
    public CloseStaleWaitlistEntriesCommandValidator() => RuleFor(x => x.WaitlistEntryId).NotEmpty();
}
