using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Domain.Models;

/// <summary>
/// HO-BIL-001 §8.3: "<c>SourceKind</c> + <c>SourceId</c> + <c>SourceLineRef</c> ... Where the charge
/// came from; the idempotency key" (unique triple <c>ix_charge_source</c>). Used by
/// <c>IBillableChargeRecorder.VoidAsync</c> (§8.4). <c>SourceLineRef</c> defaults to
/// <c>"main"</c> per §8.3.
/// </summary>
public sealed record ChargeSourceKey(ChargeSourceKind SourceKind, Guid SourceId, string SourceLineRef = "main");
