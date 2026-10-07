namespace CatEcommerce.Api.Contracts.Administration;

public sealed class CreateClinicServiceRequest
{
    public string Code { get; init; } = default!;
    public string Name { get; init; } = default!;
    public string Description { get; init; } = default!;
    public string Category { get; init; } = default!;
    public int DurationMinutes { get; init; }
    public IReadOnlyList<string> QualifiedRoleIds { get; init; } = [];
    public string RequiredRoomKind { get; init; } = default!;
    public Guid TaxClassId { get; init; }
    public bool IsBookableOnline { get; init; }
}

public sealed class UpdateClinicServiceRequest
{
    public string Name { get; init; } = default!;
    public string Description { get; init; } = default!;
    public string Category { get; init; } = default!;
    public int DurationMinutes { get; init; }
    public IReadOnlyList<string> QualifiedRoleIds { get; init; } = [];
    public string RequiredRoomKind { get; init; } = default!;
    public Guid TaxClassId { get; init; }
    public bool IsBookableOnline { get; init; }
}

public sealed class SetServiceActivationRequest
{
    public bool IsActive { get; init; }
}

public sealed class SetServicePriceRequest
{
    public string Amount { get; init; } = default!;
    public string CurrencyCode { get; init; } = default!;
    public DateTimeOffset EffectiveFrom { get; init; }
}

public sealed class CreateTaxClassRequest
{
    public string Code { get; init; } = default!;
    public string Name { get; init; } = default!;
}

public sealed class UpdateTaxClassRequest
{
    public string Name { get; init; } = default!;
    public bool IsActive { get; init; }
}

public sealed class SetTaxRateRequest
{
    public decimal RatePercent { get; init; }
    public DateTimeOffset EffectiveFrom { get; init; }
}

public sealed class SettingChangeRequest
{
    public string Key { get; init; } = default!;
    public string Value { get; init; } = default!;
}

public sealed class ChangeSettingsRequest
{
    public IReadOnlyList<SettingChangeRequest> Changes { get; init; } = [];
}
