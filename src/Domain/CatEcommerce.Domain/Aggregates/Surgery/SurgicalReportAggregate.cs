using CatEcommerce.Domain.Entities;
using CatEcommerce.Domain.Events.Surgery;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Constants;
using Platform.Infrastructure.Core.Domain;

namespace CatEcommerce.Domain.Aggregates.Surgery;

/// <summary>
/// HO-SRG-001 §8.2 / §11.4 C12 (US-SRG-05, ADR-165, BR-6, BR-11, D-5) -- the surgical report of one surgery (its <c>Id</c> IS the surgery id, so there is at most one). A DRAFT is editable by
/// the vet; <b>finalising it (vet only) makes it immutable</b> -- here, and by a PostgreSQL trigger on the table. After that the only way to add to it is an <see cref="Addenda"/> row: append-only,
/// vet only, and <b>the root is not touched</b> (no version bump, no audit stamp), because an UPDATE of a finalised report row is exactly what the trigger refuses. Its own root, so a nurse
/// changing recovery status never bumps the report's <c>xmin</c> and the vet writing the report never bumps the surgery's.
/// </summary>
public sealed class SurgicalReportAggregate : AggregateRoot
{
    private readonly List<SurgicalReportAddendum> _addenda = [];

    public Guid SurgeryId { get; private set; }
    public Guid CatId { get; private set; }
    public Guid VisitId { get; private set; }
    public Guid BranchId { get; private set; }
    public string ProcedurePerformed { get; private set; } = "";
    public string Findings { get; private set; } = "";
    public string Technique { get; private set; } = "";
    public string SutureMaterial { get; private set; } = "";
    public bool HadComplications { get; private set; }
    public string? Complications { get; private set; }
    public Guid SurgeonId { get; private set; }
    public Guid AssistantId { get; private set; }
    public DateTimeOffset? FinalisedOn { get; private set; }
    public Guid? FinalisedBy { get; private set; }
    public IReadOnlyList<SurgicalReportAddendum> Addenda => _addenda;

    public bool IsFinalised => FinalisedOn is not null;

    private SurgicalReportAggregate() { }

    public SurgicalReportAggregate(Guid surgeryId) : base(surgeryId) { }

    /// <summary>Creates the draft on first save and replaces its text on every later one, until it is finalised (then <c>ReportNotEditable</c>).</summary>
    public void SaveDraft(SurgicalReportDraftDto dto)
    {
        if (IsFinalised)
            throw new DomainRuleViolationException(BusinessViolationCodes.ReportNotEditable, "This report has been finalised and can no longer be edited. Add an addendum instead.", nameof(FinalisedOn));

        Validate(dto);

        SurgeryId = Id;
        CatId = dto.CatId;
        VisitId = dto.VisitId;
        BranchId = dto.BranchId;
        ProcedurePerformed = dto.ProcedurePerformed.Trim();
        Findings = dto.Findings.Trim();
        Technique = dto.Technique.Trim();
        SutureMaterial = dto.SutureMaterial.Trim();
        HadComplications = dto.HadComplications;
        Complications = dto.HadComplications ? dto.Complications?.Trim() : null;
        SurgeonId = dto.SurgeonId;
        AssistantId = dto.AssistantId;

        SetDefaultValue(dto.UserContext);
        UpdateVersion();
    }

