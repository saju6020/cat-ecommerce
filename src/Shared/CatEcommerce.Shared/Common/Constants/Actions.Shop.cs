namespace CatEcommerce.Shared.Common.Constants;

/// <summary>HO-SHP-001 §12.3 -- Shop's action names, appended per ADR-341. Appendix D's "Product
/// catalogue" row.</summary>
public static partial class Actions
{
    public static class Shop
    {
        public const string ProductsView = "Products.View";
        public const string ProductsManage = "Products.Manage";
        public const string ProductsManagePrices = "Products.ManagePrices";
        public const string SourcingOriginsManage = "SourcingOrigins.Manage";
        public const string ReviewsModerate = "Reviews.Moderate";
    }
}
