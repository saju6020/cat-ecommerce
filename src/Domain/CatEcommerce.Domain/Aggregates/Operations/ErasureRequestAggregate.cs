using CatEcommerce.Domain.Entities;
using CatEcommerce.Domain.Events.Operations;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Core.Domain;

namespace CatEcommerce.Domain.Aggregates.Operations;

/// <summary>
/// HO-XCT-001 section 8.3 / ADR-302 -- the erasure ledger. One per customer account (<see cref="AggregateRoot.Id"/> == the customer account
/// id), one scope per registered <c>IPersonalDataEraser</c>. It finishes a deletion request across every context, including personal data
/// held inside retained records until their statutory period ends. It contains <b>no personal data</b> (a customer id that is already
/// anonymised, context keys, dates, codes), which is why it can be Regulatory and kept indefinitely: it is the proof the request was honoured.
/// </summary>
public sealed class ErasureRequestAggregate : AggregateRoot
{
    public const int MaxScopes = 32;

    private readonly List<ErasureScope> _scopes = [];

    public DateTimeOffset RequestedOn { get; private set; }
    public ErasureRequestSource Source { get; private set; } = default!;
    public ErasureRequestStatus Status { get; private set; } = default!;
    public DateTimeOffset? CompletedOn { get; private set; }
    public IReadOnlyList<ErasureScope> Scopes => _scopes;

    private ErasureRequestAggregate() { }

    public ErasureRequestAggregate(Guid customerAccountId) : base(customerAccountId) { }

    public void Open(ErasureOpenDto dto)
    {
        ValidateKeys(dto.ContextKeys, requireAtLeastOne: true);

        RequestedOn = dto.RequestedOn;
        Source = dto.Source;
        Status = ErasureRequestStatus.Open;
        CompletedOn = null;
        _scopes.Clear();
        _scopes.AddRange(dto.ContextKeys.Select(key => new ErasureScope(Guid.NewGuid(), key)));

        SetDefaultValue(dto.UserContext);

        AddDomainEvent(new ErasureRequestOpenedEvent
        {
            CustomerAccountId = Id, RequestSource = Source, CorrelationId = dto.CorrelationId, UserContext = dto.UserContext
        });

        UpdateVersion();
    }

    /// <summary>Adds a <c>Pending</c> scope for every key not yet present and reopens a <c>Completed</c> request. A no-op when there is
    /// nothing to add. Returns whether anything changed, so the caller saves only then.</summary>
    public bool AddMissingScopes(ErasureAddScopesDto dto)
    {
        ValidateKeys(dto.ContextKeys, requireAtLeastOne: false);

        var missing = dto.ContextKeys.Where(key => _scopes.All(s => s.ContextKey != key)).ToList();
        if (missing.Count == 0) return false;

        if (_scopes.Count + missing.Count > MaxScopes)
            throw Violation(BusinessViolationCodes.ErasureScopeNotFound, "Too many erasure scopes.", nameof(Scopes));

        _scopes.AddRange(missing.Select(key => new ErasureScope(Guid.NewGuid(), key)));
        if (Status == ErasureRequestStatus.Completed)
        {
            Status = ErasureRequestStatus.Open;
            CompletedOn = null;
        }

        AddDomainEvent(new ErasureScopesAddedEvent
        {
            CustomerAccountId = Id, ContextKeys = missing, CorrelationId = dto.CorrelationId, UserContext = dto.UserContext
        });

        UpdateVersion();
        return true;
    }

    /// <summary>Records one eraser's outcome. <c>Completed</c> stamps the scope; <c>Held(until)</c> needs <c>until</c> strictly after today;
    /// <c>Failed(code)</c> makes it <c>Failing</c> and counts the attempt. When every scope is <c>Completed</c> the request completes.</summary>
    public void RecordScopeOutcome(ErasureScopeOutcomeDto dto)
    {
        var scope = _scopes.FirstOrDefault(s => s.ContextKey == dto.ContextKey)
            ?? throw Violation(BusinessViolationCodes.ErasureScopeNotFound, "This erasure scope was not found.", nameof(dto.ContextKey));

        if (scope.Status == ErasureScopeStatus.Completed)
            throw Violation(BusinessViolationCodes.ErasureScopeNotFound, "This erasure scope is already completed.", nameof(dto.ContextKey));

        switch (dto.Outcome)
        {
            case ErasureOutcome.Completed:
                scope.Complete(dto.Now);
                AddDomainEvent(new ErasureScopeCompletedEvent
                {
                    CustomerAccountId = Id, ContextKey = scope.ContextKey, CorrelationId = dto.CorrelationId, UserContext = dto.UserContext
                });
                break;

            case ErasureOutcome.Held held:
                if (held.Until <= dto.Today)
                    throw Violation(BusinessViolationCodes.InvalidHoldDate, OperationsMessages.InvalidHoldDate, nameof(held.Until));
                scope.Hold(held.Until, held.Basis, dto.Now);
                AddDomainEvent(new ErasureScopeHeldEvent
                {
                    CustomerAccountId = Id, ContextKey = scope.ContextKey, HeldUntil = held.Until,
                    CorrelationId = dto.CorrelationId, UserContext = dto.UserContext
                });
                break;

            case ErasureOutcome.Failed failed:
                scope.Fail(failed.ErrorCode, dto.Now);
                AddDomainEvent(new ErasureScopeFailedEvent
                {
                    CustomerAccountId = Id, ContextKey = scope.ContextKey, ErrorCode = scope.LastErrorCode!,
                    CorrelationId = dto.CorrelationId, UserContext = dto.UserContext
                });
                break;

            default:
                throw new InvalidOperationException("Unknown erasure outcome.");
        }

        if (Status == ErasureRequestStatus.Open && _scopes.All(s => s.Status == ErasureScopeStatus.Completed))
        {
            Status = ErasureRequestStatus.Completed;
            CompletedOn = dto.Now;
            AddDomainEvent(new ErasureRequestCompletedEvent
            {
                CustomerAccountId = Id, CompletedOn = dto.Now, CorrelationId = dto.CorrelationId, UserContext = dto.UserContext
            });
        }

        UpdateVersion();
    }

    private static void ValidateKeys(IReadOnlyList<string> keys, bool requireAtLeastOne)
    {
        if (requireAtLeastOne && keys.Count == 0)
            throw Violation(BusinessViolationCodes.ErasureScopeNotFound, "An erasure request needs at least one context.", nameof(keys));

        if (keys.Any(k => string.IsNullOrWhiteSpace(k) || k.Length > ErasureScope.MaxContextKeyLength))
            throw Violation(BusinessViolationCodes.ErasureScopeNotFound, "A context key is empty or too long.", nameof(keys));

        if (keys.Distinct(StringComparer.Ordinal).Count() != keys.Count)
            throw Violation(BusinessViolationCodes.ErasureScopeNotFound, "Context keys must be distinct.", nameof(keys));
    }

    private static DomainRuleViolationException Violation(string code, string message, string property) => new(code, message, property);
}
