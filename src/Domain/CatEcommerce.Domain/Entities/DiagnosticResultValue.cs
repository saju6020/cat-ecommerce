using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Domain.Entities;

/// <summary>
/// HO-EMR-001 §8.5 -- one result value. Name, unit and reference range are <b>stored on the value</b>
/// (defaulted from the analyte catalogue when the lab report did not enter its own), so a later change
/// to the catalogue never changes what a vet saw. <see cref="Flag"/> is computed once, by
/// <c>ReferenceRangeClassifier</c>, against the range stored here. Append-only (trigger).
/// </summary>
public sealed class DiagnosticResultValue
{
    public Guid Id { get; private set; }
    public Guid? AnalyteId { get; private set; }
    public string Name { get; private set; } = default!;
    public ResultValueKind ValueKind { get; private set; } = default!;
    public decimal? NumericValue { get; private set; }
    public string? TextValue { get; private set; }
    public string? Unit { get; private set; }
    public decimal? ReferenceLow { get; private set; }
    public decimal? ReferenceHigh { get; private set; }
    public string? NormalText { get; private set; }
    public ResultFlag Flag { get; private set; } = default!;

    /// <summary>The value's place in its result set as entered -- a lab report is read in the order it was written.</summary>
    public int Position { get; private set; }

    private DiagnosticResultValue() { }

    public DiagnosticResultValue(
        Guid id, Guid? analyteId, string name, ResultValueKind valueKind, decimal? numericValue, string? textValue,
        string? unit, decimal? referenceLow, decimal? referenceHigh, string? normalText, ResultFlag flag)
    {
        Id = id;
        AnalyteId = analyteId;
        Name = name;
        ValueKind = valueKind;
        NumericValue = numericValue;
        TextValue = textValue;
        Unit = unit;
        ReferenceLow = referenceLow;
        ReferenceHigh = referenceHigh;
        NormalText = normalText;
        Flag = flag;
    }

    internal void SetPosition(int position) => Position = position;
}
