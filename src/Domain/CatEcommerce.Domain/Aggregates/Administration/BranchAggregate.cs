using CatEcommerce.Domain.Entities;
using CatEcommerce.Domain.Events.Administration;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Constants;
using Platform.Infrastructure.Core.Domain;

namespace CatEcommerce.Domain.Aggregates.Administration;

/// <summary>
/// HO-ADM-001 §8.2. A day with no interval is closed. A new branch is closed until hours are set
/// (safe default). Code is immutable once created (§8.2) and normalised to upper-case here so the
/// "upper-case, unique" invariant is guaranteed rather than merely requested.
/// </summary>
public sealed class BranchAggregate : AggregateRoot
{
    private readonly List<OpeningInterval> _weeklyHours = [];

    public string Code { get; private set; } = default!;
    public string Name { get; private set; } = default!;
    public string TimeZoneId { get; private set; } = default!;
    public string? AddressLine { get; private set; }
    public string? PhoneNumber { get; private set; }
    public string? Email { get; private set; }
    public bool IsActive { get; private set; }
    public IReadOnlyList<OpeningInterval> WeeklyHours => _weeklyHours;

    private BranchAggregate() { }

    public BranchAggregate(Guid id) : base(id) { }

    public void Create(BranchCreateDto dto)
    {
        ValidateName(dto.Name);
        ValidateTimeZone(dto.TimeZoneId);
        ValidateContact(dto.AddressLine, dto.PhoneNumber, dto.Email);

        Code = NormaliseCode(dto.Code);
        Name = dto.Name;
        TimeZoneId = dto.TimeZoneId;
        AddressLine = dto.AddressLine;
        PhoneNumber = dto.PhoneNumber;
        Email = dto.Email;
        IsActive = true;
        _weeklyHours.Clear();

        SetDefaultValue(dto.UserContext);

        AddDomainEvent(new BranchCreatedEvent { BranchId = Id, CorrelationId = dto.CorrelationId, UserContext = dto.UserContext });
        UpdateVersion();
    }

    public void UpdateDetails(BranchDetailsUpdateDto dto)
    {
        ValidateName(dto.Name);
        ValidateTimeZone(dto.TimeZoneId);
        ValidateContact(dto.AddressLine, dto.PhoneNumber, dto.Email);

        Name = dto.Name;
        TimeZoneId = dto.TimeZoneId;
        AddressLine = dto.AddressLine;
        PhoneNumber = dto.PhoneNumber;
        Email = dto.Email;

        AddDomainEvent(new BranchDetailsUpdatedEvent { BranchId = Id, CorrelationId = dto.CorrelationId, UserContext = dto.UserContext });
        UpdateVersion();
    }

    /// <summary>Replaces the whole week -- the screen edits the whole week (§8.2).</summary>
    public void SetWeeklyHours(BranchWeeklyHoursDto dto)
    {
        if (dto.Intervals.Count > BranchRules.MaxIntervalsPerDay * 7)
            throw Invalid(nameof(dto.Intervals));

        foreach (var group in dto.Intervals.GroupBy(i => i.DayOfWeek))
        {
            if (group.Count() > BranchRules.MaxIntervalsPerDay) throw Invalid(nameof(dto.Intervals));

            var ordered = group.OrderBy(i => i.Opens).ToList();
            for (var i = 0; i < ordered.Count; i++)
            {
                if (ordered[i].Opens >= ordered[i].Closes) throw Invalid(nameof(dto.Intervals));

                if (i > 0 && ordered[i].Opens < ordered[i - 1].Closes) throw Invalid(nameof(dto.Intervals));
            }
        }

        _weeklyHours.Clear();
        foreach (var interval in dto.Intervals.OrderBy(i => i.DayOfWeek).ThenBy(i => i.Opens))
            _weeklyHours.Add(new OpeningInterval(Guid.NewGuid(), interval.DayOfWeek, interval.Opens, interval.Closes));

        AddDomainEvent(new BranchWeeklyHoursChangedEvent { BranchId = Id, CorrelationId = dto.CorrelationId, UserContext = dto.UserContext });
        UpdateVersion();
    }

    /// <summary>The "last active branch" rule is a handler-level, cross-record check (§11.4) --
    /// it needs to count other branches, which this aggregate cannot see.</summary>
    public void SetActivation(BranchActivationDto dto)
    {
        if (IsActive == dto.IsActive) return;

        IsActive = dto.IsActive;

        AddDomainEvent(new BranchActivationChangedEvent { BranchId = Id, IsActive = IsActive, CorrelationId = dto.CorrelationId, UserContext = dto.UserContext });
        UpdateVersion();
    }

    private static string NormaliseCode(string code) => code.Trim().ToUpperInvariant();

    private static void ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > BranchRules.NameMaxLength)
            throw Invalid(nameof(Name));
    }

    private static void ValidateTimeZone(string timeZoneId)
    {
        try
        {
            TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException or ArgumentNullException)
        {
            throw new DomainRuleViolationException(BusinessViolationCodes.InvalidTimeZone, $"'{timeZoneId}' is not a known time zone.", nameof(TimeZoneId));
        }
    }

    private static void ValidateContact(string? addressLine, string? phoneNumber, string? email)
    {
        if (addressLine is not null && addressLine.Length > BranchRules.AddressMaxLength) throw Invalid(nameof(AddressLine));
        if (phoneNumber is not null && phoneNumber.Length > BranchRules.PhoneMaxLength) throw Invalid(nameof(PhoneNumber));
        if (email is not null && email.Length > 256) throw Invalid(nameof(Email));
    }

    private static DomainRuleViolationException Invalid(string property) =>
        new(BusinessViolationCodes.InvalidOpeningInterval, "This branch data is invalid.", property);
}
