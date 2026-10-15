namespace CatEcommerce.Domain.Entities;

/// <summary>HO-EMR-001 §8.5 -- an analyte asked of the laboratory, snapshotted from the catalogue.</summary>
public sealed class DiagnosticRequestedAnalyte
{
    public Guid Id { get; private set; }
    public Guid AnalyteId { get; private set; }
    public string Code { get; private set; } = default!;
    public string Name { get; private set; } = default!;

    private DiagnosticRequestedAnalyte() { }

    public DiagnosticRequestedAnalyte(Guid id, Guid analyteId, string code, string name)
    {
        Id = id;
        AnalyteId = analyteId;
        Code = code;
        Name = name;
    }
}
