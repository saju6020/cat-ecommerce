using CatEcommerce.Application.Commands.Cat;
using CatEcommerce.Application.DataMappers;
using CatEcommerce.Domain.Aggregates.Cat;
using CatEcommerce.Domain.Aggregates.StoredFile;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.Extensions.Logging;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Domain;
using Platform.Infrastructure.Core.Validation;

namespace CatEcommerce.Application.CommandHandlers.Cat;

/// <summary>
/// HO-FIL-001 §1 Amended (integrator-authorised, closes the HO-CAT-002-SECURITY carried item: the
/// endpoint used to trust a client-supplied storage key).
///
/// Gate-fix 2026-09-27 (ARCHITECTURE HIGH, second pass): this handler mutates exactly ONE
/// aggregate -- <c>CatAggregate</c> -- per <c>.ai/command-handler-pattern.md</c> §7. The eligibility
/// check against <c>StoredFileAggregate</c> (purpose, uploader, freshness) is read-only
/// (<see cref="StoredFileAggregate.IsEligibleForAttach"/>); nothing on the file is saved. "One
/// file, one cat" is enforced by the unique database index on <c>clinic.cat_photo.file_id</c>, not
/// by state on the file -- a concurrent attach of the same file to two different cats now races on
/// that index instead of racing across two separately-committed aggregate saves, and the loser gets
/// the same <see cref="BusinessViolationCodes.PhotoFileNotEligible"/> 400 an ordinary ineligible
/// attach returns (mapped from the unique-violation exception by the host-wide
/// <c>UniqueConstraintExceptionFilter</c>, not caught here -- see that filter's own doc comment).
/// An earlier version of this handler also called <c>StoredFileAggregate.TryAttach</c>+`UpdateAsync`
/// to mark the file claimed; a second-pass gate found that left the file permanently, uselessly
/// claimed whenever the SECOND aggregate's own save failed for an unrelated reason (e.g. Cat's own
/// photo-limit check), because the two saves were not, and cannot be, one transaction. See
/// HO-FIL-001.md §1 Amended for the full rationale.
/// </summary>
public sealed class AddCatPhotoCommandHandler(
    ILogger<AddCatPhotoCommandHandler> logger,
    IAggregateRootRepository<CatAggregate> catRepository,
    IAggregateRootRepository<StoredFileAggregate> fileRepository,
    TimeProvider timeProvider)
    : ICommandHandlerAsync<AddCatPhotoCommand>
{
    public async Task<CommandResponse> HandleAsync(AddCatPhotoCommand command)
    {
        logger.LogInformation("Adding photo to cat {CatId}. CorrelationId: {CorrelationId}", command.CatId, command.CorrelationId);
        var cat = await catRepository.GetByIdAsync(command.CatId);
        if (cat is null)
            return Violation(BusinessViolationCodes.CatNotFound, nameof(command.CatId),
                $"Cat '{command.CatId}' was not found.");

        var file = await fileRepository.GetByIdAsync(command.FileId);
        var now = timeProvider.GetUtcNow();

        // One code for every refusal reason -- not found, wrong purpose, not uploaded by this
        // caller, or too old -- distinguishing them would let a caller probe which file ids exist
        // (mirrors ADR-043's "404, never 403" rationale, applied here). "Already attached
        // elsewhere" is not checked here at all -- see the unique index below.
        if (file is null || !file.IsEligibleForAttach(FilePurpose.CatProfilePhoto, command.UserContext.UserId, now, FileRules.AttachWindow))
        {
            return Violation(BusinessViolationCodes.PhotoFileNotEligible, nameof(command.FileId),
                "The uploaded file could not be attached to this cat. Upload it again and try immediately.");
        }

        try
        {
            cat.AddPhoto(command.ToDomainDto(now));
        }
        catch (DomainRuleViolationException ex)
        {
            return Violation(ex.ErrorCode, ex.PropertyName, ex.Message);
        }

        // A concurrent attach of this same FileId to a different cat throws here (23505 on
        // ix_cat_photo_file_id) -- deliberately not caught: UniqueConstraintExceptionFilter maps it
        // to this same PhotoFileNotEligible 400, so the two refusal paths cannot drift apart.
        await catRepository.UpdateAsync(cat, EventPublishOption.InMemoryPublishOnly);

        return new CommandResponse { IsSuccess = true, Result = command.PhotoId };
    }

    private static CommandResponse Violation(string code, string property, string message)
    {
        var validationResponse = new ValidationResponse();
        validationResponse.AddError(errorMessage: message, propertyName: property,
            errorCode: code, resourceName: nameof(CatAggregate));
        return new CommandResponse { IsSuccess = false, ValidationResult = validationResponse };
    }
}
