using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CatEcommerce.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SeedDeliveryTaxClass : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // HO-ORD-001-FU1 D3 (owner decision 2026-10-10): the delivery charge's tax class, seeded at 0 %. An Administrator changes the delivery tax by adding an
            // effective-dated rate to the DELIVERY class in the existing tax configuration. Data only -- no schema change. Idempotent: an existing DELIVERY class is kept.
            migrationBuilder.Sql(@"
INSERT INTO admin.tax_class (""Id"", code, name, is_active, ""CreatedBy"", ""CreatedDate"", ""Language"", ""LastUpdatedDate"", ""LastUpdatedBy"", ""TenantId"", ""Tags"",
                             ""VerticalId"", ""ServiceId"", ""IsMarkedToDelete"", ""Version"")
SELECT 'd31e0a7c-5b1f-4c6e-9a42-0d0e11f00001', 'DELIVERY', 'Delivery charge', TRUE, '00000000-0000-0000-0000-000000000000', now(), NULL, now(),
       '00000000-0000-0000-0000-000000000000', '00000000-0000-0000-0000-000000000000', NULL, '00000000-0000-0000-0000-000000000000', NULL, FALSE, 1
WHERE NOT EXISTS (SELECT 1 FROM admin.tax_class WHERE code = 'DELIVERY');

INSERT INTO admin.tax_rate (""Id"", tax_class_id, rate_percent, effective_from, ""CreatedBy"", ""CreatedDate"", ""Language"", ""LastUpdatedDate"", ""LastUpdatedBy"", ""TenantId"", ""Tags"",
                            ""VerticalId"", ""ServiceId"", ""IsMarkedToDelete"", ""Version"")
SELECT 'd31e0a7c-5b1f-4c6e-9a42-0d0e11f00002', c.""Id"", 0, TIMESTAMPTZ '2000-01-01 00:00:00+00', '00000000-0000-0000-0000-000000000000', now(), NULL, now(),
       '00000000-0000-0000-0000-000000000000', '00000000-0000-0000-0000-000000000000', NULL, '00000000-0000-0000-0000-000000000000', NULL, FALSE, 1
FROM admin.tax_class c
WHERE c.code = 'DELIVERY' AND NOT EXISTS (SELECT 1 FROM admin.tax_rate r WHERE r.tax_class_id = c.""Id"");
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The seeded class may by now carry invoices' history (a rate an Administrator changed): never delete it on a downgrade.
        }
    }
}
