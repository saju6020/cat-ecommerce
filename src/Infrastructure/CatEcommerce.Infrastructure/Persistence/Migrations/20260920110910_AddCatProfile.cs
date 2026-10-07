using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CatEcommerce.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCatProfile : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "audit");

            migrationBuilder.EnsureSchema(
                name: "clinic");

            migrationBuilder.CreateTable(
                name: "audit_entry",
                schema: "audit",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EntityType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    EntityId = table.Column<Guid>(type: "uuid", nullable: false),
                    Action = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    OccurredOn = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    BeforeValue = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    AfterValue = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
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
                    table.PrimaryKey("PK_audit_entry", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "cat",
                schema: "clinic",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Species = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Sex = table.Column<int>(type: "integer", nullable: false),
                    DateOfBirth = table.Column<DateOnly>(type: "date", nullable: true),
                    DateOfBirthPrecision = table.Column<int>(type: "integer", nullable: false),
                    Breed = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ColourAndMarkings = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    NeuterStatus = table.Column<int>(type: "integer", nullable: false),
                    NeuteredOn = table.Column<DateOnly>(type: "date", nullable: true),
                    DietaryNotes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    BehaviouralNotes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    IsHighRisk = table.Column<bool>(type: "boolean", nullable: false),
                    HandlingNote = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    LifeStatus = table.Column<int>(type: "integer", nullable: false),
                    DeceasedOn = table.Column<DateOnly>(type: "date", nullable: true),
                    DeceasedNote = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
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
                    table.PrimaryKey("PK_cat", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "cat_weight_record",
                schema: "clinic",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CatId = table.Column<Guid>(type: "uuid", nullable: false),
                    weight_kg = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    RecordedOn = table.Column<DateOnly>(type: "date", nullable: false),
                    RecordedByStaffId = table.Column<Guid>(type: "uuid", nullable: false),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
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
                    table.PrimaryKey("PK_cat_weight_record", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "vaccine",
                schema: "clinic",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    IsCore = table.Column<bool>(type: "boolean", nullable: false),
                    ValidityMonths = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
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
                    table.PrimaryKey("PK_vaccine", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "cat_allergy",
                schema: "clinic",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Allergen = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Reaction = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Severity = table.Column<int>(type: "integer", nullable: false),
                    RecordedOn = table.Column<DateOnly>(type: "date", nullable: false),
                    cat_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cat_allergy", x => x.Id);
                    table.ForeignKey(
                        name: "FK_cat_allergy_cat_cat_id",
                        column: x => x.cat_id,
                        principalSchema: "clinic",
                        principalTable: "cat",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "cat_chronic_condition",
                schema: "clinic",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Condition = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    DiagnosedOn = table.Column<DateOnly>(type: "date", nullable: true),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    cat_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cat_chronic_condition", x => x.Id);
                    table.ForeignKey(
                        name: "FK_cat_chronic_condition_cat_cat_id",
                        column: x => x.cat_id,
                        principalSchema: "clinic",
                        principalTable: "cat",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "cat_medication",
                schema: "clinic",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MedicationName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Dosage = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Frequency = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    StartedOn = table.Column<DateOnly>(type: "date", nullable: false),
                    EndsOn = table.Column<DateOnly>(type: "date", nullable: true),
                    cat_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cat_medication", x => x.Id);
                    table.ForeignKey(
                        name: "FK_cat_medication_cat_cat_id",
                        column: x => x.cat_id,
                        principalSchema: "clinic",
                        principalTable: "cat",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "cat_parasite_treatment",
                schema: "clinic",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TreatmentType = table.Column<int>(type: "integer", nullable: false),
                    ProductName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    BatchReference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    AdministeredOn = table.Column<DateOnly>(type: "date", nullable: false),
                    AdministeredByStaffId = table.Column<Guid>(type: "uuid", nullable: false),
                    NextDueOn = table.Column<DateOnly>(type: "date", nullable: false),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    cat_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cat_parasite_treatment", x => x.Id);
                    table.ForeignKey(
                        name: "FK_cat_parasite_treatment_cat_cat_id",
                        column: x => x.cat_id,
                        principalSchema: "clinic",
                        principalTable: "cat",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "cat_photo",
                schema: "clinic",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StorageKey = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SizeInBytes = table.Column<long>(type: "bigint", nullable: false),
                    FileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    IsPrimary = table.Column<bool>(type: "boolean", nullable: false),
                    UploadedOn = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    cat_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cat_photo", x => x.Id);
                    table.ForeignKey(
                        name: "FK_cat_photo_cat_cat_id",
                        column: x => x.cat_id,
                        principalSchema: "clinic",
                        principalTable: "cat",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "cat_vaccination",
                schema: "clinic",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    VaccineId = table.Column<Guid>(type: "uuid", nullable: false),
                    VaccineCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    VaccineName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    IsCore = table.Column<bool>(type: "boolean", nullable: false),
                    BatchReference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    BatchExpiresOn = table.Column<DateOnly>(type: "date", nullable: false),
                    AdministeredOn = table.Column<DateOnly>(type: "date", nullable: false),
                    AdministeredByVetId = table.Column<Guid>(type: "uuid", nullable: false),
                    NextDueOn = table.Column<DateOnly>(type: "date", nullable: false),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    cat_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cat_vaccination", x => x.Id);
                    table.ForeignKey(
                        name: "FK_cat_vaccination_cat_cat_id",
                        column: x => x.cat_id,
                        principalSchema: "clinic",
                        principalTable: "cat",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_audit_entry_EntityType_EntityId_OccurredOn",
                schema: "audit",
                table: "audit_entry",
                columns: new[] { "EntityType", "EntityId", "OccurredOn" });

            migrationBuilder.CreateIndex(
                name: "IX_cat_LifeStatus",
                schema: "clinic",
                table: "cat",
                column: "LifeStatus");

            migrationBuilder.CreateIndex(
                name: "IX_cat_OwnerId",
                schema: "clinic",
                table: "cat",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_cat_allergy_cat_id",
                schema: "clinic",
                table: "cat_allergy",
                column: "cat_id");

            migrationBuilder.CreateIndex(
                name: "IX_cat_chronic_condition_cat_id",
                schema: "clinic",
                table: "cat_chronic_condition",
                column: "cat_id");

            migrationBuilder.CreateIndex(
                name: "IX_cat_medication_cat_id",
                schema: "clinic",
                table: "cat_medication",
                column: "cat_id");

            migrationBuilder.CreateIndex(
                name: "IX_cat_parasite_treatment_cat_id",
                schema: "clinic",
                table: "cat_parasite_treatment",
                column: "cat_id");

            migrationBuilder.CreateIndex(
                name: "IX_cat_photo_cat_id",
                schema: "clinic",
                table: "cat_photo",
                column: "cat_id");

            migrationBuilder.CreateIndex(
                name: "IX_cat_vaccination_cat_id",
                schema: "clinic",
                table: "cat_vaccination",
                column: "cat_id");

            migrationBuilder.CreateIndex(
                name: "IX_cat_vaccination_NextDueOn",
                schema: "clinic",
                table: "cat_vaccination",
                column: "NextDueOn");

            migrationBuilder.CreateIndex(
                name: "IX_cat_weight_record_CatId_RecordedOn",
                schema: "clinic",
                table: "cat_weight_record",
                columns: new[] { "CatId", "RecordedOn" });

            migrationBuilder.CreateIndex(
                name: "IX_vaccine_Code",
                schema: "clinic",
                table: "vaccine",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_vaccine_IsCore",
                schema: "clinic",
                table: "vaccine",
                column: "IsCore");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "audit_entry",
                schema: "audit");

            migrationBuilder.DropTable(
                name: "cat_allergy",
                schema: "clinic");

            migrationBuilder.DropTable(
                name: "cat_chronic_condition",
                schema: "clinic");

            migrationBuilder.DropTable(
                name: "cat_medication",
                schema: "clinic");

            migrationBuilder.DropTable(
                name: "cat_parasite_treatment",
                schema: "clinic");

            migrationBuilder.DropTable(
                name: "cat_photo",
                schema: "clinic");

            migrationBuilder.DropTable(
                name: "cat_vaccination",
                schema: "clinic");

            migrationBuilder.DropTable(
                name: "cat_weight_record",
                schema: "clinic");

            migrationBuilder.DropTable(
                name: "vaccine",
                schema: "clinic");

            migrationBuilder.DropTable(
                name: "cat",
                schema: "clinic");
        }
    }
}
