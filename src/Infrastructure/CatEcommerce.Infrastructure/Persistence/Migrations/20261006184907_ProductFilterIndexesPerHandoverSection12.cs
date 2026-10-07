using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CatEcommerce.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ProductFilterIndexesPerHandoverSection12 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_product_brand",
                schema: "product",
                table: "product");

            migrationBuilder.DropIndex(
                name: "ix_product_country_of_origin",
                schema: "product",
                table: "product");

            migrationBuilder.DropIndex(
                name: "ix_product_first_published_on_desc",
                schema: "product",
                table: "product");

            migrationBuilder.CreateIndex(
                name: "ix_product_brand",
                schema: "product",
                table: "product",
                column: "brand",
                filter: "status = 2");

            migrationBuilder.CreateIndex(
                name: "ix_product_country_of_origin",
                schema: "product",
                table: "product",
                column: "country_of_origin_code",
                filter: "status = 2");

            migrationBuilder.CreateIndex(
                name: "ix_product_first_published_on_desc",
                schema: "product",
                table: "product",
                column: "first_published_on",
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "ix_product_food_life_stage",
                schema: "product",
                table: "product",
                column: "food_life_stage",
                filter: "status = 2");

            migrationBuilder.CreateIndex(
                name: "ix_product_food_protein_source",
                schema: "product",
                table: "product",
                column: "food_protein_source",
                filter: "status = 2");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_product_brand",
                schema: "product",
                table: "product");

            migrationBuilder.DropIndex(
                name: "ix_product_country_of_origin",
                schema: "product",
                table: "product");

            migrationBuilder.DropIndex(
                name: "ix_product_first_published_on_desc",
                schema: "product",
                table: "product");

            migrationBuilder.DropIndex(
                name: "ix_product_food_life_stage",
                schema: "product",
                table: "product");

            migrationBuilder.DropIndex(
                name: "ix_product_food_protein_source",
                schema: "product",
                table: "product");

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
        }
    }
}
