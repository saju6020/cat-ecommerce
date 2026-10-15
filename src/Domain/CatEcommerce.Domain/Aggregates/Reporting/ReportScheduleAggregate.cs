using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core.Domain;

namespace CatEcommerce.Domain.Aggregates.Reporting;

/// <summary>
/// HO-RPT-001 section 8.9 (US-RPT-06, ADR-287, ADR-288) -- a report that is generated and emailed on a schedule. Raises <b>no domain events</b>: nothing subscribes, and HO-ACC-001's
/// interceptor audits every change. The invariants that can be read from the schedule's own state live here; the ones that need another aggregate (is each recipient an active staff member who
/// holds the report's action and is assigned to the branch) are the handler's, because they read the staff account.
/// <para><c>NextRunOn</c> is an instant from <see cref="ReportScheduleCalculator"/>; it is null unless the schedule is <c>Active</c>. A resume or an update recomputes it from now -- there is no
/// catch-up for the time paused -- and a completed run recomputes it from the later of the due time and now, so any number of missed slots collapse to one.</para>
/// </summary>
public sealed class ReportScheduleAggregate : AggregateRoot
{
    private readonly List<Guid> _recipientIds = [];

    public string Name { get; private set; } = default!;
    public string ReportKey { get; private set; } = default!;

    /// <summary>A branch id, or null for all branches (the zone is then the main branch's).</summary>
    public Guid? BranchId { get; private set; }

    public ReportRelativePeriod Period { get; private set; } = default!;
    public IReadOnlyDictionary<string, string> Parameters { get; private set; } = new Dictionary<string, string>();
    public ReportFormat Format { get; private set; } = default!;
    public ReportFrequency Frequency { get; private set; } = default!;
    public DayOfWeek? DayOfWeek { get; private set; }
    public int? DayOfMonth { get; private set; }
    public bool LastDayOfMonth { get; private set; }
    public TimeOnly SendAtLocal { get; private set; }
    public IReadOnlyList<Guid> RecipientIds => _recipientIds;
    public ReportScheduleStatus Status { get; private set; } = default!;
    public DateTimeOffset? NextRunOn { get; private set; }
    public DateTimeOffset? LastRunOn { get; private set; }
    public DateTimeOffset? LastRunDueOn { get; private set; }

    private ReportScheduleAggregate() { }

    public ReportScheduleAggregate(Guid id) : base(id) { }

    public void Create(ReportScheduleDto dto)
    {
        Validate(dto);
        Apply(dto);

        Status = ReportScheduleStatus.Active;
        NextRunOn = Next(dto.Now, dto.Zone);

        SetDefaultValue(dto.UserContext);
        UpdateVersion();
    }

    public void Update(ReportScheduleDto dto)
    {
        EnsureNotCancelled();
        Validate(dto);
        Apply(dto);

        NextRunOn = Status == ReportScheduleStatus.Active ? Next(dto.Now, dto.Zone) : null;

        SetDefaultValue(dto.UserContext);
        UpdateVersion();
    }

    public void Pause(UserContext userContext)
    {
        EnsureNotCancelled();
        if (Status == ReportScheduleStatus.Paused) return;

        Status = ReportScheduleStatus.Paused;
        NextRunOn = null;

        SetDefaultValue(userContext);
        UpdateVersion();
    }

    /// <summary>Recomputes from <paramref name="now"/>: the time spent paused is never caught up (ADR-287).</summary>
    public void Resume(DateTimeOffset now, TimeZoneInfo zone, UserContext userContext)
    {
        EnsureNotCancelled();
        if (Status == ReportScheduleStatus.Active) return;

        Status = ReportScheduleStatus.Active;
        NextRunOn = Next(now, zone);

        SetDefaultValue(userContext);
        UpdateVersion();
    }

    public void Cancel(UserContext userContext)
    {
        if (Status == ReportScheduleStatus.Cancelled) return;

        Status = ReportScheduleStatus.Cancelled;
        NextRunOn = null;

        SetDefaultValue(userContext);
        UpdateVersion();
    }

    /// <summary>
    /// Idempotent: a due time already recorded changes nothing. Otherwise the slot is recorded and the next run is the first one after the <b>later</b> of the due time and now, so any number of
    /// slots missed during downtime collapse to the one that just ran (ADR-287).
    /// </summary>
    public void RecordRunCompleted(DateTimeOffset dueOn, DateTimeOffset now, TimeZoneInfo zone, UserContext userContext)
    {
        if (LastRunDueOn is { } last && last >= dueOn) return;

        LastRunDueOn = dueOn;
        LastRunOn = now;
        if (Status == ReportScheduleStatus.Active) NextRunOn = Next(dueOn > now ? dueOn : now, zone);

        SetDefaultValue(userContext);
        UpdateVersion();
    }

    public bool IsDue(DateTimeOffset now) => Status == ReportScheduleStatus.Active && NextRunOn is { } next && next <= now;

