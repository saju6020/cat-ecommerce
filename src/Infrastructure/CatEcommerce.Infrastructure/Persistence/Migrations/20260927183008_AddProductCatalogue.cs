using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CatEcommerce.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProductCatalogue : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "product");

            migrationBuilder.CreateTable(
                name: "product",
                schema: "product",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<int>(type: "integer", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    brand = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    description = table.Column<string>(type: "character varying(5000)", maxLength: 5000, nullable: false),
                    category = table.Column<int>(type: "integer", nullable: false),
                    subcategory = table.Column<int>(type: "integer", nullable: true),
                    slug = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    first_published_on = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    tax_class_id = table.Column<Guid>(type: "uuid", nullable: false),
                    country_of_origin_code = table.Column<string>(type: "character(2)", fixedLength: true, maxLength: 2, nullable: true),
                    prescription_required = table.Column<bool>(type: "boolean", nullable: false),
                    is_expiry_tracked = table.Column<bool>(type: "boolean", nullable: false),
                    listed_on_storefront = table.Column<bool>(type: "boolean", nullable: false),
                    search_text = table.Column<string>(type: "text", nullable: false),
                    food_manufacturer = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    food_life_stage = table.Column<int>(type: "integer", nullable: true),
                    food_protein_source = table.Column<int>(type: "integer", nullable: true),
                    food_ingredient_list = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    food_analysis_protein_percent = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    food_analysis_fat_percent = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    food_analysis_fibre_percent = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    food_analysis_moisture_percent = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    food_feeding_guide = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    food_storage_instructions = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    tags = table.Column<string[]>(type: "text[]", nullable: false),
                    health_focus = table.Column<int[]>(type: "integer[]", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Language = table.Column<string>(type: "text", nullable: true),
                    LastUpdatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastUpdatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    VerticalId = table.Column<Guid>(type: "uuid", nullable: false),
                    ServiceId = table.Column<string>(type: "text", nullable: true),
                    IsMarkedToDelete = table.Column<bool>(type: "boolean", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_product", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "sku_price",
                schema: "product",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    sku_id = table.Column<Guid>(type: "uuid", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    currency_code = table.Column<string>(type: "char(3)", nullable: false),
                    effective_from = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    set_by = table.Column<Guid>(type: "uuid", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Language = table.Column<string>(type: "text", nullable: true),
                    LastUpdatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastUpdatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Tags = table.Column<string[]>(type: "text[]", nullable: true),
                    VerticalId = table.Column<Guid>(type: "uuid", nullable: false),
                    ServiceId = table.Column<string>(type: "text", nullable: true),
                    IsMarkedToDelete = table.Column<bool>(type: "boolean", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sku_price", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "bundle_component",
                schema: "product",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    component_sku_id = table.Column<Guid>(type: "uuid", nullable: false),
                    quantity = table.Column<int>(type: "integer", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bundle_component", x => x.Id);
                    table.ForeignKey(
                        name: "FK_bundle_component_product_product_id",
                        column: x => x.product_id,
                        principalSchema: "product",
                        principalTable: "product",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "product_image",
                schema: "product",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    sku_id = table.Column<Guid>(type: "uuid", nullable: true),
                    file_id = table.Column<Guid>(type: "uuid", nullable: false),
                    alt_text = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    is_primary = table.Column<bool>(type: "boolean", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_product_image", x => x.Id);
                    table.ForeignKey(
                        name: "FK_product_image_product_product_id",
                        column: x => x.product_id,
                        principalSchema: "product",
                        principalTable: "product",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "product_translation",
                schema: "product",
                columns: table => new
                {
                    locale = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    description = table.Column<string>(type: "character varying(5000)", maxLength: 5000, nullable: true),
                    ingredient_list = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    feeding_guide = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_product_translation", x => new { x.locale, x.product_id });
                    table.ForeignKey(
                        name: "FK_product_translation_product_product_id",
                        column: x => x.product_id,
                        principalSchema: "product",
                        principalTable: "product",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "related_product",
                schema: "product",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    related_product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<int>(type: "integer", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_related_product", x => x.Id);
                    table.ForeignKey(
                        name: "FK_related_product_product_product_id",
                        column: x => x.product_id,
                        principalSchema: "product",
                        principalTable: "product",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "sku",
                schema: "product",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    barcode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    size_or_weight = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    flavour = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    colour = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    pack_quantity = table.Column<int>(type: "integer", nullable: true),
                    status = table.Column<int>(type: "integer", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sku", x => x.Id);
                    table.ForeignKey(
                        name: "FK_sku_product_product_id",
                        column: x => x.product_id,
                        principalSchema: "product",
                        principalTable: "product",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_bundle_component_component_sku_id",
                schema: "product",
                table: "bundle_component",
                column: "component_sku_id");

            migrationBuilder.CreateIndex(
                name: "IX_bundle_component_product_id",
                schema: "product",
                table: "bundle_component",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "ix_product_brand",
                schema: "product",
                table: "product",
                column: "brand");

            migrationBuilder.CreateIndex(
                name: "ix_product_country_of_origin",
                schema: "product",
                table: "product",
                column: "country_of_origin_code");

            migrationBuilder.CreateIndex(
                name: "ix_product_first_published_on_desc",
                schema: "product",
                table: "product",
                column: "first_published_on");

            migrationBuilder.CreateIndex(
                name: "ix_product_slug",
                schema: "product",
                table: "product",
                column: "slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_product_status_listed_category_subcategory",
                schema: "product",
                table: "product",
                columns: new[] { "status", "listed_on_storefront", "category", "subcategory" });

            migrationBuilder.CreateIndex(
                name: "IX_product_image_product_id",
                schema: "product",
                table: "product_image",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "IX_product_translation_product_id",
                schema: "product",
                table: "product_translation",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "IX_related_product_product_id",
                schema: "product",
                table: "related_product",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "ix_sku_barcode",
                schema: "product",
                table: "sku",
                column: "barcode",
                unique: true,
                filter: "barcode IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_sku_code",
                schema: "product",
                table: "sku",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_sku_id",
                schema: "product",
                table: "sku",
                column: "Id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_sku_product_status",
                schema: "product",
                table: "sku",
                columns: new[] { "product_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_sku_price_sku_effective",
                schema: "product",
                table: "sku_price",
                columns: new[] { "sku_id", "effective_from" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "bundle_component",
                schema: "product");

            migrationBuilder.DropTable(
                name: "product_image",
                schema: "product");

            migrationBuilder.DropTable(
                name: "product_translation",
                schema: "product");

            migrationBuilder.DropTable(
                name: "related_product",
                schema: "product");

            migrationBuilder.DropTable(
                name: "sku",
                schema: "product");

            migrationBuilder.DropTable(
                name: "sku_price",
                schema: "product");

            migrationBuilder.DropTable(
                name: "product",
                schema: "product");
        }
    }
}
