using CatEcommerce.Application.Commands.Administration;
using FluentValidation;

namespace CatEcommerce.Application.CommandHandlers.Validators;

public sealed class RecordAuditLogExportCommandValidator : AbstractValidator<RecordAuditLogExportCommand>
{
    public RecordAuditLogExportCommandValidator()
    {
        RuleFor(x => x.To).GreaterThanOrEqualTo(x => x.From);
    }
}
