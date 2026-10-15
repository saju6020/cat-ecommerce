using System.Text.Json;
using CatEcommerce.Domain.Aggregates.Account;
using CatEcommerce.Domain.Aggregates.Administration;
using CatEcommerce.Domain.Aggregates.Billing;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.Models;
using CatEcommerce.Domain.ValueObjects;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Domain;
using static CatEcommerce.Application.CommandHandlers.Billing.BillingHandlerSupport;

namespace CatEcommerce.Application.CommandHandlers.Billing;

/// <summary>The result of C8 / C9; <see cref="Replayed"/> is true when an <c>Idempotency-Key</c> found the invoice
/// already issued (ADR-187) -- the controller answers <c>200</c> instead of <c>201</c>.</summary>
public sealed record IssuedInvoiceResult(Guid InvoiceId, string Number, bool Replayed = false);

/// <summary>What <see cref="InvoiceIssuing"/> needs, gathered once by each issuing handler.</summary>
public sealed record InvoiceIssueDependencies(
    IAggregateRootRepository<InvoiceAggregate> Invoices,
    IAggregateRootRepository<BranchAggregate> Branches,
    IAggregateRootRepository<BillingProfileAggregate> Profiles,
    IAggregateRootRepository<CustomerAccountAggregate> Customers,
    IAggregateRootRepository<CustomerAddressBookAggregate> AddressBooks,
    IBillingPricingInputs Pricing,
    IDocumentNumbering Numbering,
    IBusinessSettings Settings,
    INotificationOutbox Outbox,
    INotificationLinkBuilder Links,
    TimeProvider Time);

/// <summary>
/// HO-BIL-001 §11.4 C8/C9 -- the issue sequence the two issuing handlers share, as a static helper over
/// repositories (not a service layer, ADR-015): profile and tax rates read at now, the financial year from the branch
/// zone and setting, the series row taken <b>tracked</b>, the invoice issued in memory, the message enqueued, and
/// everything committed in <b>one</b> save with the bounded numbering retry (ADR-179, ADR-184). The whole write
/// attempt -- enqueue included -- is the retried unit, so a collision never leaves a message carrying a number that
/// never existed.
/// </summary>
public static class InvoiceIssuing
{
    public static async Task<CommandResponse> IssueAsync(
        InvoiceIssueDependencies d, InvoiceAggregate invoice, bool isNew, Command command, string? idempotencyKey,
        Guid? pinnedTaxClassId = null, DateTimeOffset? pinnedTaxRateAsOf = null)
    {
        var branch = await d.Branches.GetByIdAsync(invoice.BranchId);
        if (branch is null)
            return Violation(BusinessViolationCodes.BranchNotFound, nameof(invoice.BranchId), "Branch not found.");

        var profile = await d.Profiles.GetByIdAsync(invoice.BranchId);
        if (profile is null)
            return Violation(BusinessViolationCodes.BillingProfileMissing, nameof(invoice.BranchId), BillingMessages.BillingProfileMissing);

        var now = d.Time.GetUtcNow();
        var zone = TimeZoneInfo.FindSystemTimeZoneById(branch.TimeZoneId);
        var issueDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, zone).DateTime);
        var financialYear = FinancialYearRules.For(issueDate, d.Settings.GetInt(SettingKeys.BillingFinancialYearStartMonth));
        var termsDays = d.Settings.GetInt(SettingKeys.BillingPaymentTermsDays, invoice.BranchId);

        var rates = await d.Pricing.GetTaxRatesAsync(invoice.Lines.Select(l => l.TaxClassId).Distinct().ToList(), now);
        if (pinnedTaxClassId is { } pinned && pinnedTaxRateAsOf is { } asOf && rates.ContainsKey(pinned))
        {
            var pinnedRates = new Dictionary<Guid, TaxRateInfo>(rates);
            pinnedRates[pinned] = (await d.Pricing.GetTaxRatesAsync([pinned], asOf))[pinned];
            rates = pinnedRates;
        }

        var billTo = await BuildBillToAsync(d, invoice.CustomerId);

        DocumentNumberSeriesAggregate series;
        try
        {
            series = await d.Numbering.EnsureSeriesAsync(invoice.BranchId, financialYear, BillingDocumentKind.Invoice);
        }
        catch (DomainRuleViolationException ex)
        {
            return Violation(ex.ErrorCode, ex.PropertyName, ex.Message);
        }

        var sequence = series.Take();
        var actor = new BillingActor(command.UserContext!, command.CorrelationId);
        try
        {
            invoice.Issue(new InvoiceIssueDto(
                DocumentNumberFormat.Format(BillingDocumentKind.Invoice, branch.Code, financialYear, sequence), financialYear, sequence, rates,
                BuildSeller(profile), billTo, now, branch.TimeZoneId, termsDays, issueDate, idempotencyKey, actor));
        }
        catch (DomainRuleViolationException ex)
        {
            return Violation(ex.ErrorCode, ex.PropertyName, ex.Message);
        }

        var customerIsActive = (await d.Customers.GetByIdAsync(invoice.CustomerId))?.Status == CustomerAccountStatus.Active;

        await d.Numbering.SaveWithRetryAsync(
            series,
            async () =>
            {
                if (customerIsActive) BillingNotifications.EnqueueInvoiceIssued(d.Outbox, d.Links, invoice, command.CorrelationId, command.UserContext!);

                if (isNew) await BillingPosting.CommitNewAsync(d.Invoices, invoice);
                else await BillingPosting.CommitAsync(d.Invoices, invoice);
            },
            collided => invoice.Renumber(DocumentNumberFormat.Format(BillingDocumentKind.Invoice, branch.Code, financialYear, collided), collided));

        return Success(new IssuedInvoiceResult(invoice.Id, invoice.Number!));
    }

    private static SellerSnapshot BuildSeller(BillingProfileAggregate profile)
    {
        var accounts = JsonSerializer.Serialize(profile.BankAccounts.Select(a => new
        {
            bankName = a.BankName, accountName = a.AccountName, accountNumber = a.AccountNumber, branchName = a.BranchName, routingNumber = a.RoutingNumber,
        }));

        return new SellerSnapshot(
            profile.LegalName, profile.TaxRegistrationNumber, string.Join("\n", profile.AddressLines), profile.Phone, profile.Email,
            profile.InvoiceFooter, accounts);
    }

    private static async Task<BillToSnapshot> BuildBillToAsync(InvoiceIssueDependencies d, Guid customerId)
    {
        var customer = await d.Customers.GetByIdAsync(customerId);
        var book = await d.AddressBooks.GetByIdAsync(customerId);
        var address = book is null ? null : (book.Addresses.FirstOrDefault(a => a.Id == book.DefaultAddressId) ?? book.Addresses.FirstOrDefault());

        var addressText = address is null ? null : string.Join(", ", new[] { address.Line1, address.Line2, address.City, address.District, address.PostalCode }
            .Where(part => !string.IsNullOrWhiteSpace(part)));

        return new BillToSnapshot(customer?.FullName ?? "Customer", customer?.MobileNumber?.Value, customer?.EmailAddress?.Value, addressText);
    }
}
