using CatEcommerce.Domain.Events.Visit;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.Models;
using CatEcommerce.Domain.ValueObjects;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Core.Domain;

namespace CatEcommerce.Domain.Aggregates.Visit;

/// <summary>
/// HO-EMR-001 §8.9 / ADR-132 / NFR-LEG-4 -- a consent for a procedure (surgery, anaesthesia,
/// euthanasia), by on-screen signature (stroke data) or signed upload, linked to the cat, the
/// appointment that is the procedure booking, the kind, the date and the <b>exact consent-form version</b>
/// shown. Its own root: it belongs to a procedure, not to a note. It can be <i>withdrawn</i> but never
/// edited or deleted (the database refuses both). <see cref="ConsentRules.IsValidFor"/> is Surgery's
/// contract (US-CLN-21 AC2).
/// </summary>
public sealed class ConsentRecordAggregate : AggregateRoot
{
    public Guid CatId { get; private set; }
    public Guid AppointmentId { get; private set; }
    public Guid BranchId { get; private set; }
    public ConsentKind Kind { get; private set; } = default!;
    public string ProcedureDescription { get; private set; } = default!;
    public ConsentMethod Method { get; private set; } = default!;
    public SignatureStrokes? Signature { get; private set; }
    public Guid? FileId { get; private set; }
    public string SignedByName { get; private set; } = default!;
    public SignerRelationship SignerRelationship { get; private set; } = default!;
    public Guid? SignedByCustomerId { get; private set; }
    public DateTimeOffset SignedOn { get; private set; }
    public string PolicyKey { get; private set; } = default!;
    public int PolicyVersionNumber { get; private set; }
    public Guid CapturedBy { get; private set; }
    public Guid? WitnessStaffId { get; private set; }
    public ConsentStatus Status { get; private set; } = ConsentStatus.Recorded;
    public DateTimeOffset? WithdrawnOn { get; private set; }
    public Guid? WithdrawnBy { get; private set; }
    public string? WithdrawalReason { get; private set; }

    private ConsentRecordAggregate() { }

    public ConsentRecordAggregate(Guid id) : base(id) { }

    public void Record(ConsentRecordingDto dto)
    {
        var hasSignature = dto.Signature is not null;
        var hasFile = dto.FileId is not null;

        // Method <=> exactly one of signature / file.
        var consistent = dto.Method == ConsentMethod.OnScreenSignature ? hasSignature && !hasFile
            : dto.Method == ConsentMethod.SignedUpload && hasFile && !hasSignature;
        if (!consistent)
            throw Violation(BusinessViolationCodes.ConsentEvidenceMismatch,
                "An on-screen consent carries a signature and no file; an uploaded consent carries a file and no signature.", nameof(dto.Method));

        if (dto.CatLifeStatus != CatLifeStatus.Alive)
            throw Violation(BusinessViolationCodes.CatIsDeceased, "A consent cannot be recorded for a deceased cat.", nameof(dto.CatId));

        if (dto.AppointmentCatId != dto.CatId || dto.AppointmentStatus == AppointmentStatus.Cancelled)
            throw Violation(BusinessViolationCodes.ConsentAppointmentInvalid,
                "Choose this cat's procedure appointment, which must not be cancelled.", nameof(dto.AppointmentId));

        if (!string.Equals(dto.PolicyKey, ConsentForms.KeyFor(dto.Kind), StringComparison.Ordinal)
            || dto.CurrentPolicyVersionNumber is null || dto.PolicyVersionNumber != dto.CurrentPolicyVersionNumber)
            throw Violation(BusinessViolationCodes.ConsentFormVersionNotCurrent,
                "The consent form has been updated. Show the current wording and capture the consent again.", nameof(dto.PolicyVersionNumber));

        if (string.IsNullOrWhiteSpace(dto.ProcedureDescription) || dto.ProcedureDescription.Length > 500)
            throw Violation(BusinessViolationCodes.ClinicalTextTooLong, "Describe the procedure in 1 to 500 characters.", nameof(dto.ProcedureDescription));

        if (string.IsNullOrWhiteSpace(dto.SignedByName) || dto.SignedByName.Length > 200)
            throw Violation(BusinessViolationCodes.ClinicalTextTooLong, "Enter the signer's name (up to 200 characters).", nameof(dto.SignedByName));

        CatId = dto.CatId;
        AppointmentId = dto.AppointmentId;
        BranchId = dto.BranchId;
        Kind = dto.Kind;
        ProcedureDescription = dto.ProcedureDescription;
        Method = dto.Method;
        Signature = dto.Signature;
        FileId = dto.FileId;
        SignedByName = dto.SignedByName;
        SignerRelationship = dto.SignerRelationship;
        SignedByCustomerId = dto.SignedByCustomerId;
        SignedOn = dto.Now;
        PolicyKey = dto.PolicyKey;
        PolicyVersionNumber = dto.PolicyVersionNumber;
        CapturedBy = dto.CapturedBy;
        WitnessStaffId = dto.WitnessStaffId;
        Status = ConsentStatus.Recorded;

        SetDefaultValue(dto.UserContext);
        AddDomainEvent(new ConsentRecordedEvent
        {
            ConsentId = Id, CatId = CatId, AppointmentId = AppointmentId, Kind = Kind, Method = Method,
            PolicyVersionNumber = PolicyVersionNumber, CorrelationId = dto.CorrelationId, UserContext = dto.UserContext
        });
        UpdateVersion();
    }

    public void Withdraw(ConsentWithdrawalDto dto)
    {
        if (Status != ConsentStatus.Recorded)
            throw Violation(BusinessViolationCodes.ConsentAlreadyWithdrawn, "This consent has already been withdrawn.", nameof(Status));

        if (string.IsNullOrWhiteSpace(dto.Reason) || dto.Reason.Length > 500)
            throw Violation(BusinessViolationCodes.ClinicalTextTooLong, "Give a reason of 1 to 500 characters.", nameof(dto.Reason));

        Status = ConsentStatus.Withdrawn;
        WithdrawnOn = dto.Now;
        WithdrawnBy = dto.WithdrawnBy;
        WithdrawalReason = dto.Reason;

        SetDefaultValue(dto.UserContext);
        AddDomainEvent(new ConsentWithdrawnEvent
        {
            ConsentId = Id, CatId = CatId, AppointmentId = AppointmentId, CorrelationId = dto.CorrelationId, UserContext = dto.UserContext
        });
        UpdateVersion();
    }

    private static DomainRuleViolationException Violation(string code, string message, string property) => new(code, message, property);
}
