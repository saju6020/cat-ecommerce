using CatEcommerce.Domain.Models;
using CatEcommerce.Infrastructure.Persistence.ReportingViews;
using CatEcommerce.Read.QueryHandlers.Reporting.Sources;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.EntityFrameworkCore;

namespace CatEcommerce.Read.QueryHandlers.Reporting.Dashboard;

internal static class TileQueries
{
    public static IQueryable<RvClinicAppointmentRow> AppointmentsToday(DashboardContext c)
    {
        var (start, end) = c.Instants(c.Today, c.Today);
        return c.Db.Set<RvClinicAppointmentRow>().AsNoTracking().Where(a => a.BranchId == c.BranchId && (a.StartsOn ?? a.CheckedInOn) >= start && (a.StartsOn ?? a.CheckedInOn) < end);
    }

    public static long Count(IEnumerable<ReportRow> rows, string key, string value) => rows.Count(r => (string?)r[key] == value);

    public static ReportMoney? OneCurrency(IEnumerable<ReportMoney?> values) => RevenueBuilder.SumIfOneCurrency(values);
}

/// <summary>clinic.appointments-today -- the same rows as R1: booked, checked in, completed, no-show.</summary>
public sealed class AppointmentsTodayTile : IDashboardTileSource
{
    public string Key => "clinic.appointments-today";

    public async Task<DashboardTileResult> RunAsync(DashboardContext c, CancellationToken ct)
    {
        var request = c.Request(ReportCatalogue.ClinicDailyScheduleKey, c.Today, c.Today);
        var rows = await new ClinicDailyScheduleSource().RunAsync(request, c.Db, ct);
        return new DashboardTileResult(
        [
            DashboardFigure.Count("booked", "Booked", TileQueries.Count(rows.Rows, "status", "Booked")),
            DashboardFigure.Count("checkedIn", "Checked in", TileQueries.Count(rows.Rows, "status", "CheckedIn") + TileQueries.Count(rows.Rows, "status", "InConsultation")),
            DashboardFigure.Count("completed", "Completed", TileQueries.Count(rows.Rows, "status", "Completed")),
            DashboardFigure.Count("noShow", "No-show", TileQueries.Count(rows.Rows, "status", "NoShow")),
        ], DrillFrom: c.Today, DrillTo: c.Today);
    }
}

/// <summary>clinic.queue-now -- cats waiting now (checked in, not started), the emergencies among them, and the longest wait.</summary>
public sealed class QueueNowTile : IDashboardTileSource
{
    public string Key => "clinic.queue-now";

    public async Task<DashboardTileResult> RunAsync(DashboardContext c, CancellationToken ct)
    {
        var waiting = await TileQueries.AppointmentsToday(c)
            .Where(a => a.CheckedInOn != null && a.StartedOn == null && a.Status == AppointmentStatus.CheckedIn.Value).Select(a => new { a.TriagePriority, a.CheckedInOn }).ToListAsync(ct);

        var longest = waiting.Count == 0 ? (long?)null : (long)Math.Round((c.Now - waiting.Min(w => w.CheckedInOn!.Value)).TotalMinutes, MidpointRounding.AwayFromZero);
        return new DashboardTileResult(
        [
            DashboardFigure.Count("waiting", "Waiting now", waiting.Count),
            DashboardFigure.Count("emergencies", "Emergencies waiting", waiting.Count(w => w.TriagePriority == TriagePriority.Emergency.Value)),
            new DashboardFigure("longestWait", "Longest wait", longest, "Integer", "min"),
        ], DrillFrom: c.Today, DrillTo: c.Today);
    }
}

/// <summary>clinic.my-appointments-today -- the caller's own appointments today and how many remain.</summary>
public sealed class MyAppointmentsTodayTile : IDashboardTileSource
{
    public string Key => "clinic.my-appointments-today";

