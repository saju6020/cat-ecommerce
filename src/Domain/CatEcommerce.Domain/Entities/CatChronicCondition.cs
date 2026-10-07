namespace CatEcommerce.Domain.Entities;

public sealed class CatChronicCondition
{
    public Guid Id { get; private set; }
    public string Condition { get; private set; } = default!;
    public DateOnly? DiagnosedOn { get; private set; }
    public string? Notes { get; private set; }

    private CatChronicCondition() { }

    public CatChronicCondition(Guid id, string condition, DateOnly? diagnosedOn, string? notes)
    {
        Id = id;
        Condition = condition;
        DiagnosedOn = diagnosedOn;
        Notes = notes;
    }
}
