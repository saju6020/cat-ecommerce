using CatEcommerce.Domain.Entities;
using CatEcommerce.Domain.Models;
using CatEcommerce.Domain.ValueObjects;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Domain.DomainServices;

/// <summary>One Billing charge of a stay as the ledger shows it (the line ref is the stay's own key, <see cref="BoardingChargePlanner.RefOf"/>).</summary>
/// <summary><paramref name="CreditedNet"/> is the net already credited on the charge by Billing credit notes: an invoiced charge that no longer applies is owed only what is NOT yet credited, so a credit clears once the credit note is issued.</summary>
public sealed record BoardingChargeRow(string LineRef, Guid ChargeId, bool IsVoided, bool IsBilled, decimal Net, decimal CreditedNet = 0m);

public sealed record PlannedCharge(string LineRef, QuotedRateUnit Unit);

/// <summary>What recording the stay's current rate units asks of Billing: charges to void (not invoiced), charges to record, and the credit that is owed for invoiced charges that no longer apply.</summary>
public sealed record BoardingChargePlan(int Revision, IReadOnlyList<string> VoidRefs, IReadOnlyList<PlannedCharge> Record, decimal CreditDue)
{
    public bool HasChanges => VoidRefs.Count > 0 || Record.Count > 0;
}

/// <summary>
/// HO-BRD-001 section 8.15 (ADR-230, renumbered ADR-363) -- the stay's charges under the PUSH model of merged Billing: each rate unit is one charge whose source key is
/// <c>(BoardingStay, stayId, "r{revision}.{kind}.{from}-{to}")</c>. Re-deriving after an extension or an early check-out compares the units with what Billing already has, by
/// kind, night range and net amount: an identical charge is KEPT (invoiced or not), an uninvoiced charge that no longer applies is VOIDED, a unit with no charge is RECORDED under
/// the next revision, and an INVOICED charge that no longer applies becomes a credit owed (a charge is never changed once invoiced, and Billing charges are never negative). Pure.
/// </summary>
public static class BoardingChargePlanner
{
    public static string RefOf(int revision, QuotedRateUnit unit) => $"r{revision}.{unit.Kind.DisplayName}.{unit.FromNight}-{unit.ToNight}";

    private static string KeyOf(QuotedRateUnit unit) => $"{unit.Kind.DisplayName}.{unit.FromNight}-{unit.ToNight}";

    /// <summary>The identity of a recorded charge without its revision prefix: <c>r2.Month.1-30</c> -> <c>Month.1-30</c>.</summary>
    public static string KeyOfRef(string lineRef)
    {
        var dot = lineRef.IndexOf('.');
        return dot < 0 ? lineRef : lineRef[(dot + 1)..];
    }

    public static BoardingChargePlan Plan(IReadOnlyList<BoardingChargeRow> existing, IEnumerable<QuotedRateUnit> units, int currentRevision)
    {
        var active = existing.Where(r => !r.IsVoided).ToList();
        var unused = active.ToList();
        var toRecord = new List<QuotedRateUnit>();

        foreach (var unit in units)
        {
            var key = KeyOf(unit);
            // Prefer a billed match (it can never change), then an unbilled one.
            var match = unused.FirstOrDefault(r => r.IsBilled && KeyOfRef(r.LineRef) == key && r.Net == unit.Net)
                ?? unused.FirstOrDefault(r => !r.IsBilled && KeyOfRef(r.LineRef) == key && r.Net == unit.Net);

            if (match is not null) unused.Remove(match);
            else toRecord.Add(unit);
        }

        var voids = unused.Where(r => !r.IsBilled).Select(r => r.LineRef).ToList();
        var credit = unused.Where(r => r.IsBilled).Sum(r => Math.Max(0m, r.Net - r.CreditedNet));

        var revision = voids.Count > 0 || toRecord.Count > 0 ? currentRevision + 1 : currentRevision;
        return new BoardingChargePlan(revision, voids, toRecord.Select(u => new PlannedCharge(RefOf(revision, u), u)).ToList(), credit);
    }

    /// <summary>The Billing request for one planned charge: the unit's amount is the unit price (quantity 1), its discount the line discount; the service date is the unit's first night.</summary>
    public static ChargeRequestDto ToRequest(PlannedCharge planned, Guid stayId, Guid customerId, Guid branchId, DateOnly arrivalOn, string currencyCode, Guid taxClassId, Guid? firstCatId)
    {
        var unit = planned.Unit;
        return new ChargeRequestDto(
            customerId, branchId, unit.Kind == RateUnitKind.AdditionalCat ? null : firstCatId, BusinessLine.Boarding, ItemKind.BoardingNight, ChargeSourceKind.BoardingStay, stayId, planned.LineRef,
            BillingGroupKind.BoardingStay, stayId, unit.Description.Length > 300 ? unit.Description[..300] : unit.Description, 1m, new Money(unit.Amount, currencyCode), new Money(unit.Discount, currencyCode),
            taxClassId, arrivalOn.AddDays(unit.FromNight - 1));
    }
}

/// <summary>US-BRD-16 -- the nights an early check-out is charged for.</summary>
public static class BoardingCheckoutRules
{
    /// <summary>
    /// <c>nightsUsed = max(1, localCheckoutDate - arrivalOn) + chargedExtraNights</c>, never more than the booked nights; a check-out on or after the booked departure is charged the
    /// booked nights. Returns the booked nights when it is not an early check-out.
    /// </summary>
    public static int NightsToCharge(DateOnly arrivalOn, DateOnly departureOn, DateOnly localCheckoutDate, int chargedExtraNights)
    {
        var booked = StayDateRules.Nights(arrivalOn, departureOn);
        if (localCheckoutDate >= departureOn) return booked;

        var used = Math.Max(1, localCheckoutDate.DayNumber - arrivalOn.DayNumber) + Math.Max(0, chargedExtraNights);
        return Math.Min(booked, used);
    }
}

/// <summary>HO-BRD-001 section 8.14 -- the vet spending limit of one cat, pure. <c>limit = (authorised ? limit : 0) + approved increases</c>; <c>headroom = max(0, limit - committed)</c>.</summary>
public static class VetSpendingHeadroom
{
    public static VetSpendingHeadroomResult Compute(StayCat cat, IEnumerable<VetChargeApproval> approvals, IEnumerable<VetChargeCommitment> commitments, string currencyCode) =>
        Compute(
            cat.VetVisitAuthorised, cat.VetSpendingLimit, approvals.Where(a => a.CatId == cat.CatId && a.Status == VetApprovalStatus.Approved).Select(a => a.Amount),
            commitments.Where(c => c.CatId == cat.CatId).Select(c => c.Amount), currencyCode);

    /// <summary>The same rule over plain values -- a read projects the cat's authorisation and limit, the APPROVED increases and the commitments, and calls this (the entity overload delegates here).</summary>
    public static VetSpendingHeadroomResult Compute(bool authorised, decimal limit, IEnumerable<decimal> approvedIncreases, IEnumerable<decimal> committedAmounts, string currencyCode)
    {
        var total = (authorised ? limit : 0m) + approvedIncreases.Sum();
        var committed = committedAmounts.Sum();

        return new VetSpendingHeadroomResult(authorised, new Money(total, currencyCode), new Money(committed, currencyCode), new Money(Math.Max(0m, total - committed), currencyCode));
    }
}
