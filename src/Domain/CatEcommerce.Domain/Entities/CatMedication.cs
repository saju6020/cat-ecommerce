namespace CatEcommerce.Domain.Entities;

public sealed class CatMedication
{
    public Guid Id { get; private set; }
    public string MedicationName { get; private set; } = default!;
    public string Dosage { get; private set; } = default!;
    public string Frequency { get; private set; } = default!;
    public DateOnly StartedOn { get; private set; }
    public DateOnly? EndsOn { get; private set; }

    private CatMedication() { }

    public CatMedication(
        Guid id, string medicationName, string dosage, string frequency,
        DateOnly startedOn, DateOnly? endsOn)
    {
        Id = id;
        MedicationName = medicationName;
        Dosage = dosage;
        Frequency = frequency;
        StartedOn = startedOn;
        EndsOn = endsOn;
    }
}
