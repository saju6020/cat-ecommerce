using CatEcommerce.Domain.ValueObjects;
using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Domain.Models;

/// <summary>
/// HO-BIL-001 Phase J -- one billable line of a finalised visit, with typed values. <see cref="LineRef"/> is a stable key of the line inside the
/// visit (<c>treatment:{id}</c> or <c>diagnostic:{id}</c>) and becomes the charge's <c>SourceLineRef</c>, so a re-read can never bill a line twice.
/// <see cref="UnitPrice"/> and <see cref="TaxClassId"/> are the snapshots EMR froze when the line was recorded (ADR-127); a null tax class is the
/// source's way of saying it has none, and Billing reports the line rather than guessing.
/// </summary>
public sealed record VisitBillingLine(
    string LineRef, VisitBillableLineKind Kind, string Description, decimal Quantity, Money UnitPrice, Guid? TaxClassId, Guid? ClinicServiceId, Guid? SkuId);

/// <summary>What Billing needs to know about a visit: whether it is finalised, who to bill (the cat's owner at finalising -- null on a visit finalised
/// before Phase J), where and when, and its billable lines (billable, not reversed, priced; diagnostic orders not cancelled).</summary>
public sealed record VisitBillingFacts(
    Guid VisitId, Guid BranchId, Guid CatId, Guid AppointmentId, Guid? CustomerId, bool IsFinalised, DateTimeOffset? FinalisedOn,
    IReadOnlyList<VisitBillingLine> Lines);
