using CatEcommerce.Application.Commands.Visit;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using FluentValidation;

namespace CatEcommerce.Application.CommandHandlers.Validators;

// HO-EMR-001 §11.2 -- shape only: ids, lengths, enum membership, the signature's array shape. Its bounds (1-200 strokes, at
// least two points each, 10-5000 points, coordinates in [0,1] with at most 4 decimals) are SignatureStrokes' own.

public sealed class RecordConsentCommandValidator : AbstractValidator<RecordConsentCommand>
{
    public RecordConsentCommandValidator()
    {
        RuleFor(x => x.ConsentId).NotEmpty();
        RuleFor(x => x.CatId).NotEmpty();
        RuleFor(x => x.AppointmentId).NotEmpty();
        RuleFor(x => x.BranchId).NotEmpty();
        RuleFor(x => x.Kind).NotEmpty().MustBeValidEnumeration<RecordConsentCommand, ConsentKind>();
        RuleFor(x => x.Method).NotEmpty().MustBeValidEnumeration<RecordConsentCommand, ConsentMethod>();
        RuleFor(x => x.SignerRelationship).NotEmpty().MustBeValidEnumeration<RecordConsentCommand, SignerRelationship>();
        RuleFor(x => x.ProcedureDescription).NotEmpty().MaximumLength(500).NoControlCharacters();
        RuleFor(x => x.SignedByName).NotEmpty().MaximumLength(200).NoControlCharacters();
        RuleFor(x => x.PolicyVersionNumber).GreaterThan(0);

        // Shape only: every stroke is a list of [x, y] pairs.
        RuleFor(x => x.Signature).Must(HasPointShape).When(x => x.Signature is not null)
            .WithMessage("A signature is a list of strokes, each a list of [x, y] points.");
    }

    private static bool HasPointShape(decimal[][][]? strokes) =>
        strokes is not null && strokes.All(stroke => stroke is not null && stroke.All(point => point is { Length: 2 }));
}

public sealed class WithdrawConsentCommandValidator : AbstractValidator<WithdrawConsentCommand>
{
    public WithdrawConsentCommandValidator()
    {
        RuleFor(x => x.ConsentId).NotEmpty();
        RuleFor(x => x.BranchId).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500).NoControlCharacters();
    }
}
