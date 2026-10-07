using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Domain.Entities;

public sealed class CatAllergy
{
    public Guid Id { get; private set; }
    public string Allergen { get; private set; } = default!;
    public string Reaction { get; private set; } = default!;
    public AllergySeverity Severity { get; private set; } = default!;
    public DateOnly RecordedOn { get; private set; }

    private CatAllergy() { }

    public CatAllergy(Guid id, string allergen, string reaction, AllergySeverity severity, DateOnly recordedOn)
    {
        Id = id;
        Allergen = allergen;
        Reaction = reaction;
        Severity = severity;
        RecordedOn = recordedOn;
    }
}