    public async Task<DashboardTileResult> RunAsync(DashboardContext c, CancellationToken ct)
    {
        var mine = await TileQueries.AppointmentsToday(c).Where(a => a.PerformerId == c.CallerId).Select(a => a.Status).ToListAsync(ct);
        var remaining = new[] { AppointmentStatus.Booked.Value, AppointmentStatus.CheckedIn.Value, AppointmentStatus.InConsultation.Value };
        return new DashboardTileResult(
        [
            DashboardFigure.Count("mine", "My appointments", mine.Count),
            DashboardFigure.Count("remaining", "Remaining", mine.Count(s => remaining.Contains(s))),
        ], DrillFrom: c.Today, DrillTo: c.Today);
    }
}

/// <summary>clinic.vaccinations-due -- due in the next 7 days and overdue, from the same list as R4 (branch-attributed, ADR-292).</summary>
public sealed class VaccinationsDueTile : IDashboardTileSource
{
    public string Key => "clinic.vaccinations-due";

    public async Task<DashboardTileResult> RunAsync(DashboardContext c, CancellationToken ct)
    {
        var request = c.Request(ReportCatalogue.VaccinationDueListKey, c.Today, c.Today.AddDays(7),
            ("includeOverdue", true), ("includeParasiteControl", false), ("includeUnattributed", false));
        var result = await new VaccinationDueListSource().RunAsync(request, c.Db, ct);
        return new DashboardTileResult(
        [
            DashboardFigure.Count("dueSoon", "Due in the next 7 days", TileQueries.Count(result.Rows, "status", "DueSoon")),
            DashboardFigure.Count("overdue", "Overdue", TileQueries.Count(result.Rows, "status", "Overdue")),
        ], DrillFrom: c.Today, DrillTo: c.Today.AddDays(7));
    }
}

/// <summary>clinic.consultations-unfinalised -- the caller's visits whose appointment is complete but whose note is not finalised.</summary>
public sealed class ConsultationsUnfinalisedTile : IDashboardTileSource
{
    public string Key => "clinic.consultations-unfinalised";

    public async Task<DashboardTileResult> RunAsync(DashboardContext c, CancellationToken ct)
    {
        var open = VisitStatus.Open.Value;
        var count = await c.Db.Set<RvVisitOpenRow>().AsNoTracking().CountAsync(v => v.BranchId == c.BranchId && v.VetId == c.CallerId && v.Status == open && v.CompletedOn != null, ct);
        return new DashboardTileResult([DashboardFigure.Count("unfinalised", "Completed, not finalised", count)]);
    }
}

/// <summary>surgery.today -- scheduled, in theatre and in recovery today.</summary>
public sealed class SurgeryTodayTile : IDashboardTileSource
{
    public string Key => "surgery.today";

    public async Task<DashboardTileResult> RunAsync(DashboardContext c, CancellationToken ct)
    {
        var (start, end) = c.Instants(c.Today, c.Today);
        var statuses = await c.Db.Set<RvSurgeryRow>().AsNoTracking().Where(s => s.BranchId == c.BranchId && s.OccurredOn >= start && s.OccurredOn < end).Select(s => s.Status).ToListAsync(ct);
        return new DashboardTileResult(
        [
            DashboardFigure.Count("scheduled", "Scheduled", statuses.Count(s => s == SurgeryStatus.Scheduled.Value || s == SurgeryStatus.PreOp.Value)),
            DashboardFigure.Count("inTheatre", "In theatre", statuses.Count(s => s == SurgeryStatus.InTheatre.Value)),
            DashboardFigure.Count("inRecovery", "In recovery", statuses.Count(s => s == SurgeryStatus.Recovery.Value)),
        ], DrillFrom: c.Today, DrillTo: c.Today);
    }
}

/// <summary>stock.expiry -- batches expiring within 30 days and their value at risk at cost (OQ-3).</summary>
public sealed class StockExpiryTile : IDashboardTileSource
{
    public string Key => "stock.expiry";

