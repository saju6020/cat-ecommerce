namespace CatEcommerce.Shared.Common.Enums;

// HO-BRD-001 §10.1 -- care profile, tasks and observations.

public sealed class FoodSource(int value, string displayName) : Enumeration(value, displayName)
{
    public static readonly FoodSource OwnFood = new(1, nameof(OwnFood));
    public static readonly FoodSource HouseFood = new(2, nameof(HouseFood));
}

public sealed class LitterType(int value, string displayName) : Enumeration(value, displayName)
{
    public static readonly LitterType Clumping = new(1, nameof(Clumping));
    public static readonly LitterType Silica = new(2, nameof(Silica));
    public static readonly LitterType Tofu = new(3, nameof(Tofu));
    public static readonly LitterType Bentonite = new(4, nameof(Bentonite));
    public static readonly LitterType Paper = new(5, nameof(Paper));
    public static readonly LitterType Other = new(6, nameof(Other));
}

public sealed class BelongingKind(int value, string displayName) : Enumeration(value, displayName)
{
    public static readonly BelongingKind Carrier = new(1, nameof(Carrier));
    public static readonly BelongingKind Bowl = new(2, nameof(Bowl));
    public static readonly BelongingKind Toy = new(3, nameof(Toy));
    public static readonly BelongingKind Bedding = new(4, nameof(Bedding));
    public static readonly BelongingKind Food = new(5, nameof(Food));
    public static readonly BelongingKind Medication = new(6, nameof(Medication));
    public static readonly BelongingKind Other = new(7, nameof(Other));
}

public sealed class SignatureMethod(int value, string displayName) : Enumeration(value, displayName)
{
    public static readonly SignatureMethod Drawn = new(1, nameof(Drawn));
    public static readonly SignatureMethod Typed = new(2, nameof(Typed));
}

public sealed class CareTaskKind(int value, string displayName) : Enumeration(value, displayName)
{
    public static readonly CareTaskKind Feed = new(1, nameof(Feed));
    public static readonly CareTaskKind Medication = new(2, nameof(Medication));
    public static readonly CareTaskKind LitterChange = new(3, nameof(LitterChange));
    public static readonly CareTaskKind Enrichment = new(4, nameof(Enrichment));
    public static readonly CareTaskKind ObservationCheck = new(5, nameof(ObservationCheck));
}

public sealed class CareTaskStatus(int value, string displayName) : Enumeration(value, displayName)
{
    public static readonly CareTaskStatus Pending = new(1, nameof(Pending));
    public static readonly CareTaskStatus Done = new(2, nameof(Done));
    public static readonly CareTaskStatus Skipped = new(3, nameof(Skipped));
    public static readonly CareTaskStatus Voided = new(4, nameof(Voided));
}

/// <summary>The three task kinds a branch's care routine schedules (litter, enrichment, observation checks).</summary>
public sealed class RoutineTaskKind(int value, string displayName) : Enumeration(value, displayName)
{
    public static readonly RoutineTaskKind LitterChange = new(1, nameof(LitterChange));
    public static readonly RoutineTaskKind Enrichment = new(2, nameof(Enrichment));
    public static readonly RoutineTaskKind ObservationCheck = new(3, nameof(ObservationCheck));
}

public sealed class PortionEaten(int value, string displayName) : Enumeration(value, displayName)
{
    public static readonly PortionEaten None = new(0, nameof(None));
    public static readonly PortionEaten Quarter = new(1, nameof(Quarter));
    public static readonly PortionEaten Half = new(2, nameof(Half));
    public static readonly PortionEaten ThreeQuarters = new(3, nameof(ThreeQuarters));
    public static readonly PortionEaten All = new(4, nameof(All));
}

public sealed class WaterIntake(int value, string displayName) : Enumeration(value, displayName)
{
    public static readonly WaterIntake None = new(1, nameof(None));
    public static readonly WaterIntake Low = new(2, nameof(Low));
    public static readonly WaterIntake Normal = new(3, nameof(Normal));
    public static readonly WaterIntake High = new(4, nameof(High));
}

public sealed class LitterOutput(int value, string displayName) : Enumeration(value, displayName)
{
    public static readonly LitterOutput Normal = new(1, nameof(Normal));
    public static readonly LitterOutput None = new(2, nameof(None));
    public static readonly LitterOutput Abnormal = new(3, nameof(Abnormal));
}

public sealed class CatMood(int value, string displayName) : Enumeration(value, displayName)
{
    public static readonly CatMood Relaxed = new(1, nameof(Relaxed));
    public static readonly CatMood Playful = new(2, nameof(Playful));
    public static readonly CatMood Anxious = new(3, nameof(Anxious));
    public static readonly CatMood Withdrawn = new(4, nameof(Withdrawn));
    public static readonly CatMood Aggressive = new(5, nameof(Aggressive));
}
