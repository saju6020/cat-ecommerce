using System.Globalization;
using CatEcommerce.Application.Commands.Order;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Application.DataMappers;

/// <summary>HO-ORD-001 §11.3 -- command to domain DTO for delivery zones. Every enum-typed field was checked by the validator
/// (<c>MustBeValidEnumeration</c>); the time format is <c>HH:mm</c>, also checked there.</summary>
public static class DeliveryZoneMapper
{
    public static readonly string[] TimeFormats = ["HH:mm", "HH:mm:ss"];

    public static ZoneDto ToDomainDto(this CreateDeliveryZoneCommand c, string currencyCode) =>
        Build(c.BranchId, c.Name, c.Priority, c.IsActive, c.CodEnabled, c.CodMaxOrderValue, c.Coverage, c.Services, currencyCode, c);

    public static ZoneDto ToDomainDto(this UpdateDeliveryZoneCommand c, string currencyCode) =>
        Build(c.BranchId, c.Name, c.Priority, c.IsActive, c.CodEnabled, c.CodMaxOrderValue, c.Coverage, c.Services, currencyCode, c);

    private static ZoneDto Build(
        Guid branchId, string name, int priority, bool isActive, bool codEnabled, decimal? codMax,
        IReadOnlyList<CoverageInput> coverage, IReadOnlyList<ZoneServiceInput> services, string currencyCode, Platform.Infrastructure.Core.Commands.Command command) =>
        new(branchId, name, priority, isActive, codEnabled, codMax, currencyCode,
            coverage.Select(c => new CoverageDto(c.District, c.PostalCodes ?? [])).ToList(),
            services.Select(s => new ZoneServiceDto(
                ParseEnum<DeliveryServiceLevel>(s.ServiceLevel), s.IsActive, s.BaseCharge, s.FreeOverOrderValue, s.LeadTimeDays,
                (s.WeightBands ?? []).Select(b => new WeightBand(b.UpToGrams, b.Surcharge)).ToList(),
                (s.SlotWindows ?? []).Select(w => new SlotWindow(Enum.Parse<DayOfWeek>(w.DayOfWeek, ignoreCase: true), ParseTime(w.Start), ParseTime(w.End))).ToList())).ToList(),
            command.UserContext!, command.CorrelationId);

    public static T ParseEnum<T>(string value) where T : Enumeration =>
        EnumerationParsing.TryParse<T>(value, out var parsed) && parsed is not null
            ? parsed
            : throw new ArgumentException($"'{value}' is not a valid {typeof(T).Name}.", nameof(value));

    public static bool TryParseTime(string? value, out TimeOnly time) =>
        TimeOnly.TryParseExact(value, TimeFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out time);

    public static bool IsDayName(string? value) =>
        !string.IsNullOrWhiteSpace(value) && !int.TryParse(value, out _) && Enum.TryParse<DayOfWeek>(value, ignoreCase: true, out _);

    private static TimeOnly ParseTime(string value) =>
        TryParseTime(value, out var time) ? time : throw new ArgumentException($"'{value}' is not an HH:mm time.", nameof(value));
}