    public async Task<DashboardTileResult> RunAsync(DashboardContext c, CancellationToken ct)
    {
        var request = c.Request(ReportCatalogue.StockNearExpiryKey, c.Today, c.Today.AddDays(30), ("includeExpired", false));
        var result = await new StockNearExpirySource().RunAsync(request, c.Db, ct);
        return new DashboardTileResult(
        [
            DashboardFigure.Count("batches", "Batches expiring within 30 days", result.Rows.Count),
            DashboardFigure.Amount("valueAtRisk", "Value at risk (cost)", result.Totals?.GetValueOrDefault("valueAtRiskCost") as ReportMoney),
        ], DrillFrom: c.Today, DrillTo: c.Today.AddDays(30));
    }
}

/// <summary>stock.low -- SKU-locations below their reorder point.</summary>
public sealed class StockLowTile : IDashboardTileSource
{
    public string Key => "stock.low";

    public async Task<DashboardTileResult> RunAsync(DashboardContext c, CancellationToken ct)
    {
        var request = c.Request(ReportCatalogue.StockLowReorderKey, c.Today.AddDays(-29), c.Today);
        var result = await new StockLowReorderSource().RunAsync(request, c.Db, ct);
        return new DashboardTileResult([DashboardFigure.Count("belowPoint", "Below reorder point", result.Rows.Count)], DrillFrom: c.Today.AddDays(-29), DrillTo: c.Today);
    }
}

/// <summary>stock.consignments-open -- consignments that have not cleared and are not cancelled.</summary>
public sealed class ConsignmentsOpenTile : IDashboardTileSource
{
    public string Key => "stock.consignments-open";

    public async Task<DashboardTileResult> RunAsync(DashboardContext c, CancellationToken ct)
    {
        var closed = new[] { ConsignmentStatus.Cleared.Value, ConsignmentStatus.Cancelled.Value };
        var count = await c.Db.Set<RvConsignmentRow>().AsNoTracking().CountAsync(x => x.BranchId == c.BranchId && !closed.Contains(x.ClearanceStatus), ct);
        return new DashboardTileResult([DashboardFigure.Count("open", "Awaiting clearance", count)], DrillFrom: c.Today.AddDays(-30), DrillTo: c.Today);
    }
}

/// <summary>orders.today -- placed today, waiting to be packed, dispatched today.</summary>
public sealed class OrdersTodayTile : IDashboardTileSource
{
    public string Key => "orders.today";

    public async Task<DashboardTileResult> RunAsync(DashboardContext c, CancellationToken ct)
    {
        var (start, end) = c.Instants(c.Today, c.Today);
        var orders = c.Db.Set<RvOrderRow>().AsNoTracking().Where(o => o.BranchId == c.BranchId);
        var confirmed = OrderStatus.Confirmed.Value;
        return new DashboardTileResult(
        [
            DashboardFigure.Count("placed", "Placed today", await orders.CountAsync(o => o.PlacedOn >= start && o.PlacedOn < end, ct)),
            DashboardFigure.Count("toPack", "To pack", await orders.CountAsync(o => o.Status == confirmed, ct)),
            DashboardFigure.Count("dispatched", "Dispatched today", await orders.CountAsync(o => o.DispatchedOn >= start && o.DispatchedOn < end, ct)),
        ]);
    }
}

/// <summary>orders.awaiting-transfer -- bank-transfer orders still in pending payment, and the oldest one's age.</summary>
public sealed class OrdersAwaitingTransferTile : IDashboardTileSource
{
    public string Key => "orders.awaiting-transfer";

    public async Task<DashboardTileResult> RunAsync(DashboardContext c, CancellationToken ct)
    {
        var pending = OrderStatus.PendingPayment.Value;
        var transfer = OrderPaymentMethod.BankTransfer.Value;
        var placed = await c.Db.Set<RvOrderRow>().AsNoTracking().Where(o => o.BranchId == c.BranchId && o.Status == pending && o.PaymentMethod == transfer).Select(o => o.PlacedOn).ToListAsync(ct);
        long? oldest = placed.Count == 0 ? null : (long)Math.Floor((c.Now - placed.Min()).TotalHours);
        return new DashboardTileResult([DashboardFigure.Count("awaiting", "Awaiting transfer", placed.Count), new DashboardFigure("oldestAge", "Oldest", oldest, "Integer", "h")]);
    }
}

