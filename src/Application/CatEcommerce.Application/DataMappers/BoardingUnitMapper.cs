using CatEcommerce.Application.Commands.Boarding;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Application.DataMappers;

/// <summary>HO-BRD-001 section 11.3 -- static <c>ToDomainDto()</c> extensions for the units, maintenance, rate-card and routine commands. Every enum-typed
/// field was accepted by its validator (<c>MustBeValidEnumeration</c>) before a handler runs.</summary>
public static class BoardingUnitMapper
{
    public static T ParseEnum<T>(string value) where T : Enumeration =>
        EnumerationParsing.TryParse<T>(value, out var parsed) && parsed is not null
            ? parsed
            : throw new ArgumentException($"'{value}' is not a valid {typeof(T).Name}.", nameof(value));

    public static BoardingUnitCreateDto ToDomainDto(this CreateBoardingUnitCommand c) =>
        new(c.BranchId, c.Code, c.Name, ParseEnum<BoardingUnitType>(c.Type), c.CapacityCats, c.Block, c.SortOrder, c.UserContext!, c.CorrelationId);

    public static BoardingUnitUpdateDto ToDomainDto(this UpdateBoardingUnitCommand c, bool hasFutureAllocations) =>
        new(c.Name, ParseEnum<BoardingUnitType>(c.Type), c.CapacityCats, c.Block, c.SortOrder, hasFutureAllocations, c.UserContext!, c.CorrelationId);

    public static BoardingUnitActivationDto ToDomainDto(this SetBoardingUnitActivationCommand c, bool hasFutureAllocations) =>
        new(c.IsActive, hasFutureAllocations, c.UserContext!, c.CorrelationId);

    public static BoardingRateCardCreateDto ToDomainDto(this SetBoardingRateCardCommand c, string currencyCode, DateTimeOffset now) =>
        new(c.BranchId, ParseEnum<BoardingUnitType>(c.UnitType), c.NightlyRate, c.WeeklyRate, c.MonthlyRate, c.AdditionalCatNightlyRate,
            c.TaxClassId, currencyCode, c.EffectiveFrom, now, c.UserContext!.UserId, c.UserContext!, c.CorrelationId);

    public static BoardingCareRoutineDto ToDomainDto(this SetBoardingCareRoutineCommand c) =>
        new(c.BranchId, c.Times.Select(t => new RoutineTimeInput(ParseEnum<RoutineTaskKind>(t.Kind), t.Time)).ToList(), c.UserContext!, c.CorrelationId);
}
