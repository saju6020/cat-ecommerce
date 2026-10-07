namespace CatEcommerce.Shared.Common.Constants;

/// <summary>
/// The code-defined catalogue of staff roles -- the eight staff columns of SRS Appendix D
/// (HO-ACC-001 §10.2, ADR-036). Not administrator-editable (HO-ADM-001 ADR-044 confirms permissions
/// stay code-defined). <c>Customer</c> is deliberately not a member -- it is not a staff role.
/// <c>Groomer</c> appears in SRS §2.3 but has no Appendix D column, so it is not a role either.
///
/// Owned by HO-ACC-001. Created here, ahead of Accounts' own build, because <see cref="ActionRoles"/>
/// rows and role lists (HO-ADM-001's <c>RosterableRoles</c>, every area's own <c>ActionRoles.[Area]
/// .cs</c>) all reference these names — Step 1 of the contracts pass, docs/handover/README.md. Not a
/// partial type: HO-ACC-001 §11.1 lists <c>StaffRoles.cs</c> as its own single file, so once Accounts
/// lands it may extend this file directly (e.g. with <c>StaffRoleCatalogue</c>-adjacent helpers) --
/// but the eight role ids and their order are the part every other area's compiled code already
/// depends on, so they must not move or be renamed without an Amended row here.
/// </summary>
public static class StaffRoles
{
    public const string Reception = nameof(Reception);
    public const string Nurse = nameof(Nurse);
    public const string Vet = nameof(Vet);
    public const string Caretaker = nameof(Caretaker);
    public const string Inventory = nameof(Inventory);
    public const string ShopManager = nameof(ShopManager);
    public const string Accountant = nameof(Accountant);
    public const string Administrator = nameof(Administrator);

    /// <summary>Catalogue order, as stored on <c>StaffAccountAggregate.RoleIds</c> (HO-ACC-001 §8.7).</summary>
    public static readonly IReadOnlyList<string> All =
    [
        Reception, Nurse, Vet, Caretaker, Inventory, ShopManager, Accountant, Administrator
    ];
}
