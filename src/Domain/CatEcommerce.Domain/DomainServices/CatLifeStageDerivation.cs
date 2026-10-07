using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Domain.DomainServices;

/// <summary>The derived life stage of a cat. <see cref="Stage"/> is null when the cat has no date
/// of birth (the recommendation then says so with an <c>AddDateOfBirth</c> hint).</summary>
public sealed record DerivedLifeStage(LifeStage? Stage, bool IsEstimated);

/// <summary>
/// HO-SHP-001 section 8.13 rule 2 -- pure. Under <paramref name="kittenMaxAgeMonths"/> months is a
/// <c>Kitten</c>; at or over <paramref name="seniorFromAgeYears"/> years is <c>Senior</c>; otherwise
/// <c>Adult</c>. An estimated date of birth is used, and flagged so the reason can say "about".
/// Shared by the recommendation blocks (US-SHP-08) and the food recommendations (US-SHP-11).
/// General guidance pending clinical sign-off (OQ-9).
/// </summary>
public static class CatLifeStageDerivation
{
    public static DerivedLifeStage Derive(
        DateOnly? dateOfBirth, DateOfBirthPrecision precision, DateOnly today, int kittenMaxAgeMonths, int seniorFromAgeYears)
    {
        if (dateOfBirth is null) return new DerivedLifeStage(null, false);

        var months = (today.Year - dateOfBirth.Value.Year) * 12 + (today.Month - dateOfBirth.Value.Month);
        if (today.Day < dateOfBirth.Value.Day) months--;
        if (months < 0) months = 0;

        var stage = months < kittenMaxAgeMonths ? LifeStage.Kitten
            : months >= seniorFromAgeYears * 12 ? LifeStage.Senior
            : LifeStage.Adult;

        return new DerivedLifeStage(stage, precision == DateOfBirthPrecision.Estimated);
    }
}
