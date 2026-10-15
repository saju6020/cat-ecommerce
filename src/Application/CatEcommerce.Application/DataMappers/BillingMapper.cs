using CatEcommerce.Application.Commands.Billing;
using CatEcommerce.Domain.Aggregates.Billing;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Core.Commands;

namespace CatEcommerce.Application.DataMappers;

/// <summary>HO-BIL-001 §11.3 -- static <c>ToDomainDto()</c> extensions; one per command that needs a DTO.
/// The actor (who, and the request's correlation id) travels on every Billing mutation so the domain
/// event it raises names its actor.</summary>
public static class BillingMapper
{
    public static BillingActor ToBillingActor(this Command command) => new(command.UserContext!, command.CorrelationId);

    /// <summary>Case-insensitive, by display name. Every enum-typed command field is checked by its validator
    /// (<c>MustBeValidEnumeration</c>) before a handler runs, so this only throws for a value that bypassed validation.</summary>
    public static T ParseEnum<T>(string value) where T : Enumeration =>
        EnumerationParsing.TryParse<T>(value, out var parsed) && parsed is not null
            ? parsed
            : throw new ArgumentException($"'{value}' is not a valid {typeof(T).Name}.", nameof(value));

    /// <summary>A charge becomes an invoice line as a snapshot -- the charge is never re-priced (HO-ADM-001 ADR-049).</summary>
    public static DraftInvoiceLineInput ToDraftLine(this ChargeAggregate charge) =>
        new(charge.Id, null, charge.BusinessLine, charge.ItemKind, charge.SourceKind, charge.SourceId, charge.BillingGroupKind, charge.BillingGroupId,
            charge.CatId, charge.Description, charge.Quantity, charge.UnitPriceAmount, charge.LineDiscountAmount, charge.TaxClassId);

    public static BillingProfileSetDto ToDomainDto(this SetBillingProfileCommand c) =>
        new(c.LegalName, c.TaxRegistrationNumber, c.AddressLines, c.Phone, c.Email, c.InvoiceFooter, c.BankAccounts, c.ToBillingActor());
}
