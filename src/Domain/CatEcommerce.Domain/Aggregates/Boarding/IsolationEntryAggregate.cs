using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Shared.Common.Constants;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core.Domain;

namespace CatEcommerce.Domain.Aggregates.Boarding;

/// <summary>
/// HO-BRD-001 section 8.12 (US-BRD-18 AC2, ADR-227 as renumbered ADR-360) -- a member of staff going into (and leaving) an isolation unit, recorded so contact with a
/// suspected-infectious cat is traceable. Its OWN root; the exit is the only later change.
/// </summary>
public sealed class IsolationEntryAggregate : AggregateRoot
{
    public Guid BranchId { get; private set; }
    public Guid StayId { get; private set; }
    public Guid UnitId { get; private set; }
    public Guid StaffAccountId { get; private set; }
    public DateTimeOffset EnteredOn { get; private set; }
    public DateTimeOffset? LeftOn { get; private set; }
    public string Purpose { get; private set; } = default!;

    private IsolationEntryAggregate() { }

    public IsolationEntryAggregate(Guid id) : base(id) { }

    public void Enter(Guid branchId, Guid stayId, Guid unitId, DateTimeOffset now, string purpose, UserContext userContext)
    {
        if (string.IsNullOrWhiteSpace(purpose) || purpose.Trim().Length > IncidentRules.MaxIsolationPurposeLength)
            throw new DomainRuleViolationException(BusinessViolationCodes.NotInIsolation, "A purpose is required (up to 200 characters).", nameof(purpose));

        BranchId = branchId;
        StayId = stayId;
        UnitId = unitId;
        StaffAccountId = userContext.UserId;
        EnteredOn = now;
        Purpose = purpose.Trim();

        SetDefaultValue(userContext);
        UpdateVersion();
    }

    public void Exit(DateTimeOffset now)
    {
        if (LeftOn is not null) return;

        LeftOn = now;
        UpdateVersion();
    }
}
