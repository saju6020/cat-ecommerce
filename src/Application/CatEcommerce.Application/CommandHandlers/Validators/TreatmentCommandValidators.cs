using CatEcommerce.Application.Commands.Visit;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using FluentValidation;

namespace CatEcommerce.Application.CommandHandlers.Validators;

// HO-EMR-001 section 11.2 -- shape only. Whether the SKU exists, is controlled or has stock, which batch is FEFO, expiry, the shortfall and every
// price are handler rules; whether the quantity is whole is the ledger's rule for the SKU (HO-IMP-001 OQ-19).

public sealed class RecordTreatmentCommandValidator : AbstractValidator<RecordTreatmentCommand>
{
    public RecordTreatmentCommandValidator()
    {
        RuleFor(x => x.VisitId).NotEmpty();
        RuleFor(x => x.LineId).NotEmpty();
        RuleFor(x => x.BranchId).NotEmpty();
        RuleFor(x => x.Kind).NotEmpty().MustBeValidEnumeration<RecordTreatmentCommand, TreatmentKind>();

        RuleFor(x => x.Description).MaximumLength(ClinicalNoteRules.MaxTreatmentDescriptionLength).NoControlCharacters();
        RuleFor(x => x.Description).NotEmpty()
            .When(x => string.Equals(x.Kind, TreatmentKind.Procedure.DisplayName, StringComparison.OrdinalIgnoreCase))
            .WithMessage(ClinicalMessages.TreatmentDescriptionRequired);

        RuleFor(x => x.SkuId).NotEmpty()
            .When(x => x.SkuId is not null || (EnumerationParsing.TryParse<TreatmentKind>(x.Kind, out var kind) && kind is not null && kind.MovesStock))
            .WithMessage("Choose the item from the dispensary.");
        RuleFor(x => x.SkuId).Null()
            .When(x => string.Equals(x.Kind, TreatmentKind.Procedure.DisplayName, StringComparison.OrdinalIgnoreCase))
            .WithMessage("A procedure does not take stock. Record the items used as consumables.");

        RuleFor(x => x.Quantity).GreaterThan(0m).LessThanOrEqualTo(ClinicalNoteRules.MaxTreatmentQuantity)
            .Must(q => decimal.Round(q, 3) == q).WithMessage("The quantity is stated to at most 3 decimal places.");

        RuleFor(x => x.Dose).MaximumLength(ClinicalNoteRules.MaxDoseLength).NoControlCharacters();
        RuleFor(x => x.Route).MaximumLength(ClinicalNoteRules.MaxRouteLength).NoControlCharacters();
        RuleFor(x => x.BatchOverrideReason).MaximumLength(ClinicalNoteRules.MaxBatchOverrideReasonLength).NoControlCharacters();
    }
}

public sealed class ReverseTreatmentLineCommandValidator : AbstractValidator<ReverseTreatmentLineCommand>
{
    public ReverseTreatmentLineCommandValidator()
    {
        RuleFor(x => x.VisitId).NotEmpty();
        RuleFor(x => x.LineId).NotEmpty();
        RuleFor(x => x.BranchId).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty().WithMessage(ClinicalMessages.ReversalReasonRequired)
            .MaximumLength(ClinicalNoteRules.MaxTreatmentReversalReasonLength).NoControlCharacters();
    }
}