    /// <summary>Vet only. Every required field must be present, named in the refusal: procedure performed, findings, technique, suture material, surgeon, assistant, and the complications
    /// when <c>HadComplications</c> is set.</summary>
    public void Finalise(SurgicalReportFinaliseDto dto)
    {
        if (IsFinalised)
            throw new DomainRuleViolationException(BusinessViolationCodes.ReportAlreadyFinalised, "This report has already been finalised.", nameof(FinalisedOn));
        if (!dto.CallerIsVet)
            throw new DomainRuleViolationException(BusinessViolationCodes.VeterinarianRequired, "Only a veterinarian finalises a surgical report.", nameof(dto.CallerIsVet));

        var missing = new List<string>();
        if (string.IsNullOrWhiteSpace(ProcedurePerformed)) missing.Add("procedure performed");
        if (string.IsNullOrWhiteSpace(Findings)) missing.Add("findings");
        if (string.IsNullOrWhiteSpace(Technique)) missing.Add("technique");
        if (string.IsNullOrWhiteSpace(SutureMaterial)) missing.Add("suture material");
        if (SurgeonId == Guid.Empty) missing.Add("surgeon");
        if (AssistantId == Guid.Empty) missing.Add("assistant");
        if (HadComplications && string.IsNullOrWhiteSpace(Complications)) missing.Add("complications");
        if (missing.Count > 0)
            throw new DomainRuleViolationException(BusinessViolationCodes.ReportIncomplete, $"The report is incomplete: {string.Join(", ", missing)}.", nameof(ProcedurePerformed));

        FinalisedOn = dto.Now;
        FinalisedBy = dto.UserContext.UserId;

        SetDefaultValue(dto.UserContext);
        AddDomainEvent(new SurgicalReportFinalisedEvent { SurgeryId = Id, CatId = CatId, BranchId = BranchId, CorrelationId = dto.CorrelationId, UserContext = dto.UserContext });
        UpdateVersion();
    }

    /// <summary>An addendum to a FINALISED report (the only way to add to it): vet only, append-only. <b>Deliberately does not touch the root</b> -- no <c>UpdateVersion</c>, no
    /// <c>SetDefaultValue</c> -- because an UPDATE of a finalised report row is what the immutability trigger refuses (U-E6). The addendum row is its own INSERT.</summary>
    public void AddAddendum(SurgicalReportAddendumDto dto)
    {
        if (!IsFinalised)
            throw new DomainRuleViolationException(BusinessViolationCodes.ReportNotFinalised, "An addendum can only be added to a finalised report; edit the draft instead.", nameof(FinalisedOn));
        if (!dto.CallerIsVet)
            throw new DomainRuleViolationException(BusinessViolationCodes.VeterinarianRequired, "Only a veterinarian adds an addendum.", nameof(dto.CallerIsVet));

        var text = dto.Text?.Trim();
        if (string.IsNullOrEmpty(text) || text.Length > SurgeryRules.AddendumMax)
            throw new DomainRuleViolationException(BusinessViolationCodes.SurgeryTextInvalid, $"An addendum is 1 to {SurgeryRules.AddendumMax} characters.", nameof(dto.Text));
        if (_addenda.Count >= SurgeryRules.MaxAddenda)
            throw new DomainRuleViolationException(BusinessViolationCodes.SurgeryTextInvalid, $"A report has at most {SurgeryRules.MaxAddenda} addenda.", nameof(dto.Text));

        _addenda.Add(new SurgicalReportAddendum(dto.AddendumId, text, dto.UserContext.UserId, dto.Now));
        AddDomainEvent(new SurgicalReportAddendumAddedEvent { SurgeryId = Id, AddendumId = dto.AddendumId, CorrelationId = dto.CorrelationId, UserContext = dto.UserContext });
    }

    private static void Validate(SurgicalReportDraftDto dto)
    {
        static void Text(string? value, string name, int max)
        {
            if (value is not null && value.Length > max)
                throw new DomainRuleViolationException(BusinessViolationCodes.SurgeryTextInvalid, $"{name} is at most {max} characters.", name);
        }

        Text(dto.ProcedurePerformed, nameof(dto.ProcedurePerformed), SurgeryRules.ReportTextMax);
        Text(dto.Findings, nameof(dto.Findings), SurgeryRules.ReportTextMax);
        Text(dto.Technique, nameof(dto.Technique), SurgeryRules.ReportTextMax);
        Text(dto.SutureMaterial, nameof(dto.SutureMaterial), 500);
        Text(dto.Complications, nameof(dto.Complications), SurgeryRules.ReportTextMax);
    }
}
