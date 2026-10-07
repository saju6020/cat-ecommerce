namespace CatEcommerce.Shared.Common.Enums;

/// <summary>
/// Owned by HO-BIL-001 §8.2/§8.3. Named <c>ChargeSourceKind</c> here for the field HO-BIL-001's own
/// prose calls <c>SourceKind</c> (on <c>ChargeAggregate</c> and <c>ChargeRequestDto</c>) -- given the
/// generic-sounding field name, the type itself is named for what it discriminates rather than
/// reusing the bare word "SourceKind" as a type name. <see cref="DaycareVisit"/> is HO-BRD-001
/// §12.8 P2's addition ("Daycare: <c>DaycareVisit</c> source on check-out").
/// </summary>
public sealed class ChargeSourceKind(int value, string displayName) : Enumeration(value, displayName)
{
    public static readonly ChargeSourceKind Appointment = new(1, nameof(Appointment));
    public static readonly ChargeSourceKind Visit = new(2, nameof(Visit));
    public static readonly ChargeSourceKind Dispense = new(3, nameof(Dispense));
    public static readonly ChargeSourceKind Surgery = new(4, nameof(Surgery));
    public static readonly ChargeSourceKind Order = new(5, nameof(Order));
    public static readonly ChargeSourceKind BoardingStay = new(6, nameof(BoardingStay));
    public static readonly ChargeSourceKind Manual = new(7, nameof(Manual));
    public static readonly ChargeSourceKind DaycareVisit = new(8, nameof(DaycareVisit));
}
