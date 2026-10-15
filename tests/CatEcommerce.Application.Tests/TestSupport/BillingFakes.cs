using CatEcommerce.Application.CommandHandlers.Billing;
using CatEcommerce.Domain.Aggregates.Billing;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Models;
using CatEcommerce.Domain.ValueObjects;
using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Application.Tests.TestSupport;

/// <summary>Hand-written test doubles for the Billing ports (no mocking library is in scope). Each one records
/// what it was asked so a handler test can assert on it.</summary>
public sealed class FakeBillingPricingInputs : IBillingPricingInputs
{
    public Dictionary<Guid, TaxRateInfo> TaxClasses { get; } = [];
    public Dictionary<Guid, ServicePriceInfo> Services { get; } = [];

    public void AddTaxClass(Guid id, decimal? rate = 15m, string code = "STD") => TaxClasses[id] = new TaxRateInfo(id, code, rate);

    public Task<IReadOnlyDictionary<Guid, TaxRateInfo>> GetTaxRatesAsync(IReadOnlyCollection<Guid> taxClassIds, DateTimeOffset at, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyDictionary<Guid, TaxRateInfo>>(taxClassIds.Where(TaxClasses.ContainsKey).Distinct().ToDictionary(id => id, id => TaxClasses[id]));

    public Task<ServicePriceInfo?> GetServicePriceAsync(Guid serviceId, DateTimeOffset at, CancellationToken ct = default) =>
        Task.FromResult(Services.GetValueOrDefault(serviceId));

    public Task<Guid?> GetServiceTaxClassIdAsync(Guid serviceId, CancellationToken ct = default) =>
        Task.FromResult<Guid?>(Services.TryGetValue(serviceId, out var s) ? s.TaxClassId : null);

    public Dictionary<Guid, Guid> SkuTaxClasses { get; } = [];

    public Task<IReadOnlyDictionary<Guid, Guid>> GetSkuTaxClassIdsAsync(IReadOnlyCollection<Guid> skuIds, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyDictionary<Guid, Guid>>(skuIds.Where(SkuTaxClasses.ContainsKey).Distinct().ToDictionary(id => id, id => SkuTaxClasses[id]));
}

public sealed class FakeBillableItemSource(ChargeSourceKind kind, BillableSourceResult result) : IBillableItemSource
{
    public ChargeSourceKind Kind => kind;
    public int Calls { get; private set; }

    public Task<BillableSourceResult> GetItemsAsync(Guid sourceId, CancellationToken ct = default)
    {
        Calls++;
        return Task.FromResult(result);
    }
}

public sealed class FakeBillableItemSourceResolver(params IBillableItemSource[] sources) : IBillableItemSourceResolver
{
    public IBillableItemSource? Resolve(ChargeSourceKind kind) => sources.FirstOrDefault(s => s.Kind == kind);
}

public sealed class FakeNotificationOutbox : INotificationOutbox
{
    public List<NotificationRequestDto> Requests { get; } = [];

    public void Enqueue(NotificationRequestDto request) => Requests.Add(request);
}

public static class BillingTestSettings
{
    /// <summary>A settings source returning the Billing defaults of HO-BIL-001 §10.2 plus BDT.</summary>
    public static CatEcommerce.Infrastructure.Fakes.Administration.InMemoryBusinessSettings Defaults(Dictionary<string, object>? overrides = null)
    {
        var values = new Dictionary<string, object>
        {
            [CatEcommerce.Shared.Common.Constants.SettingKeys.CommerceCurrencyCode] = "BDT",
            [CatEcommerce.Shared.Common.Constants.SettingKeys.BillingFinancialYearStartMonth] = 7,
            [CatEcommerce.Shared.Common.Constants.SettingKeys.BillingPaymentTermsDays] = 0,
            [CatEcommerce.Shared.Common.Constants.SettingKeys.BillingPaymentApprovalThreshold] = 50_000m,
            [CatEcommerce.Shared.Common.Constants.SettingKeys.BillingBankMatchDateToleranceDays] = 3,
            [CatEcommerce.Shared.Common.Constants.SettingKeys.BillingCodRemittanceDueDays] = 7,
            [CatEcommerce.Shared.Common.Constants.SettingKeys.BillingLoyaltyEnabled] = false,
            [CatEcommerce.Shared.Common.Constants.SettingKeys.BillingLoyaltySpendPerPoint] = 100m,
            [CatEcommerce.Shared.Common.Constants.SettingKeys.BillingLoyaltyPointValue] = 1m,
            [CatEcommerce.Shared.Common.Constants.SettingKeys.BillingLoyaltyMinimumRedemptionPoints] = 100,
        };
        if (overrides is not null) foreach (var (key, value) in overrides) values[key] = value;
        return new CatEcommerce.Infrastructure.Fakes.Administration.InMemoryBusinessSettings(values);
    }
}

/// <summary>Records what a handler asked and answers from plain lists -- the cross-record lookups of <see cref="IBillingFinder"/>.</summary>
public sealed class FakeBillingFinder : IBillingFinder
{
    public HashSet<Guid> BilledChargeIds { get; } = [];
    public List<Guid> UnbilledChargeIds { get; } = [];
    public Dictionary<(Guid CallerId, string Key), IssuedInvoiceRef> IdempotentInvoices { get; } = [];
    public List<Guid> MovableChargeIds { get; } = [];
    public List<Guid> DraftInvoiceIds { get; } = [];
    public UnbilledChargeCriteria? LastCriteria { get; private set; }

    public Task<IReadOnlySet<Guid>> GetBilledChargeIdsAsync(IReadOnlyCollection<Guid> chargeIds, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlySet<Guid>>(chargeIds.Where(BilledChargeIds.Contains).ToHashSet());

    public Task<IReadOnlyList<Guid>> FindUnbilledChargeIdsAsync(UnbilledChargeCriteria criteria, CancellationToken ct = default)
    {
        LastCriteria = criteria;
        return Task.FromResult<IReadOnlyList<Guid>>(UnbilledChargeIds.ToList());
    }

    public Task<IssuedInvoiceRef?> FindInvoiceByIdempotencyKeyAsync(Guid issuedBy, string key, CancellationToken ct = default) =>
        Task.FromResult(IdempotentInvoices.GetValueOrDefault((issuedBy, key)));

    public Dictionary<(ChargeSourceKind Kind, Guid SourceId), List<Guid>> ChargesBySource { get; } = [];

    public Task<IReadOnlyList<Guid>> FindChargeIdsBySourceAsync(ChargeSourceKind kind, Guid sourceId, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<Guid>>(ChargesBySource.TryGetValue((kind, sourceId), out var ids) ? ids.ToList() : []);

    public Task<IReadOnlyList<Guid>> FindMovableChargeIdsAsync(Guid customerId, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<Guid>>(MovableChargeIds.ToList());

    public Task<IReadOnlyList<Guid>> FindDraftInvoiceIdsAsync(Guid customerId, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<Guid>>(DraftInvoiceIds.ToList());

    public Dictionary<(Guid CallerId, string Key), RecordedPaymentRef> IdempotentPayments { get; } = [];
    private long _receiptNumber;

    public Task<RecordedPaymentRef?> FindPaymentByIdempotencyKeyAsync(Guid recordedBy, string key, CancellationToken ct = default) =>
        Task.FromResult(IdempotentPayments.GetValueOrDefault((recordedBy, key)));

    public Task<long> NextReceiptNumberAsync(CancellationToken ct = default) => Task.FromResult(++_receiptNumber);

    public Dictionary<Guid, CreditedSoFar> CreditedSoFar { get; } = [];
    public List<Guid> PaymentsNewestFirst { get; } = [];
    public Dictionary<(Guid CallerId, string Key), IssuedCreditNoteRef> IdempotentCreditNotes { get; } = [];
    public Dictionary<(Guid CallerId, string Key), IssuedRefundRef> IdempotentRefunds { get; } = [];
    public Guid? CorrectingInvoiceId { get; set; }

    public Task<IReadOnlyDictionary<Guid, CreditedSoFar>> GetCreditedSoFarAsync(Guid invoiceId, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyDictionary<Guid, CreditedSoFar>>(CreditedSoFar);

    public Task<IReadOnlyList<Guid>> FindPaymentIdsAllocatedToInvoiceAsync(Guid invoiceId, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<Guid>>(PaymentsNewestFirst.ToList());

    public Task<IssuedCreditNoteRef?> FindCreditNoteByIdempotencyKeyAsync(Guid issuedBy, string key, CancellationToken ct = default) =>
        Task.FromResult(IdempotentCreditNotes.GetValueOrDefault((issuedBy, key)));

    public Task<IssuedRefundRef?> FindRefundByIdempotencyKeyAsync(Guid issuedBy, string key, CancellationToken ct = default) =>
        Task.FromResult(IdempotentRefunds.GetValueOrDefault((issuedBy, key)));

    public Task<Guid?> FindCorrectingInvoiceIdAsync(Guid creditNoteId, CancellationToken ct = default) => Task.FromResult(CorrectingInvoiceId);

    public List<CashUpRow> CashUpRowsOfTheDay { get; } = [];
    public int PendingApprovals { get; set; }

    public Task<IReadOnlyList<CashUpRow>> GetCashUpRowsAsync(Guid branchId, DateOnly businessDate, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<CashUpRow>>(CashUpRowsOfTheDay.ToList());

    public Task<int> CountPendingApprovalsAsync(Guid branchId, DateOnly businessDate, string timeZoneId, CancellationToken ct = default) => Task.FromResult(PendingApprovals);

    public Dictionary<Guid, decimal> RemittedTotals { get; } = [];
    public HashSet<(string Label, string Sha)> ImportedStatements { get; } = [];
    public List<TransferPaymentCandidate> UnmatchedTransfers { get; } = [];
    public HashSet<Guid> MatchedLineIds { get; } = [];
    public HashSet<Guid> MatchedPaymentIds { get; } = [];

    public Task<IReadOnlyDictionary<Guid, decimal>> GetRemittedTotalsAsync(IReadOnlyCollection<Guid> paymentIds, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyDictionary<Guid, decimal>>(RemittedTotals.Where(kv => paymentIds.Contains(kv.Key)).ToDictionary(kv => kv.Key, kv => kv.Value));

    public Task<bool> StatementExistsAsync(string bankAccountLabel, string sha256, CancellationToken ct = default) =>
        Task.FromResult(ImportedStatements.Contains((bankAccountLabel, sha256)));

    public Task<IReadOnlyList<TransferPaymentCandidate>> FindUnmatchedTransferPaymentsAsync(Guid branchId, DateOnly from, DateOnly to, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<TransferPaymentCandidate>>(UnmatchedTransfers.Where(p => p.ReceivedOn >= from && p.ReceivedOn <= to).ToList());

    public Task<IReadOnlySet<Guid>> GetActivelyMatchedLineIdsAsync(Guid statementId, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlySet<Guid>>(MatchedLineIds.ToHashSet());

    public Task<bool> IsPaymentActivelyMatchedAsync(Guid paymentId, CancellationToken ct = default) => Task.FromResult(MatchedPaymentIds.Contains(paymentId));

    public HashSet<(LoyaltyEntryKind Kind, Guid Source)> LoyaltyEntries { get; } = [];
    public int AccruedNotReversed { get; set; }
    public List<Guid> FreeCreditPaymentsOldestFirst { get; } = [];

    public Task<bool> LoyaltyEntryExistsAsync(LoyaltyEntryKind kind, Guid? invoiceId, Guid? creditNoteId, Guid? sourceCustomerId, CancellationToken ct = default) =>
        Task.FromResult(LoyaltyEntries.Contains((kind, invoiceId ?? creditNoteId ?? sourceCustomerId ?? Guid.Empty)));

    public Task<int> GetAccruedNotReversedAsync(Guid invoiceId, CancellationToken ct = default) => Task.FromResult(AccruedNotReversed);

    public Task<IReadOnlyList<Guid>> FindFreeCreditPaymentIdsAsync(Guid customerId, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<Guid>>(FreeCreditPaymentsOldestFirst.ToList());

    public List<Guid> CreditNoteIds { get; } = [];

    public Task<IReadOnlyList<Guid>> FindCreditNoteIdsAsync(Guid invoiceId, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<Guid>>(CreditNoteIds.ToList());
}

/// <summary>A numbering port without a database: one series per (branch, year, kind), no collisions.</summary>
public sealed class FakeDocumentNumbering : IDocumentNumbering
{
    private readonly Dictionary<(Guid, string, BillingDocumentKind), DocumentNumberSeriesAggregate> _series = [];

    public Task<DocumentNumberSeriesAggregate> EnsureSeriesAsync(Guid branchId, string financialYear, BillingDocumentKind kind, CancellationToken ct = default)
    {
        if (!_series.TryGetValue((branchId, financialYear, kind), out var series))
        {
            series = new DocumentNumberSeriesAggregate(Guid.NewGuid());
            series.Open(branchId, financialYear, kind, TestUserContext.Create());
            _series[(branchId, financialYear, kind)] = series;
        }

        return Task.FromResult(series);
    }

    public Task SaveWithRetryAsync(DocumentNumberSeriesAggregate series, Func<Task> save, Action<long> renumber, CancellationToken ct = default) => save();
}

/// <summary>An in-memory loyalty account store: one tracked account per customer, created empty on first use.</summary>
public sealed class FakeLoyaltyAccountStore(FakeAggregateRootRepository<LoyaltyAccountAggregate> accounts, FakeAggregateRootRepository<LoyaltyEntryAggregate> entries) : ILoyaltyAccountStore
{
    public void Stage(LoyaltyEntryAggregate entry) => entries.SaveAsync(entry, Platform.Infrastructure.Core.Commands.EventPublishOption.InMemoryPublishOnly).GetAwaiter().GetResult(); // no transaction in memory: staged is saved

    public async Task<LoyaltyAccountAggregate> EnsureAsync(Guid customerId, CancellationToken ct = default)
    {
        var existing = await accounts.GetByIdAsync(customerId);
        if (existing is not null) return existing;

        var created = new LoyaltyAccountAggregate(customerId);
        created.Open(TestUserContext.Create());
        accounts.Seed(created);
        return created;
    }
}

/// <summary>The loyalty collaborators of the handlers, over in-memory repositories the test can inspect.</summary>
public sealed class TestLoyalty
{
    public FakeAggregateRootRepository<LoyaltyAccountAggregate> Accounts { get; } = new();
    public FakeAggregateRootRepository<LoyaltyEntryAggregate> Entries { get; } = new();
    public FakeAggregateRootRepository<InvoiceAggregate> Invoices { get; }
    public LoyaltyDependencies Dependencies { get; }

    public TestLoyalty(FakeAggregateRootRepository<InvoiceAggregate> invoices)
    {
        Invoices = invoices;
        Dependencies = new LoyaltyDependencies(new FakeLoyaltyAccountStore(Accounts, Entries), Accounts, Entries, invoices);
    }
}
