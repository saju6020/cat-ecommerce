using CatEcommerce.Domain.Events.Administration;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Constants;
using Platform.Infrastructure.Core.Domain;

namespace CatEcommerce.Domain.Aggregates.Administration;

/// <summary>HO-ADM-001 §8.3 / ADR-046. Id is the staff account id -- 1:1, created on first write.</summary>
public sealed class StaffBranchAssignmentAggregate : AggregateRoot
{
    private readonly List<Guid> _branchIds = [];

    public IReadOnlyList<Guid> BranchIds => _branchIds;
    public Guid PrimaryBranchId { get; private set; }

    private StaffBranchAssignmentAggregate() { }

    public StaffBranchAssignmentAggregate(Guid id) : base(id) { }

    public void Assign(StaffBranchAssignDto dto)
    {
        if (dto.BranchIds.Count == 0)
            throw new DomainRuleViolationException(BusinessViolationCodes.BranchNotFound, "At least one branch is required.", nameof(dto.BranchIds));

        if (!dto.BranchIds.Contains(dto.PrimaryBranchId))
            throw new DomainRuleViolationException(BusinessViolationCodes.BranchNotFound, "The primary branch must be one of the assigned branches.", nameof(dto.PrimaryBranchId));

        _branchIds.Clear();
        _branchIds.AddRange(dto.BranchIds.Distinct());
        PrimaryBranchId = dto.PrimaryBranchId;

        SetDefaultValue(dto.UserContext);

        AddDomainEvent(new StaffBranchesAssignedEvent { StaffAccountId = Id, CorrelationId = dto.CorrelationId, UserContext = dto.UserContext });
        UpdateVersion();
    }
}
