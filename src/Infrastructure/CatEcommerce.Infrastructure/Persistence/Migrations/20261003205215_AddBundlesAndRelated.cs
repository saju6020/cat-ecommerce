using CatEcommerce.Shared.Common.Enums;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CatEcommerce.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// HO-SHP-001 section 12.1 Phase F -- re-creates <c>product.storefront_catalogue_entry</c> with
    /// the bundle clause (US-SHP-12 AC2). The <c>bundle_component</c> and <c>related_product</c>
    /// tables already exist: Phase A's <c>AddProductCatalogue</c> created every table owned by
    /// <c>ProductAggregate</c> up front, because they are one aggregate's owned collections (see
    /// HO-SHP-001.md section 1 Amended, 2026-10-04) -- so the model snapshot is unchanged and this
    /// migration is the view alone. Status values are read from the enumeration constants, never
    /// typed as literals (U-X5).
    ///
    /// <c>in_stock</c> keeps Phase E's approximation (an Active SKU with a price effective now; see
    /// section 1 Amended, 2026-09-28) for a standard product. A bundle is in stock only when its
    /// own SKU is on sale AND it has components AND every component SKU is Active, on an Active,
    /// listed product and priced now -- exactly <c>SkuSellability</c> on the read side, so the
    /// catalogue and the detail page cannot disagree.
    /// </summary>
    public partial class AddBundlesAndRelated : Migration
    {
        private static readonly int ProductActive = ProductStatus.Active.Value;
        private static readonly int SkuActive = SkuStatus.Active.Value;
        private static readonly int KindBundle = ProductKind.Bundle.Value;

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP VIEW IF EXISTS product.storefront_catalogue_entry;");
            migrationBuilder.Sql(ViewSql(withBundleClause: true));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP VIEW IF EXISTS product.storefront_catalogue_entry;");
            migrationBuilder.Sql(ViewSql(withBundleClause: false));
        }

        private static string ViewSql(bool withBundleClause)
        {
            // Perf (I-E11): "an Active SKU with a price effective now" is exactly "the pricing aggregate below is
            // non-null" (it aggregates the current price of every Active SKU), so the separate per-row
            // EXISTS is dropped -- same semantics, one evaluation instead of two.
            var standardInStock = "pricing.min_price IS NOT NULL";

            var bundleInStock = $"""
                CASE WHEN p.kind = {KindBundle} THEN (
                        EXISTS (
                            SELECT 1
                            FROM product.sku sb
                            JOIN product.sku_price spb ON spb.sku_id = sb."Id" AND spb.effective_from <= now()
                            WHERE sb.product_id = p."Id" AND sb.status = {SkuActive}
                        )
                        AND EXISTS (SELECT 1 FROM product.bundle_component bc0 WHERE bc0.product_id = p."Id")
                        AND NOT EXISTS (
                            SELECT 1
                            FROM product.bundle_component bc
                            LEFT JOIN product.sku cs ON cs."Id" = bc.component_sku_id
                            LEFT JOIN product.product cp ON cp."Id" = cs.product_id
                            WHERE bc.product_id = p."Id"
                              AND NOT (
                                cs.status = {SkuActive}
                                AND cp.status = {ProductActive}
                                AND cp.listed_on_storefront = true
                                AND EXISTS (SELECT 1 FROM product.sku_price csp WHERE csp.sku_id = cs."Id" AND csp.effective_from <= now())
                              )
                        )
                    ) ELSE {standardInStock} END
                """;

            var inStock = withBundleClause ? bundleInStock : standardInStock;

            return $"""
                CREATE VIEW product.storefront_catalogue_entry AS
                SELECT
                    p."Id" AS product_id,
                    p.slug,
                    p.name,
                    p.brand,
                    p.category,
                    p.subcategory,
                    p.kind,
                    p.country_of_origin_code,
                    p.food_life_stage AS life_stage,
                    p.food_protein_source AS protein_source,
                    p.prescription_required,
                    p.search_text,
                    p.first_published_on,
                    pricing.min_price,
                    pricing.max_price,
                    pricing.currency_code,
                    {inStock} AS in_stock,
                    rating.rating_average,
                    COALESCE(rating.rating_count, 0) AS rating_count,
                    COALESCE(sales.units_sold_90d, 0) AS units_sold_90d,
                    img.file_id AS primary_image_file_id,
                    img.alt_text AS primary_image_alt_text
                FROM product.product p
                -- Set-based current price per SKU (the greatest effective_from <= now(), the SkuPricing rule) joined once,
                -- instead of one correlated index probe per SKU per product: the broad catalogue queries
                -- (the p95 cases) evaluate the view for every matching product.
                LEFT JOIN (
                    SELECT s.product_id, MIN(cp.amount) AS min_price, MAX(cp.amount) AS max_price, MIN(cp.currency_code) AS currency_code
                    FROM product.sku s
                    JOIN (
                        SELECT DISTINCT ON (sku_id) sku_id, amount, currency_code
                        FROM product.sku_price
                        WHERE effective_from <= now()
                        ORDER BY sku_id, effective_from DESC
                    ) cp ON cp.sku_id = s."Id"
                    WHERE s.status = {SkuActive}
                    GROUP BY s.product_id
                ) pricing ON pricing.product_id = p."Id"
                LEFT JOIN product.product_rating_summary rating ON rating.product_id = p."Id"
                LEFT JOIN product.product_sales_stats sales ON sales."Id" = p."Id"
                LEFT JOIN product.product_image img ON img.product_id = p."Id" AND img.sku_id IS NULL AND img.is_primary = true
                WHERE p.status = {ProductActive} AND p.listed_on_storefront = true;
                """;
        }
    }
}
