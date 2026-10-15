using System.Text.RegularExpressions;
using CatEcommerce.Domain.Events.Visit;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Core.Domain;

namespace CatEcommerce.Domain.Aggregates.Visit;

/// <summary>
/// HO-EMR-001 §8.5 -- one analyte of the laboratory catalogue: its value kind, unit and default
/// reference range. A result value <i>copies</i> these defaults onto itself when the lab report enters
/// none (so editing the catalogue never changes a past result). No seed: the content is clinical
/// (OQ-3). Deactivated, never deleted.
/// </summary>
public sealed partial class LabAnalyteAggregate : AggregateRoot
{
    public string Code { get; private set; } = default!;
    public string Name { get; private set; } = default!;
    public ResultValueKind ValueKind { get; private set; } = default!;
    public DiagnosticKind DiagnosticKind { get; private set; } = default!;
    public string? Unit { get; private set; }
    public decimal? ReferenceLow { get; private set; }
    public decimal? ReferenceHigh { get; private set; }
    public string? NormalText { get; private set; }
    public bool IsActive { get; private set; } = true;

    private LabAnalyteAggregate() { }

    public LabAnalyteAggregate(Guid id) : base(id) { }

    public void Create(LabAnalyteCreateDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Code) || dto.Code.Length > DiagnosticRules.MaxAnalyteCodeLength || !CodeFormat().IsMatch(dto.Code))
            throw new DomainRuleViolationException(
                BusinessViolationCodes.InvalidDiagnosisCode,
                "An analyte code is upper-case letters, digits, '.', '_' or '-', up to 30 characters.", nameof(dto.Code));

        Validate(dto.Name, dto.ValueKind, dto.Unit, dto.ReferenceLow, dto.ReferenceHigh, dto.NormalText);

        Code = dto.Code;
        Apply(dto.Name, dto.ValueKind, dto.DiagnosticKind, dto.Unit, dto.ReferenceLow, dto.ReferenceHigh, dto.NormalText);
        IsActive = true;

        SetDefaultValue(dto.UserContext);
        AddDomainEvent(new LabAnalyteChangedEvent { LabAnalyteId = Id, IsActive = IsActive, CorrelationId = dto.CorrelationId, UserContext = dto.UserContext });
        UpdateVersion();
    }

    public void Update(LabAnalyteUpdateDto dto)
    {
        Validate(dto.Name, dto.ValueKind, dto.Unit, dto.ReferenceLow, dto.ReferenceHigh, dto.NormalText);
        Apply(dto.Name, dto.ValueKind, dto.DiagnosticKind, dto.Unit, dto.ReferenceLow, dto.ReferenceHigh, dto.NormalText);

        SetDefaultValue(dto.UserContext);
        AddDomainEvent(new LabAnalyteChangedEvent { LabAnalyteId = Id, IsActive = IsActive, CorrelationId = dto.CorrelationId, UserContext = dto.UserContext });
        UpdateVersion();
    }

    public void SetActive(CatalogueActiveDto dto)
    {
        IsActive = dto.IsActive;

        SetDefaultValue(dto.UserContext);
        AddDomainEvent(new LabAnalyteChangedEvent { LabAnalyteId = Id, IsActive = IsActive, CorrelationId = dto.CorrelationId, UserContext = dto.UserContext });
        UpdateVersion();
    }

    private void Apply(string name, ResultValueKind valueKind, DiagnosticKind diagnosticKind, string? unit, decimal? low, decimal? high, string? normalText)
    {
        Name = name;
        ValueKind = valueKind;
        DiagnosticKind = diagnosticKind;
        // A qualitative analyte has a normal text and no numeric range; a numeric one the reverse.
        Unit = valueKind == ResultValueKind.Numeric ? unit : null;
        ReferenceLow = valueKind == ResultValueKind.Numeric ? low : null;
        ReferenceHigh = valueKind == ResultValueKind.Numeric ? high : null;
        NormalText = valueKind == ResultValueKind.Qualitative && !string.IsNullOrWhiteSpace(normalText) ? normalText : null;
    }

    private static void Validate(string name, ResultValueKind valueKind, string? unit, decimal? low, decimal? high, string? normalText)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > DiagnosticRules.MaxValueNameLength)
            throw new DomainRuleViolationException(BusinessViolationCodes.NameRequired, "Enter a name of up to 100 characters.", nameof(name));

        if (unit is { Length: > DiagnosticRules.MaxUnitLength } || normalText is { Length: > DiagnosticRules.MaxNormalTextLength })
            throw new DomainRuleViolationException(BusinessViolationCodes.InvalidReferenceRange, "The unit or normal text is too long.", nameof(unit));

        if (valueKind == ResultValueKind.Numeric)
        {
            if (string.IsNullOrWhiteSpace(unit))
                throw new DomainRuleViolationException(BusinessViolationCodes.InvalidReferenceRange, "A numeric analyte needs a unit.", nameof(unit));

            if (low is { } l && high is { } h && l > h)
                throw new DomainRuleViolationException(BusinessViolationCodes.InvalidReferenceRange, "The low bound cannot be above the high bound.", nameof(low));
        }
    }

    [GeneratedRegex("^[A-Z0-9][A-Z0-9._-]*$")]
    private static partial Regex CodeFormat();
}
