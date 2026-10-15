using CatEcommerce.Shared.Common.Enums;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CatEcommerce.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// HO-SHP-001-FU4 (SHP backlog row 64) -- the catalogue's work is done on the matching products, not on the whole catalogue:
    /// (1) an expression index on <c>lower(brand)</c> so the brand filter's <c>lower(brand) = ANY(...)</c> is indexable AND the planner
    /// gets statistics for the expression (the plain <c>ix_product_brand</c> could serve neither). Deliberately NOT partial: PostgreSQL
    /// ignores the expression statistics of a partial index when estimating, which would leave the 32-rows-for-5,000 misestimate in place; (2) <c>product.storefront_catalogue_entry</c> re-created with its pricing as a
    /// per-product LATERAL over <c>ix_sku_price_sku_effective</c> (latest price effective now per Active SKU) instead of a
    /// DISTINCT ON / GROUP BY over all of <c>sku_price</c> and <c>sku</c> on every request. Same columns, types and values (the
    /// unique (sku_id, effective_from) index makes "latest" unambiguous); <c>CREATE OR REPLACE</c> keeps dependants. The model
    /// snapshot is unchanged (EF cannot model an expression index). Status values come from the enumerations (U-X5).
    /// </summary>
    public partial class OptimiseStorefrontCatalogue : Migration
    {
        private static readonly int ProductActive = ProductStatus.Active.Value;
        private static readonly int SkuActive = SkuStatus.Active.Value;
        private static readonly int KindBundle = ProductKind.Bundle.Value;

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql($"""
                CREATE INDEX IF NOT EXISTS ix_product_brand_lower ON product.product (lower(brand));
                ANALYZE product.product;
                """);
            migrationBuilder.Sql(ViewSql(lateralPricing: true));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(ViewSql(lateralPricing: false));
            migrationBuilder.Sql("DROP INDEX IF EXISTS product.ix_product_brand_lower;");
        }

        private static string ViewSql(bool lateralPricing)
        {
            var pricing = lateralPricing
                ? $"""
                -- Per product: the latest price effective now (the SkuPricing rule) of each Active SKU, probed through
                -- ix_sku_price_sku_effective. The filters on product run first, so only the matching products are priced.
                LEFT JOIN LATERAL (
                    SELECT MIN(cp.amount) AS min_price, MAX(cp.amount) AS max_price, MIN(cp.currency_code) AS currency_code
                    FROM product.sku s
                    CROSS JOIN LATERAL (
                        SELECT sp.amount, sp.currency_code
                        FROM product.sku_price sp
                        WHERE sp.sku_id = s."Id" AND sp.effective_from <= now()
                        ORDER BY sp.effective_from DESC
                        LIMIT 1
                    ) cp
                    WHERE s.product_id = p."Id" AND s.status = {SkuActive}
                ) pricing ON true
                """
                : $"""
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
                """;

            // The aggregate-without-GROUP-BY lateral always yields one row, all NULL when the product has no priced Active SKU --
            // the same min_price IS NULL the old LEFT JOIN produced, so is_on_sale is unchanged.
            return $"""
                CREATE OR REPLACE VIEW product.storefront_catalogue_entry AS
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
                        ) ELSE pricing.min_price IS NOT NULL END AS is_on_sale,
                    rating.rating_average,
                    COALESCE(rating.rating_count, 0) AS rating_count,
                    COALESCE(sales.units_sold_90d, 0) AS units_sold_90d,
                    img.file_id AS primary_image_file_id,
                    img.alt_text AS primary_image_alt_text
                FROM product.product p
                {pricing}
                LEFT JOIN product.product_rating_summary rating ON rating.product_id = p."Id"
                LEFT JOIN product.product_sales_stats sales ON sales."Id" = p."Id"
                LEFT JOIN product.product_image img ON img.product_id = p."Id" AND img.sku_id IS NULL AND img.is_primary = true
                WHERE p.status = {ProductActive} AND p.listed_on_storefront = true;
                """;
        }
    }
}
