namespace CatEcommerce.Shared.Common.Helpers;

/// <summary>
/// The "no meaningful value" return type used by several cross-area ports whose async methods
/// have nothing to return but still need to report success/failure through a result wrapper
/// (e.g. <c>Task&lt;StockResult&lt;Unit&gt;&gt;</c> in HO-IMP-001 §8.8,
/// <c>Task&lt;FileStoreResult&lt;Unit&gt;&gt;</c> in ADR-318). Every owning handover's code sample
/// uses <c>Unit</c> without redefining it, so it is a solution-wide shared type rather than any one
/// area's. Not the platform's -- <c>Platform.Infrastructure.Core</c> 1.0.34 has no such type.
/// </summary>
public readonly struct Unit : IEquatable<Unit>
{
    public static readonly Unit Value = default;

    public bool Equals(Unit other) => true;
    public override bool Equals(object? obj) => obj is Unit;
    public override int GetHashCode() => 0;
    public override string ToString() => "()";

    public static bool operator ==(Unit left, Unit right) => true;
    public static bool operator !=(Unit left, Unit right) => false;
}
