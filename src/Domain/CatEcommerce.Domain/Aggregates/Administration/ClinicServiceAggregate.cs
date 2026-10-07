using CatEcommerce.Domain.Events.Administration;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Core.Domain;

namespace CatEcommerce.Domain.Aggregates.Administration;

/// <summary>HO-ADM-001 §8.6. No price on the aggregate -- see <see cref="ServicePriceAggregate"/>.</summary>
public sealed class ClinicServiceAggregate : AggregateRoot
{
    private readonly List<string> _qualifiedRoleIds = [];

    public string Code { get; private set; } = default!;
    public string Name { get; private set; } = default!;
    public string Description { get; private set; } = default!;
    public ServiceCategory Category { get; private set; } = default!;
    public int DurationMinutes { get; private set; }
    public IReadOnlyList<string> QualifiedRoleIds => _qualifiedRoleIds;
    public RoomKind RequiredRoomKind { get; private set; } = default!;
    public Guid TaxClassId { get; private set; }
    public bool IsBookableOnline { get; private set; }
    public bool IsActive { get; private set; }

    private ClinicServiceAggregate() { }

    public ClinicServiceAggregate(Guid id) : base(id) { }

    public void Create(ClinicServiceCreateDto dto)
    {
        ValidateFields(dto.Name, dto.Description, dto.DurationMinutes, dto.QualifiedRoleIds);

        Code = dto.Code;
        Name = dto.Name;
        Description = dto.Description;
        Category = dto.Category;
        DurationMinutes = dto.DurationMinutes;
        _qualifiedRoleIds.Clear();
        _qualifiedRoleIds.AddRange(dto.QualifiedRoleIds);
        RequiredRoomKind = dto.RequiredRoomKind;
        TaxClassId = dto.TaxClassId;
        IsBookableOnline = dto.IsBookableOnline;
        IsActive = true;

        SetDefaultValue(dto.UserContext);

        AddDomainEvent(new ClinicServiceCreatedEvent { ServiceId = Id, CorrelationId = dto.CorrelationId, UserContext = dto.UserContext });
        UpdateVersion();
    }

    public void Update(ClinicServiceUpdateDto dto)
    {
        ValidateFields(dto.Name, dto.Description, dto.DurationMinutes, dto.QualifiedRoleIds);

        Name = dto.Name;
        Description = dto.Description;
        Category = dto.Category;
        DurationMinutes = dto.DurationMinutes;
        _qualifiedRoleIds.Clear();
        _qualifiedRoleIds.AddRange(dto.QualifiedRoleIds);
        RequiredRoomKind = dto.RequiredRoomKind;
        TaxClassId = dto.TaxClassId;
        IsBookableOnline = dto.IsBookableOnline;

        AddDomainEvent(new ClinicServiceUpdatedEvent { ServiceId = Id, CorrelationId = dto.CorrelationId, UserContext = dto.UserContext });
        UpdateVersion();
    }

    public void SetActivation(ClinicServiceActivationDto dto)
    {
        if (IsActive == dto.IsActive) return;

        IsActive = dto.IsActive;

        AddDomainEvent(new ClinicServiceActivationChangedEvent { ServiceId = Id, IsActive = IsActive, CorrelationId = dto.CorrelationId, UserContext = dto.UserContext });
        UpdateVersion();
    }

    private static void ValidateFields(string name, string description, int durationMinutes, IReadOnlyList<string> qualifiedRoleIds)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > 200)
            throw new DomainRuleViolationException(BusinessViolationCodes.NameRequired, "A service name is required.", nameof(Name));

        if (description.Length > 2000)
            throw new DomainRuleViolationException(BusinessViolationCodes.InvalidServiceDuration, "The description is too long.", nameof(Description));

        if (durationMinutes is < 5 or > 480 || durationMinutes % 5 != 0)
            throw new DomainRuleViolationException(BusinessViolationCodes.InvalidServiceDuration, "Duration must be between 5 and 480 minutes, in multiples of 5.", nameof(DurationMinutes));

        if (qualifiedRoleIds.Count == 0)
            throw new DomainRuleViolationException(BusinessViolationCodes.ServiceRolesRequired, "At least one qualified role is required.", nameof(QualifiedRoleIds));

        foreach (var role in qualifiedRoleIds)
            if (!StaffRoles.All.Contains(role, StringComparer.Ordinal))
                throw new DomainRuleViolationException(BusinessViolationCodes.UnknownStaffRoleReferenced, $"'{role}' is not a known staff role.", nameof(QualifiedRoleIds));
    }
}