/// <summary>orders.returns-pending -- returns received and waiting for a restock or write-off decision.</summary>
public sealed class OrdersReturnsPendingTile : IDashboardTileSource
{
    public string Key => "orders.returns-pending";

    public async Task<DashboardTileResult> RunAsync(DashboardContext c, CancellationToken ct)
    {
        var received = ReturnStatus.Received.Value;
        var count = await c.Db.Set<RvOrderReturnRow>().AsNoTracking().CountAsync(r => r.BranchId == c.BranchId && r.Status == received, ct);
        return new DashboardTileResult([DashboardFigure.Count("pending", "Awaiting a decision", count)]);
    }
}

/// <summary>revenue.shop-today -- shop net revenue today, with no cost.</summary>
public sealed class ShopRevenueTodayTile : IDashboardTileSource
{
    public string Key => "revenue.shop-today";

    public async Task<DashboardTileResult> RunAsync(DashboardContext c, CancellationToken ct)
    {
        var facts = await RevenueLoader.LoadAsync(c.Db, c.Request(ReportCatalogue.RevenueShopByCategoryKey, c.Today, c.Today), shopOnly: true, includeCost: false, ct);
        var net = RevenueBuilder.Aggregate(facts, TimeBucket.Day, f => [f.Line]).Select(a => RevenueBuilder.Money(a.Net, a.Currency));
        return new DashboardTileResult([DashboardFigure.Amount("net", "Shop net revenue", TileQueries.OneCurrency(net) ?? (facts.Count == 0 ? new ReportMoney(0m, c.CurrencyCode) : null))], DrillFrom: c.Today, DrillTo: c.Today);
    }
}

/// <summary>payments.today -- recorded and accepted today, by method: exactly the four launch methods.</summary>
public sealed class PaymentsTodayTile : IDashboardTileSource
{
    public string Key => "payments.today";

    public async Task<DashboardTileResult> RunAsync(DashboardContext c, CancellationToken ct)
    {
        var accepted = PaymentStatus.Accepted.Value;
        var payments = await c.Db.Set<RvPaymentRow>().AsNoTracking().Where(p => p.BranchId == c.BranchId && p.Status == accepted && p.BusinessDate == c.Today).ToListAsync(ct);

        DashboardFigure For(PaymentMethod method, string label) =>
            DashboardFigure.Amount(method.DisplayName, label, TileQueries.OneCurrency(payments.Where(p => p.Method == method.Value).GroupBy(p => p.CurrencyCode).Select(g => (ReportMoney?)new ReportMoney(g.Sum(p => p.Amount), g.Key)))
                ?? (payments.All(p => p.Method != method.Value) ? new ReportMoney(0m, c.CurrencyCode) : null));

        return new DashboardTileResult(
        [
            For(ReportPaymentMethods.CashOnDelivery, "Cash on delivery"), For(ReportPaymentMethods.CashInStore, "Cash in store"), For(ReportPaymentMethods.CardInStore, "Card in store"), For(ReportPaymentMethods.BankTransfer, "Bank transfer"),
        ], DrillFrom: c.Today, DrillTo: c.Today);
    }
}

/// <summary>payments.unreconciled -- open mismatches over the last week and their amount.</summary>
public sealed class PaymentsUnreconciledTile : IDashboardTileSource
{
    public string Key => "payments.unreconciled";

    public async Task<DashboardTileResult> RunAsync(DashboardContext c, CancellationToken ct)
    {
        var from = c.Today.AddDays(-6);
        var result = await new PaymentReconciliationSource().RunAsync(c.Request(ReportCatalogue.PaymentReconciliationKey, from, c.Today), c.Db, ct);
        var open = result.Rows.Sum(r => (long)(r["mismatchesOpen"] ?? 0L));
        var amount = TileQueries.OneCurrency(result.Rows.Where(r => (long)(r["mismatchesOpen"] ?? 0L) > 0).Select(r => r["variance"] as ReportMoney));
        return new DashboardTileResult([DashboardFigure.Count("open", "Open mismatches", open), DashboardFigure.Amount("amount", "Amount", amount ?? (open == 0 ? new ReportMoney(0m, c.CurrencyCode) : null))],
            "Over the last 7 days.", from, c.Today);
    }
}

