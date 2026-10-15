using System.Globalization;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Domain.DomainServices;

/// <summary>One refusal of a report request: a violation code, its plain-language message and the property it names.</summary>
public sealed record ReportViolation(string Code, string Message, string Property);

/// <summary>A request that passed <see cref="ReportFilterValidator"/>: the parameters typed (enum and guid as text/guid, integer as int, boolean as bool) and defaults filled in.</summary>
public sealed record ValidatedReportFilters(
    DateOnly From, DateOnly To, TimeBucket? GroupBy, IReadOnlyDictionary<string, object> Parameters);

/// <summary>
/// HO-RPT-001 section 8.2 / 12.6 (U-A4) -- the range and parameter rules of every report, <b>pure</b>. FR-RPT-1 makes the date range part of the pipeline, so no report can be missing it.
/// </summary>
public static class ReportFilterValidator
{
    private static readonly HashSet<string> ReservedNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "from", "to", "branchId", "groupBy", "sort", "pageNumber", "pageSize"
    };

    public static bool IsReserved(string name) => ReservedNames.Contains(name);

    public static (ValidatedReportFilters? Filters, ReportViolation? Violation) Validate(
        ReportDefinition definition, DateOnly? from, DateOnly? to, string? groupBy, IReadOnlyDictionary<string, string> rawParameters)
    {
        if (from is null || to is null)
            return (null, new ReportViolation(BusinessViolationCodes.ReportRangeRequired, ReportingMessages.ReportRangeRequired, "from"));

        if (to < from)
            return (null, new ReportViolation(BusinessViolationCodes.ReportRangeInverted, ReportingMessages.ReportRangeInverted, "to"));

        // Inclusive: a 366-day report accepts from..from+365, and refuses from..from+366.
        var days = to.Value.DayNumber - from.Value.DayNumber + 1;
        if (days > definition.MaxRangeDays)
            return (null, new ReportViolation(BusinessViolationCodes.ReportRangeTooLong, ReportingMessages.ReportRangeTooLong(definition.MaxRangeDays), "to"));

        TimeBucket? bucket = null;
        if (!string.IsNullOrWhiteSpace(groupBy))
        {
            if (!definition.TimeBucketed)
                return (null, new ReportViolation(BusinessViolationCodes.UnknownReportParameter, ReportingMessages.UnknownReportParameter("groupBy"), "groupBy"));

            bucket = Enumeration.GetAll<TimeBucket>().FirstOrDefault(b => string.Equals(b.DisplayName, groupBy.Trim(), StringComparison.OrdinalIgnoreCase));
            if (bucket is null)
                return (null, new ReportViolation(BusinessViolationCodes.InvalidReportParameter, ReportingMessages.InvalidReportParameter("groupBy"), "groupBy"));
        }
        else if (definition.TimeBucketed)
        {
            bucket = TimeBucket.Day;
        }

        var typed = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        foreach (var (name, _) in rawParameters)
        {
            if (definition.FindParameter(name) is null)
                return (null, new ReportViolation(BusinessViolationCodes.UnknownReportParameter, ReportingMessages.UnknownReportParameter(name), name));
        }

        foreach (var parameter in definition.Parameters)
        {
            var raw = rawParameters.FirstOrDefault(p => string.Equals(p.Key, parameter.Name, StringComparison.OrdinalIgnoreCase)).Value;
            var text = string.IsNullOrWhiteSpace(raw) ? parameter.Default : raw.Trim();

            var parsed = Parse(parameter, text);
            if (parsed is null)
                return (null, new ReportViolation(BusinessViolationCodes.InvalidReportParameter, ReportingMessages.InvalidReportParameter(parameter.Name), parameter.Name));

            typed[parameter.Name] = parsed;
        }

        return (new ValidatedReportFilters(from.Value, to.Value, bucket, typed), null);
    }

    private static object? Parse(ReportParameter parameter, string text)
    {
        if (parameter.Type == ReportParameterType.Boolean)
            return bool.TryParse(text, out var flag) ? flag : null;

        if (parameter.Type == ReportParameterType.Guid)
            return Guid.TryParse(text, out var id) ? id : null;

        if (parameter.Type == ReportParameterType.Integer)
        {
            if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number)) return null;
            if (parameter.Min is { } min && number < min) return null;
            if (parameter.Max is { } max && number > max) return null;
            return number;
        }

        // Enum: the canonical spelling of an allowed value.
        return parameter.AllowedValues?.FirstOrDefault(v => string.Equals(v, text, StringComparison.OrdinalIgnoreCase));
    }
}
