using System.Text.RegularExpressions;
using CatEcommerce.Domain.Events.Inventory;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Core.Domain;

namespace CatEcommerce.Domain.Aggregates.Inventory;

/// <summary>
/// HO-IMP-001 §8.3 / ADR-109. A place stock is held, at a branch. <c>Code</c> is upper-case and unique per branch. What
/// may happen here is <c>StockLocationRules</c>' fixed table; the "no stock left" check on deactivation needs a read, so
/// the handler makes it (<c>StockLocationHasStock</c>).
/// </summary>
public sealed partial class StockLocationAggregate : AggregateRoot
{
    public Guid BranchId { get; private set; }
    public string Code { get; private set; } = default!;
    public string Name { get; private set; } = default!;
    public StockLocationKind Kind { get; private set; } = default!;
    public bool IsActive { get; private set; }

    private StockLocationAggregate() { }

    public StockLocationAggregate(Guid id) : base(id) { }

    public void Create(StockLocationCreateDto dto)
    {
        var code = InventoryViolation.Normalise(dto.Code).ToUpperInvariant();
        if (!CodePattern().IsMatch(code) || code.Length > InventoryRules.MaxLocationCodeLength)
            throw InventoryViolation.Of(BusinessViolationCodes.InvalidStockLocationCode, "A location code is 1-20 letters, digits, dashes or underscores.", nameof(Code));
        ValidateName(dto.Name);

        BranchId = dto.BranchId;
        Code = code;
        Name = dto.Name.Trim();
        Kind = dto.Kind;
        IsActive = true;

        SetDefaultValue(dto.Actor.UserContext);
        AddDomainEvent(new StockLocationCreatedEvent { LocationId = Id, BranchId = BranchId, Kind = Kind, CorrelationId = dto.Actor.CorrelationId, UserContext = dto.Actor.UserContext });
        UpdateVersion();
    }

    public void Rename(string name, InventoryActor actor)
    {
        ValidateName(name);
        Name = name.Trim();
        Raise(actor);
    }

    public void Deactivate(InventoryActor actor)
    {
        if (!IsActive) return;
        IsActive = false;
        Raise(actor);
    }

    public void Reactivate(InventoryActor actor)
    {
        if (IsActive) return;
        IsActive = true;
        Raise(actor);
    }

    private void Raise(InventoryActor actor)
    {
        AddDomainEvent(new StockLocationChangedEvent { LocationId = Id, IsActive = IsActive, CorrelationId = actor.CorrelationId, UserContext = actor.UserContext });
        UpdateVersion();
    }

    private static void ValidateName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 100)
            throw InventoryViolation.Of(BusinessViolationCodes.InvalidStockLocationCode, "A location name is required (at most 100 characters).", nameof(Name));
    }

    [GeneratedRegex("^[A-Z0-9_-]+$")]
    private static partial Regex CodePattern();
}
