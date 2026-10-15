using CatEcommerce.Domain.Events.Visit;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Constants;
using Platform.Infrastructure.Core.Domain;

namespace CatEcommerce.Domain.Aggregates.Visit;

/// <summary>
/// HO-EMR-001 §8.4 / ADR-134 -- a per-service note template (US-CLN-20): the five text fields as
/// defaults, at most <b>one active template per service</b> (filtered unique index), versioned -- the
/// version rises on every edit. It is delivered in the visit payload for the <i>client</i> to pre-fill
/// (the note stays fully editable); the applied template id and version are recorded on the note.
/// </summary>
public sealed class NoteTemplateAggregate : AggregateRoot
{
    public Guid ClinicServiceId { get; private set; }
    public string Name { get; private set; } = default!;
    public string Subjective { get; private set; } = "";
    public string Objective { get; private set; } = "";
    public string Assessment { get; private set; } = "";
    public string Plan { get; private set; } = "";
    public string DischargeInstructions { get; private set; } = "";
    public int TemplateVersion { get; private set; } = 1;
    public bool IsActive { get; private set; } = true;

    private NoteTemplateAggregate() { }

    public NoteTemplateAggregate(Guid id) : base(id) { }

    public void Create(NoteTemplateCreateDto dto)
    {
        Validate(dto.Name, dto.Subjective, dto.Objective, dto.Assessment, dto.Plan, dto.DischargeInstructions);

        ClinicServiceId = dto.ClinicServiceId;
        Apply(dto.Name, dto.Subjective, dto.Objective, dto.Assessment, dto.Plan, dto.DischargeInstructions);
        TemplateVersion = 1;
        IsActive = true;

        SetDefaultValue(dto.UserContext);
        Raise(dto.CorrelationId, dto.UserContext);
        UpdateVersion();
    }

    /// <summary>Every edit raises <see cref="TemplateVersion"/> by one (U-A4).</summary>
    public void Update(NoteTemplateUpdateDto dto)
    {
        Validate(dto.Name, dto.Subjective, dto.Objective, dto.Assessment, dto.Plan, dto.DischargeInstructions);

        Apply(dto.Name, dto.Subjective, dto.Objective, dto.Assessment, dto.Plan, dto.DischargeInstructions);
        TemplateVersion++;

        SetDefaultValue(dto.UserContext);
        Raise(dto.CorrelationId, dto.UserContext);
        UpdateVersion();
    }

    public void SetActive(CatalogueActiveDto dto)
    {
        IsActive = dto.IsActive;

        SetDefaultValue(dto.UserContext);
        Raise(dto.CorrelationId, dto.UserContext);
        UpdateVersion();
    }

    private void Apply(string name, string subjective, string objective, string assessment, string plan, string discharge)
    {
        Name = name;
        Subjective = subjective;
        Objective = objective;
        Assessment = assessment;
        Plan = plan;
        DischargeInstructions = discharge;
    }

    private void Raise(Guid correlationId, Platform.Infrastructure.Common.Security.UserContext userContext) =>
        AddDomainEvent(new NoteTemplateChangedEvent
        {
            NoteTemplateId = Id, ClinicServiceId = ClinicServiceId, TemplateVersion = TemplateVersion, IsActive = IsActive,
            CorrelationId = correlationId, UserContext = userContext
        });

    private static void Validate(string name, params string[] texts)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > ClinicalNoteRules.MaxCatalogueNameLength)
            throw new DomainRuleViolationException(BusinessViolationCodes.NameRequired, "Enter a name of up to 200 characters.", nameof(name));

        if (texts.Any(t => t.Length > ClinicalNoteRules.MaxSoapFieldLength))
            throw new DomainRuleViolationException(BusinessViolationCodes.ClinicalTextTooLong, "A template text is too long.", nameof(texts));
    }
}
