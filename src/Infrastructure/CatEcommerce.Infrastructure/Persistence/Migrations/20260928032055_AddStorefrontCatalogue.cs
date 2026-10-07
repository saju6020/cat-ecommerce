using CatEcommerce.Shared.Common.Enums;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CatEcommerce.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// HO-SHP-001 section 12.1 Phase E -- pg_trgm, the trigram index and the three storefront
    /// views. Status values in the view SQL and the trigram predicate are read from the
    /// enumeration constants below (ProductStatus.Active.Value etc.), never typed as literals
    /// (U-X5): C# string interpolation resolves them when this migration runs, against whatever
    /// this build's enumeration values actually are.
    ///
    /// <c>in_stock</c> is computed as "has an Active SKU with a current price" -- see
    /// HO-SHP-001.md section 1 Amended (2026-09-28): HO-IMP-001's inventory.v_sellable_stock does
    /// not exist in this worktree (not a section 5 prerequisite of this slice), so the view cannot
    /// join it. This is a documented, temporary approximation for the availability filter/sort
    /// only.
    /// </summary>
    public partial class AddStorefrontCatalogue : Migration
    {
        private static readonly int ProductActive = ProductStatus.Active.Value;
        private static readonly int SkuActive = SkuStatus.Active.Value;
        private static readonly int ReviewPublished = ProductReviewStatus.Published.Value;

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS pg_trgm;");

            migrationBuilder.Sql(
                "CREATE INDEX ix_product_search_text_trgm ON product.product USING gin (search_text gin_trgm_ops);");

            migrationBuilder.Sql($"""
                CREATE VIEW product.product_rating_summary AS
                SELECT
                    product_id,
                    ROUND(AVG(rating)::numeric, 1) AS rating_average,
                    COUNT(*)::int AS rating_count
                FROM product.product_review
                WHERE status = {ReviewPublished}
                GROUP BY product_id;
                """);

            migrationBuilder.Sql($"""
                CREATE VIEW product.catalogue_term AS
                SELECT DISTINCT LOWER(brand) AS term
                FROM product.product
                WHERE status = {ProductActive} AND listed_on_storefront = true
                UNION
                SELECT DISTINCT LOWER(name) AS term
                FROM product.product
                WHERE status = {ProductActive} AND listed_on_storefront = true;
                """);

            migrationBuilder.Sql($"""
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
                    EXISTS (
                        SELECT 1
                        FROM product.sku s2
                        JOIN product.sku_price sp2 ON sp2.sku_id = s2."Id" AND sp2.effective_from <= now()
                        WHERE s2.product_id = p."Id" AND s2.status = {SkuActive}
                    ) AS in_stock,
                    rating.rating_average,
                    COALESCE(rating.rating_count, 0) AS rating_count,
                    COALESCE(sales.units_sold_90d, 0) AS units_sold_90d,
                    img.file_id AS primary_image_file_id,
                    img.alt_text AS primary_image_alt_text
                FROM product.product p
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
                LEFT JOIN product.product_rating_summary rating ON rating.product_id = p."Id"
                LEFT JOIN product.product_sales_stats sales ON sales."Id" = p."Id"
                LEFT JOIN product.product_image img ON img.product_id = p."Id" AND img.sku_id IS NULL AND img.is_primary = true
                WHERE p.status = {ProductActive} AND p.listed_on_storefront = true;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Drops the views and the trigram index, but never the extension itself (section 12.1:
            // "Down drops the views and index but not the extension").
            migrationBuilder.Sql("DROP VIEW IF EXISTS product.storefront_catalogue_entry;");
            migrationBuilder.Sql("DROP VIEW IF EXISTS product.catalogue_term;");
            migrationBuilder.Sql("DROP VIEW IF EXISTS product.product_rating_summary;");
            migrationBuilder.Sql("DROP INDEX IF EXISTS product.ix_product_search_text_trgm;");
        }
    }
}
