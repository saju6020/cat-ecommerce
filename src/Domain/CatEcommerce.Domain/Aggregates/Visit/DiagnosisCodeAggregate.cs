using System.Text.RegularExpressions;
using CatEcommerce.Domain.Events.Visit;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Constants;
using Platform.Infrastructure.Core.Domain;

namespace CatEcommerce.Domain.Aggregates.Visit;

/// <summary>
/// HO-EMR-001 §8.2 / ADR-123 -- the in-house coded diagnosis catalogue (OQ-2), with an optional external
/// code (VeNom or SNOMED, mapped later). <see cref="Code"/> is an upper-case key, unique, and immutable
/// once created; <c>SYS-NAD</c> ("Routine visit -- no abnormality detected") is seeded, may be neither
/// renamed nor deactivated, and keeps a well visit finalisable. A code is deactivated, never deleted
/// (visits snapshot it and refer to it).
/// </summary>
public sealed partial class DiagnosisCodeAggregate : AggregateRoot
{
    public string Code { get; private set; } = default!;
    public string Name { get; private set; } = default!;
    public string? Category { get; private set; }
    public string? ExternalSystem { get; private set; }
    public string? ExternalCode { get; private set; }
    public bool IsActive { get; private set; } = true;

    public bool IsSystem => DiagnosisCodeSeed.IsSystemCode(Code);

    private DiagnosisCodeAggregate() { }

    public DiagnosisCodeAggregate(Guid id) : base(id) { }

    public void Create(DiagnosisCodeCreateDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Code) || dto.Code.Length > ClinicalNoteRules.MaxDiagnosisCodeLength || !CodeFormat().IsMatch(dto.Code))
            throw new DomainRuleViolationException(
                BusinessViolationCodes.InvalidDiagnosisCode,
                "A diagnosis code is upper-case letters, digits, '.', '_' or '-', up to 30 characters.", nameof(dto.Code));

        ValidateDetails(dto.Name, dto.Category, dto.ExternalSystem, dto.ExternalCode);

        Code = dto.Code;
        Name = dto.Name;
        Category = Clean(dto.Category);
        ExternalSystem = Clean(dto.ExternalSystem);
        ExternalCode = Clean(dto.ExternalCode);
        IsActive = true;

        SetDefaultValue(dto.UserContext);
        AddDomainEvent(new DiagnosisCodeChangedEvent { DiagnosisCodeId = Id, IsActive = IsActive, CorrelationId = dto.CorrelationId, UserContext = dto.UserContext });
        UpdateVersion();
    }

    public void Update(DiagnosisCodeUpdateDto dto)
    {
        EnsureNotSystem();
        ValidateDetails(dto.Name, dto.Category, dto.ExternalSystem, dto.ExternalCode);

        Name = dto.Name;
        Category = Clean(dto.Category);
        ExternalSystem = Clean(dto.ExternalSystem);
        ExternalCode = Clean(dto.ExternalCode);

        SetDefaultValue(dto.UserContext);
        AddDomainEvent(new DiagnosisCodeChangedEvent { DiagnosisCodeId = Id, IsActive = IsActive, CorrelationId = dto.CorrelationId, UserContext = dto.UserContext });
        UpdateVersion();
    }

    public void SetActive(CatalogueActiveDto dto)
    {
        EnsureNotSystem();

        IsActive = dto.IsActive;

        SetDefaultValue(dto.UserContext);
        AddDomainEvent(new DiagnosisCodeChangedEvent { DiagnosisCodeId = Id, IsActive = IsActive, CorrelationId = dto.CorrelationId, UserContext = dto.UserContext });
        UpdateVersion();
    }

    private void EnsureNotSystem()
    {
        if (IsSystem)
            throw new DomainRuleViolationException(
                BusinessViolationCodes.SystemDiagnosisCodeReadOnly, "The system diagnosis code cannot be changed or deactivated.", nameof(Code));
    }

    private static void ValidateDetails(string name, string? category, string? externalSystem, string? externalCode)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > ClinicalNoteRules.MaxDiagnosisNameLength)
            throw new DomainRuleViolationException(BusinessViolationCodes.NameRequired, "Enter a name of up to 200 characters.", nameof(name));

        if (category is { Length: > ClinicalNoteRules.MaxCategoryLength }
            || externalSystem is { Length: > ClinicalNoteRules.MaxExternalCodeLength }
            || externalCode is { Length: > ClinicalNoteRules.MaxExternalCodeLength })
            throw new DomainRuleViolationException(BusinessViolationCodes.ClinicalTextTooLong, "A category or external code is too long.", nameof(category));
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    [GeneratedRegex("^[A-Z0-9][A-Z0-9._-]*$")]
    private static partial Regex CodeFormat();
}
