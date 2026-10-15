using CatEcommerce.Domain.Aggregates.Billing;
using Microsoft.EntityFrameworkCore;

namespace CatEcommerce.Infrastructure.Persistence;

/// <summary>Billing (HO-BIL-001) DbSets -- one partial per area (ADR-341). The model is discovered
/// from the configurations; these properties are the typed entry points.</summary>
public sealed partial class CatEcommerceDbContext
{
    public DbSet<BillingProfileAggregate> BillingProfiles => Set<BillingProfileAggregate>();
    public DbSet<DocumentNumberSeriesAggregate> DocumentNumberSeries => Set<DocumentNumberSeriesAggregate>();
    public DbSet<ChargeAggregate> Charges => Set<ChargeAggregate>();
    public DbSet<InvoiceAggregate> Invoices => Set<InvoiceAggregate>();
    public DbSet<PaymentAggregate> Payments => Set<PaymentAggregate>();
    public DbSet<CreditNoteAggregate> CreditNotes => Set<CreditNoteAggregate>();
    public DbSet<RefundAggregate> Refunds => Set<RefundAggregate>();
    public DbSet<CashUpAggregate> CashUps => Set<CashUpAggregate>();
    public DbSet<CodRemittanceAggregate> CodRemittances => Set<CodRemittanceAggregate>();
    public DbSet<BankStatementAggregate> BankStatements => Set<BankStatementAggregate>();
    public DbSet<BankMatchAggregate> BankMatches => Set<BankMatchAggregate>();
    public DbSet<AccountingExportRunAggregate> AccountingExportRuns => Set<AccountingExportRunAggregate>();
    public DbSet<LoyaltyAccountAggregate> LoyaltyAccounts => Set<LoyaltyAccountAggregate>();
    public DbSet<LoyaltyEntryAggregate> LoyaltyEntries => Set<LoyaltyEntryAggregate>();
}
