namespace CatEcommerce.Shared.Common.Constants;

/// <summary>HO-ADM-001 §10.3.</summary>
public static class StaffRosterRules
{
    public static readonly IReadOnlyList<string> RosterableRoles =
    [
        StaffRoles.Vet, StaffRoles.Nurse, StaffRoles.Caretaker, StaffRoles.Reception
    ];

    public const int MaxSlotsPerDay = 3;
}
