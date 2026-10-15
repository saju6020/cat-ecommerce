using CatEcommerce.Application.Commands.Visit;
using CatEcommerce.Application.DataMappers;
using CatEcommerce.Domain.Aggregates.Administration;
using CatEcommerce.Domain.Aggregates.Cat;
using CatEcommerce.Domain.Aggregates.Clinic;
using CatEcommerce.Domain.Aggregates.StoredFile;
using CatEcommerce.Domain.Aggregates.Visit;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.Models;
using CatEcommerce.Domain.ValueObjects;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Domain;

namespace CatEcommerce.Application.CommandHandlers.Visit;

using static VisitCommandHandlerSupport;

/// <summary>
/// C19 -- records consent for a procedure (§8.9, US-CLN-21, ADR-132). The handler loads what the consent depends on and the
/// aggregate decides: the appointment and cat, the <b>current</b> published version of the consent form for the kind (HO-ADM-001
/// policy document <c>consent-surgery</c> / <c>-anaesthesia</c> / <c>-euthanasia</c>), the evidence (an on-screen signature
/// <i>xor</i> a signed upload) and the signer. An on-screen signature is stroke data -- no upload, no scan -- so signing works
/// even where uploads are refused (ADR-132). A signed upload must be a stored file of purpose <c>ConsentForm</c> uploaded by the
/// caller, referenced once. The signed-by customer id, when given, is a frozen fact read through HO-ACC-001's merge chain.
/// </summary>
public sealed class RecordConsentCommandHandler(
    IAggregateRootRepository<ConsentRecordAggregate> consentRepository,
    IAggregateRootRepository<AppointmentAggregate> appointmentRepository,
    IAggregateRootRepository<CatAggregate> catRepository,
    IAggregateRootRepository<PolicyDocumentAggregate> policyRepository,
    IAggregateRootRepository<StoredFileAggregate> fileRepository,
    TimeProvider timeProvider)
    : ICommandHandlerAsync<RecordConsentCommand>
{
    public async Task<CommandResponse> HandleAsync(RecordConsentCommand command)
    {
        var appointment = await appointmentRepository.GetByIdAsync(command.AppointmentId);
        if (appointment is null || appointment.BranchId != command.BranchId)
            return Violation(BusinessViolationCodes.AppointmentNotFound, nameof(command.AppointmentId), "Appointment not found.", nameof(ConsentRecordAggregate));

        var cat = await catRepository.GetByIdAsync(command.CatId);
        if (cat is null)
            return Violation(BusinessViolationCodes.CatNotFound, nameof(command.CatId), "Cat not found.", nameof(ConsentRecordAggregate));

        var kind = AppointmentMapper.ParseEnum<ConsentKind>(command.Kind);
        var method = AppointmentMapper.ParseEnum<ConsentMethod>(command.Method);
        var policyKey = ConsentForms.KeyFor(kind);

        SignatureStrokes? signature = null;
        if (command.Signature is not null)
        {
            try { signature = SignatureStrokes.Create(command.Signature); }
            catch (DomainRuleViolationException ex) { return Violation(ex.ErrorCode, ex.PropertyName, ex.Message, nameof(ConsentRecordAggregate)); }
        }

        var now = timeProvider.GetUtcNow();
        if (method == ConsentMethod.SignedUpload && command.FileId is { } fileId)
        {
            var file = await fileRepository.GetByIdAsync(fileId);
            if (file is null)
                return Violation(BusinessViolationCodes.FileNotFound, nameof(command.FileId), "The file was not found. Upload it again.", nameof(StoredFileAggregate));

            if (!file.IsEligibleForAttach(FilePurpose.ConsentForm, command.UserContext.UserId, now, FileRules.AttachWindow))
                return Violation(BusinessViolationCodes.FilePurposeMismatch, nameof(command.FileId),
                    "This file cannot be attached here. Upload it as a ConsentForm file and attach it straight away.", nameof(StoredFileAggregate));

            // One file, one consent. (A file of this purpose can be referenced from no other clinical table.)
            if (await consentRepository.ExistAsync(c => c.FileId == fileId))
                return Violation(BusinessViolationCodes.FileAlreadyAttached, nameof(command.FileId), "This file is already attached to a clinical record.", nameof(StoredFileAggregate));
        }

        var policy = await policyRepository.GetByFilterAsync(d => d.Key == policyKey);

        var consent = new ConsentRecordAggregate(command.ConsentId);
        try
        {
            consent.Record(new ConsentRecordingDto(
                command.CatId, command.AppointmentId, appointment.CatId, appointment.Status, appointment.BranchId, cat.LifeStatus,
                kind, command.ProcedureDescription, method, signature, command.FileId, command.SignedByName,
                AppointmentMapper.ParseEnum<SignerRelationship>(command.SignerRelationship), command.SignedByCustomerId,
                policyKey, command.PolicyVersionNumber, policy?.CurrentVersionNumber, command.UserContext.UserId, command.WitnessStaffId,
                now, command.UserContext, command.CorrelationId));
        }
        catch (DomainRuleViolationException ex)
        {
            return Violation(ex.ErrorCode, ex.PropertyName, ex.Message, nameof(ConsentRecordAggregate));
        }

        await consentRepository.SaveAsync(consent, EventPublishOption.InMemoryPublishOnly);
        return Success(consent.Id);
    }
}

/// <summary>C20 -- withdraws a recorded consent. Nothing is deleted: the status changes and who, when and why are kept
/// (the status contract Surgery reads then answers <c>false</c>).</summary>
public sealed class WithdrawConsentCommandHandler(
    IAggregateRootRepository<ConsentRecordAggregate> consentRepository,
    TimeProvider timeProvider)
    : ICommandHandlerAsync<WithdrawConsentCommand>
{
    public async Task<CommandResponse> HandleAsync(WithdrawConsentCommand command)
    {
        var consent = await consentRepository.GetByIdAsync(command.ConsentId);
        if (consent is null || consent.BranchId != command.BranchId)
            return Violation(BusinessViolationCodes.ConsentNotFound, nameof(command.ConsentId), "Consent not found.", nameof(ConsentRecordAggregate));

        try
        {
            consent.Withdraw(new ConsentWithdrawalDto(command.Reason, command.UserContext.UserId, timeProvider.GetUtcNow(), command.UserContext, command.CorrelationId));
        }
        catch (DomainRuleViolationException ex)
        {
            return Violation(ex.ErrorCode, ex.PropertyName, ex.Message, nameof(ConsentRecordAggregate));
        }

        await consentRepository.UpdateAsync(consent, EventPublishOption.InMemoryPublishOnly);
        return Success();
    }
}
