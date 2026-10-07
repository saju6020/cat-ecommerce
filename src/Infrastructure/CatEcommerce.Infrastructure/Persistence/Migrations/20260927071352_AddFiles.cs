using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CatEcommerce.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddFiles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Gate-fix 2026-09-27 (ARCHITECTURE MEDIUM, second pass): every pre-existing cat_photo
            // row was written before HO-FIL-001 existed, under the client-trusted StorageKey/
            // ContentType/SizeInBytes/FileName shape this migration replaces (the very defect
            // HO-CAT-002-SECURITY carried and this handover closes). None of those rows has a real
            // files.stored_file row to point at, so there is no FileId to migrate them to -- adding
            // FileId NOT NULL with a Guid.Empty default would silently turn every one into a photo
            // that points at nothing (and the read already hides a row whose FileId has no matching
            // StoredFile, so this would fail invisibly), and if any cat ever had two or more photos,
            // the unique index below would refuse to build at all. They are deleted explicitly
            // instead: a customer or clinic that had cat photos before this branch will see the
            // photo list empty until photos are re-uploaded through the new, server-issued-FileId
            // flow -- a one-time, visible loss on upgrade, not a silent one. See HO-FIL-001.md §1
            // Amended for the record of which environments this affects.
            migrationBuilder.Sql("DELETE FROM clinic.cat_photo;");

            migrationBuilder.DropColumn(
                name: "ContentType",
                schema: "clinic",
                table: "cat_photo");

            migrationBuilder.DropColumn(
                name: "FileName",
                schema: "clinic",
                table: "cat_photo");

            migrationBuilder.DropColumn(
                name: "SizeInBytes",
                schema: "clinic",
                table: "cat_photo");

            migrationBuilder.DropColumn(
                name: "StorageKey",
                schema: "clinic",
                table: "cat_photo");

            migrationBuilder.EnsureSchema(
                name: "files");

            migrationBuilder.AddColumn<Guid>(
                name: "FileId",
                schema: "clinic",
                table: "cat_photo",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateTable(
                name: "stored_file",
                schema: "files",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Purpose = table.Column<int>(type: "integer", nullable: false),
                    ContentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SizeInBytes = table.Column<long>(type: "bigint", nullable: false),
                    Sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    OriginalFileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    StorageKey = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    UploadedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    UploadedOn = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    ScannerName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    RetainUntil = table.Column<DateOnly>(type: "date", nullable: true),
                    LegalHold = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("PK_stored_file", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_cat_photo_file_id",
                schema: "clinic",
                table: "cat_photo",
                column: "FileId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_stored_file_Purpose_UploadedOn",
                schema: "files",
                table: "stored_file",
                columns: new[] { "Purpose", "UploadedOn" });

            migrationBuilder.CreateIndex(
                name: "IX_stored_file_Sha256",
                schema: "files",
                table: "stored_file",
                column: "Sha256");

            migrationBuilder.CreateIndex(
                name: "IX_stored_file_UploadedBy",
                schema: "files",
                table: "stored_file",
                column: "UploadedBy");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "stored_file",
                schema: "files");

            migrationBuilder.DropIndex(
                name: "ix_cat_photo_file_id",
                schema: "clinic",
                table: "cat_photo");

            migrationBuilder.DropColumn(
                name: "FileId",
                schema: "clinic",
                table: "cat_photo");

            migrationBuilder.AddColumn<string>(
                name: "ContentType",
                schema: "clinic",
                table: "cat_photo",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "FileName",
                schema: "clinic",
                table: "cat_photo",
                type: "character varying(255)",
                maxLength: 255,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<long>(
                name: "SizeInBytes",
                schema: "clinic",
                table: "cat_photo",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<string>(
                name: "StorageKey",
                schema: "clinic",
                table: "cat_photo",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "");
        }
    }
}
