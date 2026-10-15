using CatEcommerce.Application.Commands.Visit;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using FluentValidation;

namespace CatEcommerce.Application.CommandHandlers.Validators;

// HO-EMR-001 §11.2 -- shape only. Whether a value is numeric or qualitative, the reference-range order and the
// classification are aggregate rules.

public sealed class OrderDiagnosticCommandValidator : AbstractValidator<OrderDiagnosticCommand>
{
    public OrderDiagnosticCommandValidator()
    {
        RuleFor(x => x.DiagnosticOrderId).NotEmpty();
        RuleFor(x => x.VisitId).NotEmpty();
        RuleFor(x => x.BranchId).NotEmpty();
        RuleFor(x => x.Kind).NotEmpty().MustBeValidEnumeration<OrderDiagnosticCommand, DiagnosticKind>();
        RuleFor(x => x.Description).MaximumLength(DiagnosticRules.MaxDescriptionLength).NoControlCharacters();
        RuleFor(x => x.AnalyteIds).NotNull().Must(ids => ids.Count <= DiagnosticRules.MaxRequestedAnalytes)
            .WithMessage($"An order lists at most {DiagnosticRules.MaxRequestedAnalytes} analytes.");
        RuleForEach(x => x.AnalyteIds).NotEmpty();
    }
}

public sealed class ResultValueInputValidator : AbstractValidator<ResultValueInput>
{
    public ResultValueInputValidator()
    {
        RuleFor(x => x.ValueId).NotEmpty();
        RuleFor(x => x.Name).MaximumLength(DiagnosticRules.MaxValueNameLength).NoControlCharacters();
        RuleFor(x => x.ValueKind).MustBeValidEnumeration<ResultValueInput, ResultValueKind>().When(x => x.ValueKind is not null);
        RuleFor(x => x.TextValue).MaximumLength(DiagnosticRules.MaxTextValueLength).NoControlCharacters();
        RuleFor(x => x.Unit).MaximumLength(DiagnosticRules.MaxUnitLength).NoControlCharacters();
        RuleFor(x => x.NormalText).MaximumLength(DiagnosticRules.MaxNormalTextLength).NoControlCharacters();
        // numeric(14,4): at most four decimals and ten integer digits.
        RuleFor(x => x.NumericValue).Must(FitsNumeric144).When(x => x.NumericValue is not null).WithMessage("A numeric value has at most 4 decimal places.");
        RuleFor(x => x.ReferenceLow).Must(FitsNumeric144).When(x => x.ReferenceLow is not null).WithMessage("A reference bound has at most 4 decimal places.");
        RuleFor(x => x.ReferenceHigh).Must(FitsNumeric144).When(x => x.ReferenceHigh is not null).WithMessage("A reference bound has at most 4 decimal places.");
    }

    private static bool FitsNumeric144(decimal? value) =>
        value is null || (decimal.Round(value.Value, 4) == value.Value && Math.Abs(value.Value) < 10_000_000_000m);
}

public sealed class RecordDiagnosticResultsCommandValidator : AbstractValidator<RecordDiagnosticResultsCommand>
{
    public RecordDiagnosticResultsCommandValidator()
    {
        RuleFor(x => x.DiagnosticOrderId).NotEmpty();
        RuleFor(x => x.ResultSetId).NotEmpty();
        RuleFor(x => x.BranchId).NotEmpty();
        RuleFor(x => x.ReportedOn).NotEqual(default(DateOnly));
        RuleFor(x => x.Values).NotEmpty().Must(v => v.Count <= DiagnosticRules.MaxResultValuesPerSet)
            .WithMessage($"A result set has between 1 and {DiagnosticRules.MaxResultValuesPerSet} values.");
        RuleForEach(x => x.Values).SetValidator(new ResultValueInputValidator());
        RuleFor(x => x.Note).MaximumLength(DiagnosticRules.MaxResultNoteLength).NoControlCharacters();
    }
}

public sealed class AttachDiagnosticFileCommandValidator : AbstractValidator<AttachDiagnosticFileCommand>
{
    public AttachDiagnosticFileCommandValidator()
    {
        RuleFor(x => x.DiagnosticOrderId).NotEmpty();
        RuleFor(x => x.AttachmentId).NotEmpty();
        RuleFor(x => x.BranchId).NotEmpty();
        RuleFor(x => x.FileId).NotEmpty();
        RuleFor(x => x.ResultDate).NotEqual(default(DateOnly));
        RuleFor(x => x.Note).MaximumLength(DiagnosticRules.MaxAttachmentNoteLength).NoControlCharacters();
    }
}

public sealed class CancelDiagnosticOrderCommandValidator : AbstractValidator<CancelDiagnosticOrderCommand>
{
    public CancelDiagnosticOrderCommandValidator()
    {
        RuleFor(x => x.DiagnosticOrderId).NotEmpty();
        RuleFor(x => x.BranchId).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(DiagnosticRules.MaxCancelReasonLength).NoControlCharacters();
    }
}

public sealed class AttachClinicalPhotoCommandValidator : AbstractValidator<AttachClinicalPhotoCommand>
{
    public AttachClinicalPhotoCommandValidator()
    {
        RuleFor(x => x.VisitId).NotEmpty();
        RuleFor(x => x.PhotoId).NotEmpty();
        RuleFor(x => x.BranchId).NotEmpty();
        RuleFor(x => x.FileId).NotEmpty();
        RuleFor(x => x.TakenOn).NotEqual(default(DateOnly));
        RuleFor(x => x.Note).MaximumLength(ClinicalNoteRules.MaxPhotoNoteLength).NoControlCharacters();
    }
}