    private DateTimeOffset Next(DateTimeOffset after, TimeZoneInfo zone) =>
        ReportScheduleCalculator.NextAfter(Frequency, DayOfWeek, DayOfMonth, LastDayOfMonth, SendAtLocal, after, zone);

    private void Apply(ReportScheduleDto dto)
    {
        Name = dto.Name.Trim();
        ReportKey = dto.Definition.Key;
        BranchId = dto.BranchId;
        Period = dto.Period;
        Parameters = new Dictionary<string, string>(dto.Parameters, StringComparer.OrdinalIgnoreCase);
        Format = dto.Format;
        Frequency = dto.Frequency;
        DayOfWeek = dto.Frequency == ReportFrequency.Weekly ? dto.DayOfWeek : null;
        DayOfMonth = dto.Frequency == ReportFrequency.Monthly && !dto.LastDayOfMonth ? dto.DayOfMonth : null;
        LastDayOfMonth = dto.Frequency == ReportFrequency.Monthly && dto.LastDayOfMonth;
        SendAtLocal = dto.SendAtLocal;
        _recipientIds.Clear();
        _recipientIds.AddRange(dto.RecipientIds);
    }

    private void EnsureNotCancelled()
    {
        if (Status == ReportScheduleStatus.Cancelled)
            throw new DomainRuleViolationException(BusinessViolationCodes.ReportScheduleCancelled, ReportingMessages.ReportScheduleCancelled, nameof(Status));
    }

    private static void Validate(ReportScheduleDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Name) || dto.Name.Trim().Length > ReportScheduleRules.NameMaxLength)
            throw Invalid($"A schedule needs a name of up to {ReportScheduleRules.NameMaxLength} characters.", nameof(dto.Name));

        if (dto.Format != ReportFormat.Csv && dto.Format != ReportFormat.Pdf)
            throw new DomainRuleViolationException(BusinessViolationCodes.ReportFormatNotSupported, ReportingMessages.ReportFormatNotSupported, nameof(dto.Format));

        if (!ReportSchedulePeriods.IsCompatible(dto.Definition, dto.Period))
            throw new DomainRuleViolationException(BusinessViolationCodes.InvalidReportSchedulePeriod, ReportingMessages.InvalidReportSchedulePeriod(dto.Definition.Title), nameof(dto.Period));

        if (dto.SendAtLocal.Second != 0 || dto.SendAtLocal.Millisecond != 0 || dto.SendAtLocal.Minute % ReportScheduleRules.SendAtStepMinutes != 0)
            throw Invalid($"The send time must be on a {ReportScheduleRules.SendAtStepMinutes}-minute step.", nameof(dto.SendAtLocal));

        ValidateFrequency(dto);
        ValidateRecipients(dto.RecipientIds);
    }

    private static void ValidateFrequency(ReportScheduleDto dto)
    {
        if (dto.Frequency == ReportFrequency.Daily)
        {
            if (dto.DayOfWeek is not null || dto.DayOfMonth is not null || dto.LastDayOfMonth)
                throw Invalid("A daily schedule has no day of the week or month.", nameof(dto.Frequency));
        }
        else if (dto.Frequency == ReportFrequency.Weekly)
        {
            if (dto.DayOfWeek is null || dto.DayOfMonth is not null || dto.LastDayOfMonth)
                throw Invalid("A weekly schedule needs a day of the week.", nameof(dto.DayOfWeek));
        }
        else if (dto.Frequency == ReportFrequency.Monthly)
        {
            if (dto.DayOfWeek is not null || dto.LastDayOfMonth == (dto.DayOfMonth is not null))
                throw Invalid("A monthly schedule needs either a day of the month (1 to 28) or the last day of the month.", nameof(dto.DayOfMonth));

            if (dto.DayOfMonth is { } day && (day < 1 || day > ReportScheduleRules.MaxDayOfMonth))
                throw Invalid($"The day of the month must be between 1 and {ReportScheduleRules.MaxDayOfMonth}.", nameof(dto.DayOfMonth));
        }
        else
        {
            throw Invalid("Unknown frequency.", nameof(dto.Frequency));
        }
    }

    private static void ValidateRecipients(IReadOnlyList<Guid> recipients)
    {
        if (recipients.Count == 0)
            throw new DomainRuleViolationException(BusinessViolationCodes.ReportRecipientRequired, ReportingMessages.ReportRecipientRequired, "recipientIds");

        if (recipients.Count > ReportScheduleRules.MaxRecipients)
            throw new DomainRuleViolationException(BusinessViolationCodes.ReportRecipientLimitExceeded, ReportingMessages.ReportRecipientLimitExceeded(ReportScheduleRules.MaxRecipients), "recipientIds");

        if (recipients.Distinct().Count() != recipients.Count)
            throw Invalid("Each recipient can be listed once.", "recipientIds");
    }

    private static DomainRuleViolationException Invalid(string message, string property) =>
        new(BusinessViolationCodes.InvalidReportSchedule, message, property);
}
