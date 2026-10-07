using CatEcommerce.Application.Commands.StoredFile;
using CatEcommerce.Domain.Aggregates.StoredFile;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Shared.Common.Constants;
using Microsoft.Extensions.Logging;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Domain;
using Platform.Infrastructure.Core.Validation;

namespace CatEcommerce.Application.CommandHandlers.StoredFile;

/// <summary>
/// HO-FIL-001 §8.7 / §11.3. Unlike every other handler in this solution, this one does not call
/// <c>repository.UpdateAsync(...)</c> -- <c>StoredFileAggregate</c> has no mutation method at all
/// (§8.1: "there is no Update"), so a real hard delete goes through <see cref="IStoredFileEraser"/>
/// (object first, row second) instead of the generic aggregate repository, which has no
/// hard-delete member (.ai/persistence-ef-core.md §1). The repository is still used for the
/// existence/<c>LegalHold</c> check -- a read-only lookup, exactly like any other handler's
/// existence check (.ai/command-handler-pattern.md §4).
/// </summary>
public sealed class DeleteStoredFileCommandHandler(
    ILogger<DeleteStoredFileCommandHandler> logger,
    IAggregateRootRepository<StoredFileAggregate> fileRepository,
    IStoredFileEraser eraser)
    : ICommandHandlerAsync<DeleteStoredFileCommand>
{
    public async Task<CommandResponse> HandleAsync(DeleteStoredFileCommand command)
    {
        logger.LogInformation("Deleting stored file {FileId}. CorrelationId: {CorrelationId}", command.FileId, command.CorrelationId);

        var file = await fileRepository.GetByIdAsync(command.FileId);
        if (file is null)
            return Violation(BusinessViolationCodes.FileNotFound, nameof(command.FileId), $"File '{command.FileId}' was not found.");

        if (file.LegalHold)
            return Violation(BusinessViolationCodes.FileLegallyHeld, nameof(command.FileId), $"File '{command.FileId}' is under legal hold and cannot be deleted.");

        await eraser.EraseAsync(command.FileId, CancellationToken.None);

        return new CommandResponse { IsSuccess = true };
    }

    private static CommandResponse Violation(string code, string property, string message)
    {
        var validationResponse = new ValidationResponse();
        validationResponse.AddError(errorMessage: message, propertyName: property,
            errorCode: code, resourceName: nameof(StoredFileAggregate));
        return new CommandResponse { IsSuccess = false, ValidationResult = validationResponse };
    }
}
