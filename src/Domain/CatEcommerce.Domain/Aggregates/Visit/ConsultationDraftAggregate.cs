using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Core.Domain;

namespace CatEcommerce.Domain.Aggregates.Visit;

/// <summary>
/// HO-EMR-001 §8.4 / ADR-122 -- the author's <b>auto-saved working copy</b> of a consultation note:
/// one row per <c>(VisitId, AuthorStaffId)</c> (unique), created on first write and overwritten on every
/// auto-save. It is <i>not</i> the record -- the note on the visit changes only on an explicit save --
/// so it is excluded from audit change capture (<c>AuditCaptureExclusions</c>: it is overwritten every
/// few seconds and would otherwise flood the trail) and is returned only to its author, and only while
/// newer than the saved note on a still-open visit. It is never deleted.
/// </summary>
public sealed class ConsultationDraftAggregate : AggregateRoot
{
    public Guid VisitId { get; private set; }
    public Guid AuthorStaffId { get; private set; }
    public string Subjective { get; private set; } = "";
    public string Objective { get; private set; } = "";
    public string Assessment { get; private set; } = "";
    public string Plan { get; private set; } = "";
    public string DischargeInstructions { get; private set; } = "";

    /// <summary>Diagnosis-code ids only (stored as <c>jsonb</c>) -- the draft carries no problem text.</summary>
    public List<Guid> ProblemCodeIds { get; private set; } = [];

    public DateTimeOffset SavedOn { get; private set; }

    private ConsultationDraftAggregate() { }

    public ConsultationDraftAggregate(Guid id) : base(id) { }

    /// <summary>Creates the draft on first write, overwrites it afterwards. The visit must be open.</summary>
    public void Overwrite(ConsultationDraftDto dto)
    {
        if (dto.VisitStatus != VisitStatus.Open)
            throw new DomainRuleViolationException(BusinessViolationCodes.VisitFinalised, ClinicalMessages.VisitFinalised, nameof(dto.VisitStatus));

        foreach (var (name, text, max) in new (string, string, int)[]
                 {
                     (nameof(dto.Subjective), dto.Subjective, ClinicalNoteRules.MaxSoapFieldLength),
                     (nameof(dto.Objective), dto.Objective, ClinicalNoteRules.MaxSoapFieldLength),
                     (nameof(dto.Assessment), dto.Assessment, ClinicalNoteRules.MaxSoapFieldLength),
                     (nameof(dto.Plan), dto.Plan, ClinicalNoteRules.MaxSoapFieldLength),
                     (nameof(dto.DischargeInstructions), dto.DischargeInstructions, ClinicalNoteRules.MaxDischargeInstructionsLength),
                 })
        {
            if (text.Length > max)
                throw new DomainRuleViolationException(BusinessViolationCodes.ClinicalTextTooLong, $"{name} cannot exceed {max} characters.", name);
        }

        if (dto.ProblemCodeIds.Count > ClinicalNoteRules.MaxProblems)
            throw new DomainRuleViolationException(
                BusinessViolationCodes.ClinicalLimitExceeded, $"A draft lists at most {ClinicalNoteRules.MaxProblems} problems.", nameof(dto.ProblemCodeIds));

        VisitId = dto.VisitId;
        AuthorStaffId = dto.AuthorStaffId;
        Subjective = dto.Subjective;
        Objective = dto.Objective;
        Assessment = dto.Assessment;
        Plan = dto.Plan;
        DischargeInstructions = dto.DischargeInstructions;
        ProblemCodeIds = dto.ProblemCodeIds.Distinct().ToList();
        SavedOn = dto.Now;

        SetDefaultValue(dto.UserContext);
        UpdateVersion();
    }
}
