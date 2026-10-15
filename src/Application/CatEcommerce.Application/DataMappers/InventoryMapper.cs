using CatEcommerce.Application.Commands.Inventory;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Core.Commands;

namespace CatEcommerce.Application.DataMappers;

/// <summary>
/// HO-IMP-001 §11.3 -- static <c>ToDomainDto()</c> extensions for locations, storage, stock control, alerts, recall and breaches;
/// <see cref="ProcurementMapper"/> has suppliers, purchase orders, consignments and receipts. Every enumeration-typed command field is
/// parsed with <see cref="EnumerationParsing"/>: the command's own validator rejects an unrecognised value first
/// (<c>MustBeValidEnumeration</c>), so a throw here means a validator rule is missing, not bad input.
/// </summary>
public static class InventoryMapper
{
    public static InventoryActor ToActor(this Command command) => new(command.UserContext!, command.CorrelationId);

    public static StockLocationCreateDto ToDomainDto(this CreateStockLocationCommand c) =>
        new(c.BranchId, c.Code, c.Name, ParseEnum<StockLocationKind>(c.Kind), c.ToActor());

    public static StorageProfileDto ToDomainDto(this SetProductStorageProfileCommand c) =>
        new(ParseEnum<StorageCondition>(c.Condition), c.MinTemperatureC, c.MaxTemperatureC, c.HandlingNotes, c.ToActor());

    public static T ParseEnum<T>(string value) where T : Enumeration
    {
        if (!EnumerationParsing.TryParse<T>(value, out var result) || result is null)
            throw new InvalidOperationException($"'{value}' is not a recognised {typeof(T).Name}. The command's validator should have rejected it.");

        return result;
    }

    public static T? ParseNullableEnum<T>(string? value) where T : Enumeration =>
        string.IsNullOrWhiteSpace(value) ? null : ParseEnum<T>(value);
}
