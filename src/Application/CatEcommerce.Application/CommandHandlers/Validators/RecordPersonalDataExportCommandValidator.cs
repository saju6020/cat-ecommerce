using CatEcommerce.Application.Commands.Account;
using FluentValidation;

namespace CatEcommerce.Application.CommandHandlers.Validators;

/// <summary>No payload beyond the base Command -- registered so the platform's dispatch pipeline
/// (which requires an IValidator&lt;T&gt; whenever ValidateCommand=true) has one to resolve.</summary>
public sealed class RecordPersonalDataExportCommandValidator : AbstractValidator<RecordPersonalDataExportCommand>;
