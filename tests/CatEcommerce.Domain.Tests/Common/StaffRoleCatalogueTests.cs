using CatEcommerce.Shared.Common.Constants;

namespace CatEcommerce.Domain.Tests.Common;

/// <summary>HO-ACC-001 §15, U-F5.</summary>
public sealed class StaffRoleCatalogueTests
{
    [Fact]
    public void Cells_Covers8RolesBy20Capabilities() =>
        Assert.Equal(8 * 20, StaffRoleCatalogue.Cells.Count);

    [Fact]
    public void Cells_NeverContainCustomerRole() =>
        Assert.DoesNotContain(StaffRoleCatalogue.Cells, c => c.RoleId == "Customer");

    [Fact]
    public void StaffRoles_All_HasEightRolesInCatalogueOrder() =>
        Assert.Equal(
            new[] { "Reception", "Nurse", "Vet", "Caretaker", "Inventory", "ShopManager", "Accountant", "Administrator" },
            StaffRoles.All);
}
