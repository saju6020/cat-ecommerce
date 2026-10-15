using System.Reflection;
using CatEcommerce.Domain.Events.Inventory;
using CatEcommerce.Infrastructure.Inventory;
using CatEcommerce.Shared.Common.Enums;
using CatEcommerce.Shared.Common.Helpers;
using Platform.Infrastructure.Core.Events;

namespace CatEcommerce.Infrastructure.Tests.Persistence;

/// <summary>HO-IMP-001 section 15 U-X1 and U-X2 -- the inventory events carry no free text (an event is published to subscribers, so personal data never rides on one), and the
/// hand-written migration SQL takes its numbers from the enumerations and its expiry predicate from <see cref="BatchExpiryRules"/>.</summary>
public sealed class InventorySqlAndEventTests
{
    [Fact]
    public void U_X1_NoInventoryEventHasAStringProperty()
    {
        var events = typeof(StockLocationCreatedEvent).Assembly.GetTypes()
            .Where(t => t.Namespace == typeof(StockLocationCreatedEvent).Namespace && typeof(Event).IsAssignableFrom(t) && !t.IsAbstract).ToList();

        Assert.NotEmpty(events);
        foreach (var type in events)
        {
            var strings = type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly).Where(p => p.PropertyType == typeof(string)).Select(p => p.Name).ToList();
            Assert.True(strings.Count == 0, $"{type.Name} has string properties: {string.Join(", ", strings)}");
        }
    }

    [Fact]
    public void U_X2_TheSellableViewAndTriggersUseTheEnumerationValues_AndTheExpiryPredicateIsStrictlyGreaterThan()
    {
        var view = InventoryLedgerSql.CreateSellableStockView();
        Assert.Contains($"b.status = {BatchStatus.Available.Value}", view);
        Assert.Contains($"{StockLocationKind.ShopFloor.Value}, {StockLocationKind.Warehouse.Value}", view);
        Assert.Contains($"::date {BatchExpiryRules.SqlComparison} b.expires_on", view);

        var usable = InventoryLedgerSql.BatchUsableFunctionAndLedgerTrigger();
        Assert.Contains($"{BatchStatus.Available.Value}", usable);
        Assert.Contains(BatchExpiryRules.SqlComparison, usable);
        Assert.DoesNotContain(">=", usable.Replace("quantity_delta", string.Empty).Replace(">= 0", string.Empty), StringComparison.Ordinal);

        var apply = InventoryLedgerSql.ApplyLedgerFunctionAndTrigger();
        Assert.Contains($"{MovementKind.Sale.Value}", InventoryLedgerSql.AddLedgerSignCheck() + apply);
    }
}
