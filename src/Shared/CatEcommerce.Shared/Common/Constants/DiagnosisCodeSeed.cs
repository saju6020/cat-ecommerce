namespace CatEcommerce.Shared.Common.Constants;

/// <summary>
/// HO-EMR-001 §8.2 / ADR-123 -- the one seeded system diagnosis code. It keeps a well visit
/// finalisable (a visit needs at least one problem to finalise) and may be neither renamed nor
/// deactivated (<c>SystemDiagnosisCodeReadOnly</c>).
/// </summary>
public static class DiagnosisCodeSeed
{
    public static class RoutineNoAbnormality
    {
        public static readonly Guid Id = new("5e1d0a6c-7b0e-4d0e-9a54-0c3a7c1b5a01");
        public const string Code = "SYS-NAD";
        public const string Name = "Routine visit — no abnormality detected";
    }

    public static bool IsSystemCode(string code) => string.Equals(code, RoutineNoAbnormality.Code, StringComparison.Ordinal);
}
