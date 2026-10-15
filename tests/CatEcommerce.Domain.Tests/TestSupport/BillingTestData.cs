using CatEcommerce.Domain.Aggregates.Billing;
using CatEcommerce.Domain.Models;
using CatEcommerce.Domain.ValueObjects;
using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Domain.Tests.TestSupport;

/// <summary>Builders for the Billing aggregates' inputs -- one place, so a test reads as the rule it proves.</summary>
internal static class BillingTestData
{
    public const string Currency = "BDT";

    public static BillingActor Actor() => new(TestUserContext.Create(), Guid.NewGuid());

    public static DraftInvoiceLineInput Line(
        decimal quantity = 1m, decimal unitPrice = 100m, decimal discount = 0m, Guid? taxClassId = null, ItemKind? kind = null,
        Guid? chargeId = null, BusinessLine? line = null, ChargeSourceKind? source = null, string description = "Item") =>
        new(chargeId ?? Guid.NewGuid(), null, line ?? BusinessLine.Clinic, kind ?? ItemKind.Service, source ?? ChargeSourceKind.Appointment,
            Guid.NewGuid(), null, null, null, description, quantity, unitPrice, discount, taxClassId ?? Guid.NewGuid());

    public static InvoiceAggregate Draft(params DraftInvoiceLineInput[] lines)
    {
        var invoice = new InvoiceAggregate(Guid.NewGuid());
        invoice.CreateDraft(new DraftInvoiceDto(
            Guid.NewGuid(), Guid.NewGuid(), Currency, lines.Length == 0 ? [Line()] : lines, null, null, null, DateTimeOffset.UtcNow, Actor()));
        return invoice;
    }

    public static SellerSnapshot Seller() => new("PurrCare Ltd", "TIN-123", "1 Road, Dhaka", null, null, null, "[]");

    public static BillToSnapshot BillTo() => new("A Customer", "+8801700000000", null, null);

    public static InvoiceIssueDto IssueDto(InvoiceAggregate draft, decimal ratePercent = 15m, string number = "INV-MAIN-2627-000001", long sequence = 1, int termsDays = 0)
    {
        var rates = draft.Lines.Select(l => l.TaxClassId).Distinct()
            .ToDictionary(id => id, id => new TaxRateInfo(id, "STD", ratePercent));
        return IssueDto(rates, number, sequence, termsDays);
    }

    public static InvoiceIssueDto IssueDto(IReadOnlyDictionary<Guid, TaxRateInfo> rates, string number = "INV-MAIN-2627-000001", long sequence = 1, int termsDays = 0) =>
        new(number, "2026-27", sequence, rates, Seller(), BillTo(), DateTimeOffset.UtcNow, "Asia/Dhaka", termsDays,
            DateOnly.FromDateTime(DateTime.UtcNow), null, Actor());

    public static InvoiceAggregate Issued(decimal unitPrice = 1000m, decimal ratePercent = 0m)
    {
        var invoice = Draft(Line(1m, unitPrice));
        invoice.Issue(IssueDto(invoice, ratePercent));
        return invoice;
    }

    public static PaymentRecordDto PaymentDto(
        decimal amount = 100m, PaymentMethod? method = null, string? reference = null, PaymentCollector? collector = null,
        PaymentEarmark? earmark = null, decimal threshold = 50_000m, IReadOnlyList<AllocationRequest>? allocations = null,
        DateOnly? receivedOn = null, DateOnly? todayLocal = null, string? notes = null, string? waiverReason = null)
    {
        var today = todayLocal ?? DateOnly.FromDateTime(DateTime.UtcNow);
        return new PaymentRecordDto(
            Guid.NewGuid(), Guid.NewGuid(), method ?? PaymentMethod.CashInStore, amount, Currency, receivedOn ?? today, reference, collector, earmark,
            notes, threshold, allocations ?? [], today, DateTimeOffset.UtcNow, null, Actor(), null, waiverReason);
    }

    public static PaymentAggregate Payment(PaymentRecordDto? dto = null)
    {
        var payment = new PaymentAggregate(Guid.NewGuid());
        payment.Record(dto ?? PaymentDto());
        return payment;
    }
}
