namespace CatEcommerce.Shared.Common.Enums;

/// <summary>HO-PHM-001 §10.1 -- How often a dose is given. <see cref="Label"/> is the wording <c>DoseFormatter</c> prints on the medicine label
/// ("twice daily"). Persisted by <c>Value</c> (ADR-022).</summary>
public sealed class DoseFrequency(int value, string displayName, string label) : Enumeration(value, displayName)
{
    public static readonly DoseFrequency Once = new(1, nameof(Once), "once");
    public static readonly DoseFrequency OnceDaily = new(2, nameof(OnceDaily), "once daily");
    public static readonly DoseFrequency TwiceDaily = new(3, nameof(TwiceDaily), "twice daily");
    public static readonly DoseFrequency ThreeTimesDaily = new(4, nameof(ThreeTimesDaily), "three times daily");
    public static readonly DoseFrequency FourTimesDaily = new(5, nameof(FourTimesDaily), "four times daily");
    public static readonly DoseFrequency EveryOtherDay = new(6, nameof(EveryOtherDay), "every other day");
    public static readonly DoseFrequency Weekly = new(7, nameof(Weekly), "once weekly");
    public static readonly DoseFrequency AsNeeded = new(8, nameof(AsNeeded), "as needed");

    public string Label { get; } = label;
}