/// <summary>billing.cash-up -- today's cash-up: open or closed, and the variance.</summary>
public sealed class CashUpTodayTile : IDashboardTileSource
{
    public string Key => "billing.cash-up";

    public async Task<DashboardTileResult> RunAsync(DashboardContext c, CancellationToken ct)
    {
        var row = await c.Db.Set<RvCashUpRow>().AsNoTracking().Where(x => x.BranchId == c.BranchId && x.BusinessDate == c.Today).FirstOrDefaultAsync(ct);
        return new DashboardTileResult(
        [
            DashboardFigure.Text("status", "Today", row is null ? CashUpReportBuilder.NotClosed : ReportLabels.Of<CashUpStatus>(row.Status)),
            DashboardFigure.Amount("variance", "Variance", row?.Variance is { } v ? new ReportMoney(v, c.CurrencyCode) : null),
        ], DrillFrom: c.Today, DrillTo: c.Today);
    }
}

/// <summary>billing.receivables -- what is outstanding and how much of it is over 90 days old.</summary>
public sealed class ReceivablesTile : IDashboardTileSource
{
    public string Key => "billing.receivables";

    public async Task<DashboardTileResult> RunAsync(DashboardContext c, CancellationToken ct)
    {
        var open = await c.Db.Set<RvInvoiceBalanceRow>().AsNoTracking().Where(i => i.BranchId == c.BranchId && i.OutstandingAmount > 0m).Select(i => new { i.CurrencyCode, i.OutstandingAmount, i.IssueDate }).ToListAsync(ct);
        var cutoff = c.Today.AddDays(-90);
        ReportMoney? Sum(Func<DateOnly?, bool> take) => open.Count(i => take(i.IssueDate)) == 0 ? new ReportMoney(0m, c.CurrencyCode)
            : TileQueries.OneCurrency(open.Where(i => take(i.IssueDate)).GroupBy(i => i.CurrencyCode).Select(g => (ReportMoney?)new ReportMoney(g.Sum(i => i.OutstandingAmount), g.Key)));

        return new DashboardTileResult(
        [
            DashboardFigure.Amount("outstanding", "Outstanding", Sum(_ => true)),
            DashboardFigure.Amount("over90", "Over 90 days", Sum(d => d < cutoff)),
        ], DrillFrom: c.Today, DrillTo: c.Today);
    }
}

/// <summary>revenue.today-by-line -- net revenue today for clinic, shop and boarding.</summary>
public sealed class RevenueTodayByLineTile : IDashboardTileSource
{
    public string Key => "revenue.today-by-line";

    public async Task<DashboardTileResult> RunAsync(DashboardContext c, CancellationToken ct)
    {
        var facts = await RevenueLoader.LoadAsync(c.Db, c.Request(ReportCatalogue.RevenueByBusinessLineKey, c.Today, c.Today), shopOnly: false, includeCost: false, ct);
        var aggregates = RevenueBuilder.Aggregate(facts, TimeBucket.Day, f => [f.Line]);

        DashboardFigure For(string line, string label)
        {
            var parts = aggregates.Where(a => a.Dimensions[0] == line).Select(a => RevenueBuilder.Money(a.Net, a.Currency)).ToList();
            return DashboardFigure.Amount(line.ToLowerInvariant(), label, parts.Count == 0 ? new ReportMoney(0m, c.CurrencyCode) : TileQueries.OneCurrency(parts));
        }

        // Boarding is a deferred source (HO-BRD-001 is not merged): its figure is absent -- not zero -- until there is boarding revenue to report (handover U-E4).
        var figures = new List<DashboardFigure> { For("Clinic", "Clinic"), For("Shop", "Shop") };
        if (aggregates.Any(a => a.Dimensions[0] == "Boarding")) figures.Add(For("Boarding", "Boarding"));

        return new DashboardTileResult(figures, DrillFrom: c.Today, DrillTo: c.Today);
    }
}
