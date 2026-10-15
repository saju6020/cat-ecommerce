namespace CatEcommerce.Shared.Common.Enums;

/// <summary>HO-SRG-001 §10.1. Persisted by <c>Value</c> (ADR-022).</summary>
public sealed class AnaesthesiaMilestone(int value, string displayName) : Enumeration(value, displayName)
{
    public static readonly AnaesthesiaMilestone Premedication = new(1, nameof(Premedication));
    public static readonly AnaesthesiaMilestone Induction = new(2, nameof(Induction));
    public static readonly AnaesthesiaMilestone Intubation = new(3, nameof(Intubation));
    public static readonly AnaesthesiaMilestone IncisionStart = new(4, nameof(IncisionStart));
    public static readonly AnaesthesiaMilestone ProcedureEnd = new(5, nameof(ProcedureEnd));
    public static readonly AnaesthesiaMilestone Extubation = new(6, nameof(Extubation));
    public static readonly AnaesthesiaMilestone Other = new(7, nameof(Other));
}
