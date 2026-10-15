using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Domain.Entities;

/// <summary>
/// HO-EMR-001 §8.2 -- one coded problem of a visit. <see cref="Code"/> and <see cref="Name"/> are
/// <b>snapshots</b> of the diagnosis code at the time it was recorded (U-C2): renaming the code
/// later never changes a visit.
///
/// A problem removed from an open visit is <b>withdrawn</b> (<see cref="WithdrawnOn"/>), never
/// deleted: the table is <c>RecordClass.Clinical</c> / <c>HardDeletePolicy.Forbidden</c> (HO-XCT-001
/// §8.2, C-ALL-4), so no EF <c>DELETE</c> is ever emitted for it. Reads and the finalisation rule
/// look only at <see cref="IsActive"/> problems.
/// </summary>
public sealed class VisitProblem
{
    public Guid Id { get; private set; }
    public Guid DiagnosisCodeId { get; private set; }
    public string Code { get; private set; } = default!;
    public string Name { get; private set; } = default!;
    public ProblemCertainty Certainty { get; private set; } = default!;
    public string? Note { get; private set; }
    public DateTimeOffset AddedOn { get; private set; }
    public DateTimeOffset? WithdrawnOn { get; private set; }

    public bool IsActive => WithdrawnOn is null;

    private VisitProblem() { }

    public VisitProblem(
        Guid id, Guid diagnosisCodeId, string code, string name, ProblemCertainty certainty, string? note, DateTimeOffset addedOn)
    {
        Id = id;
        DiagnosisCodeId = diagnosisCodeId;
        Code = code;
        Name = name;
        Certainty = certainty;
        Note = note;
        AddedOn = addedOn;
    }

    public void Update(Guid diagnosisCodeId, string code, string name, ProblemCertainty certainty, string? note)
    {
        DiagnosisCodeId = diagnosisCodeId;
        Code = code;
        Name = name;
        Certainty = certainty;
        Note = note;
        WithdrawnOn = null;
    }

    public void Withdraw(DateTimeOffset now) => WithdrawnOn ??= now;
}
