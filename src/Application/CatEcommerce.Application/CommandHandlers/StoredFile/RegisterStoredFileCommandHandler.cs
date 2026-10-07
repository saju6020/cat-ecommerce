using CatEcommerce.Application.Commands.StoredFile;
using CatEcommerce.Application.DataMappers;
using CatEcommerce.Domain.Aggregates.StoredFile;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Constants;
using Microsoft.Extensions.Logging;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Domain;
using Platform.Infrastructure.Core.Validation;

namespace CatEcommerce.Application.CommandHandlers.StoredFile;

/// <summary>
/// HO-FIL-001 §8.2 step 7 / §11.3.
///
/// Gate-fix 2026-09-27 (ARCHITECTURE MEDIUM, cqrs.md §5 rule 1): the command's own
/// <c>CommandResponse.Result</c> is the full §12.4 signed-file shape
/// (<see cref="StoredFileRegistrationResult"/>) -- <c>FilesController</c>'s upload action returns
/// it directly instead of dispatching <c>GetStoredFileQuery</c> as a second, separate dispatch
/// right after this command.
/// </summary>
public sealed class RegisterStoredFileCommandHandler(
    ILogger<RegisterStoredFileCommandHandler> logger,
    IAggregateRootRepository<StoredFileAggregate> fileRepository,
    IFileUrlSigner signer,
    IBusinessSettings businessSettings,
    TimeProvider timeProvider)
    : ICommandHandlerAsync<RegisterStoredFileCommand>
{
    public async Task<CommandResponse> HandleAsync(RegisterStoredFileCommand command)
    {
        logger.LogInformation(
            "Registering stored file {FileId} for purpose {Purpose}. CorrelationId: {CorrelationId}",
            command.FileId, command.Purpose.DisplayName, command.CorrelationId);

        var file = new StoredFileAggregate(command.FileId);

        try
        {
            file.Register(command.ToDomainDto());
        }
        catch (DomainRuleViolationException ex)
        {
            return Violation(ex.ErrorCode, ex.PropertyName, ex.Message);
        }

        await fileRepository.SaveAsync(file, EventPublishOption.InMemoryPublishOnly);

        var disposition = FileServingRules.ResolveDisposition(file.ContentType, FileServingRules.Inline);
        var signedUrlMinutes = businessSettings.GetInt(SettingKeys.FilesSignedUrlMinutes);
        var expiresIn = TimeSpan.FromMinutes(Math.Max(1, signedUrlMinutes));
        var url = signer.Sign(file.Id, expiresIn, disposition);

        var result = new StoredFileRegistrationResult(
            file.Id, url, file.ContentType, file.SizeInBytes, file.OriginalFileName, timeProvider.GetUtcNow().Add(expiresIn));

        return new CommandResponse { IsSuccess = true, Result = result };
    }

    private static CommandResponse Violation(string code, string property, string message)
    {
        var validationResponse = new ValidationResponse();
        validationResponse.AddError(errorMessage: message, propertyName: property,
            errorCode: code, resourceName: nameof(StoredFileAggregate));
        return new CommandResponse { IsSuccess = false, ValidationResult = validationResponse };
    }
}
