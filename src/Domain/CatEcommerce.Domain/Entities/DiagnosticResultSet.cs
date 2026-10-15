namespace CatEcommerce.Domain.Entities;

/// <summary>
/// HO-EMR-001 §8.5 / ADR-124 -- one report of results for an order. A correction is a <i>new</i> set
/// naming the one it supersedes; the earlier set stays visible. Append-only (trigger).
/// </summary>
public sealed class DiagnosticResultSet
{
    private readonly List<DiagnosticResultValue> _values = [];

    public Guid Id { get; private set; }
    public DateOnly ReportedOn { get; private set; }
    public string? Note { get; private set; }
    public Guid? SupersedesResultSetId { get; private set; }
    public Guid RecordedBy { get; private set; }
    public DateTimeOffset RecordedOn { get; private set; }

    public IReadOnlyList<DiagnosticResultValue> Values => _values;

    private DiagnosticResultSet() { }

    public DiagnosticResultSet(
        Guid id, DateOnly reportedOn, string? note, Guid? supersedesResultSetId, Guid recordedBy, DateTimeOffset recordedOn,
        IEnumerable<DiagnosticResultValue> values)
    {
        Id = id;
        ReportedOn = reportedOn;
        Note = note;
        SupersedesResultSetId = supersedesResultSetId;
        RecordedBy = recordedBy;
        RecordedOn = recordedOn;
        _values.AddRange(values);
        for (var i = 0; i < _values.Count; i++) _values[i].SetPosition(i);
    }
}
