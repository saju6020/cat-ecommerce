namespace CatEcommerce.Api.Filters;

/// <summary>
/// HO-APT-001 ADR-068/§12.4/§12.5, ARCHITECTURE gate second pass HIGH B (owner decision,
/// 2026-09-28: scope the deadlock mapping to slot claims, not every area). A deliberate marker --
/// "this action claims an appointment slot" -- carries no behaviour of its own; MVC turns an
/// attribute on a controller action into endpoint metadata automatically, which
/// <see cref="UniqueConstraintExceptionFilter"/> reads via <c>HttpContext.GetEndpoint()</c> to
/// decide whether a Postgres deadlock (<c>40P01</c>) on this request maps to the same
/// <c>409 SlotNoLongerAvailable</c> a clean exclusion violation (<c>23P01</c>) on the appointment
/// constraints would. Applied to exactly the five slot-taking actions (§11.4): customer place hold
/// (C1), customer reschedule (C4), staff booking (C6), staff move (C7). C15 (the waitlist offer) is
/// dispatched only by <c>ClinicWorker</c>, never by a controller (ADR-075) -- there is no HTTP
/// endpoint to mark for it.
///
/// Every other endpoint in every other area is unaffected: an unmarked endpoint's deadlock stays
/// unhandled by this branch and becomes the logged <c>500</c> it always was, so a real
/// lock-ordering bug elsewhere stays diagnosable instead of turning into a silent, misleading
/// <c>409</c>.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class SlotClaimEndpointAttribute : Attribute;
