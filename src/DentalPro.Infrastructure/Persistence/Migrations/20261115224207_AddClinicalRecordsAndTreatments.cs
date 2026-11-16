using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DentalPro.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddClinicalRecordsAndTreatments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ClinicalEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MedicalRecordId = table.Column<Guid>(type: "uuid", nullable: false),
                    DentistId = table.Column<Guid>(type: "uuid", nullable: false),
                    AppointmentId = table.Column<Guid>(type: "uuid", nullable: true),
                    Type = table.Column<string>(type: "text", nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    Diagnosis = table.Column<string>(type: "text", nullable: true),
                    Notes = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClinicalEntries", x => x.Id);
                    table.CheckConstraint("CK_ClinicalEntries_Type", "\"Type\" IN ('Consultation','Treatment','Observation','FollowUp','ExternalRecord','Other')");
                    table.ForeignKey(
                        name: "FK_ClinicalEntries_Appointments_AppointmentId",
                        column: x => x.AppointmentId,
                        principalTable: "Appointments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ClinicalEntries_AspNetUsers_DentistId",
                        column: x => x.DentistId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ClinicalEntries_MedicalRecords_MedicalRecordId",
                        column: x => x.MedicalRecordId,
                        principalTable: "MedicalRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Treatments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MedicalRecordId = table.Column<Guid>(type: "uuid", nullable: false),
                    DentistId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "text", nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    RecordedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    Priority = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    IsFinalized = table.Column<bool>(type: "boolean", nullable: false),
                    FinalizedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    Cost = table.Column<decimal>(type: "numeric(12,2)", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Treatments", x => x.Id);
                    table.CheckConstraint("CK_Treatments_Cost_NonNegative", "\"Cost\" IS NULL OR \"Cost\" >= 0");
                    table.CheckConstraint("CK_Treatments_Priority", "\"Priority\" IN ('Low','Medium','High','Urgent')");
                    table.CheckConstraint("CK_Treatments_Status", "\"Status\" IN ('Planned','InProgress','PartiallyCompleted','Completed','Cancelled')");
                    table.CheckConstraint("CK_Treatments_Type", "\"Type\" IN ('Restoration','Extraction','RootCanal','Cleaning','Crown','Other')");
                    table.ForeignKey(
                        name: "FK_Treatments_AspNetUsers_DentistId",
                        column: x => x.DentistId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Treatments_MedicalRecords_MedicalRecordId",
                        column: x => x.MedicalRecordId,
                        principalTable: "MedicalRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ClinicalEntryVersions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ClinicalEntryId = table.Column<Guid>(type: "uuid", nullable: false),
                    VersionNumber = table.Column<int>(type: "integer", nullable: false),
                    MedicalRecordId = table.Column<Guid>(type: "uuid", nullable: false),
                    DentistId = table.Column<Guid>(type: "uuid", nullable: false),
                    AppointmentId = table.Column<Guid>(type: "uuid", nullable: true),
                    Type = table.Column<string>(type: "text", nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    Diagnosis = table.Column<string>(type: "text", nullable: true),
                    Notes = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedTransactionId = table.Column<long>(type: "bigint", nullable: false, defaultValueSql: "pg_current_xact_id()::text::bigint"),
                    ChangeReason = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClinicalEntryVersions", x => x.Id);
                    table.CheckConstraint("CK_ClinicalEntryVersions_Type", "\"Type\" IN ('Consultation','Treatment','Observation','FollowUp','ExternalRecord','Other')");
                    table.CheckConstraint("CK_ClinicalEntryVersions_VersionNumber_Positive", "\"VersionNumber\" > 0");
                    table.ForeignKey(
                        name: "FK_ClinicalEntryVersions_Appointments_AppointmentId",
                        column: x => x.AppointmentId,
                        principalTable: "Appointments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ClinicalEntryVersions_AspNetUsers_CreatedBy",
                        column: x => x.CreatedBy,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ClinicalEntryVersions_AspNetUsers_DentistId",
                        column: x => x.DentistId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ClinicalEntryVersions_ClinicalEntries_ClinicalEntryId",
                        column: x => x.ClinicalEntryId,
                        principalTable: "ClinicalEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ClinicalEntryVersions_MedicalRecords_MedicalRecordId",
                        column: x => x.MedicalRecordId,
                        principalTable: "MedicalRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ClinicalEntryTreatments",
                columns: table => new
                {
                    ClinicalEntryId = table.Column<Guid>(type: "uuid", nullable: false),
                    TreatmentId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClinicalEntryTreatments", x => new { x.ClinicalEntryId, x.TreatmentId });
                    table.ForeignKey(
                        name: "FK_ClinicalEntryTreatments_ClinicalEntries_ClinicalEntryId",
                        column: x => x.ClinicalEntryId,
                        principalTable: "ClinicalEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ClinicalEntryTreatments_Treatments_TreatmentId",
                        column: x => x.TreatmentId,
                        principalTable: "Treatments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TreatmentTeeth",
                columns: table => new
                {
                    TreatmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    ToothNumber = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    ResultingToothState = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TreatmentTeeth", x => new { x.TreatmentId, x.ToothNumber });
                    table.CheckConstraint("CK_TreatmentTeeth_FdiPermanent", "\"ToothNumber\" / 10 BETWEEN 1 AND 4 AND \"ToothNumber\" % 10 BETWEEN 1 AND 8");
                    table.CheckConstraint("CK_TreatmentTeeth_Status", "\"Status\" IN ('Planned','InProgress','Completed','Cancelled')");
                    table.CheckConstraint("CK_TreatmentTeeth_CompletedAt_Consistency", "(\"Status\" = 'Completed') = (\"CompletedAt\" IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_TreatmentTeeth_Treatments_TreatmentId",
                        column: x => x.TreatmentId,
                        principalTable: "Treatments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ClinicalEntryVersionTreatments",
                columns: table => new
                {
                    ClinicalEntryVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    TreatmentId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClinicalEntryVersionTreatments", x => new { x.ClinicalEntryVersionId, x.TreatmentId });
                    table.ForeignKey(
                        name: "FK_ClinicalEntryVersionTreatments_ClinicalEntryVersions_VersionId",
                        column: x => x.ClinicalEntryVersionId,
                        principalTable: "ClinicalEntryVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ClinicalEntryVersionTreatments_Treatments_TreatmentId",
                        column: x => x.TreatmentId,
                        principalTable: "Treatments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ClinicalEntries_AppointmentId",
                table: "ClinicalEntries",
                column: "AppointmentId");

            migrationBuilder.CreateIndex(
                name: "IX_ClinicalEntries_DentistId",
                table: "ClinicalEntries",
                column: "DentistId");

            migrationBuilder.CreateIndex(
                name: "IX_ClinicalEntries_MedicalRecordId_OccurredAt",
                table: "ClinicalEntries",
                columns: new[] { "MedicalRecordId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ClinicalEntryTreatments_TreatmentId",
                table: "ClinicalEntryTreatments",
                column: "TreatmentId");

            migrationBuilder.CreateIndex(
                name: "IX_ClinicalEntryVersions_AppointmentId",
                table: "ClinicalEntryVersions",
                column: "AppointmentId");

            migrationBuilder.CreateIndex(
                name: "IX_ClinicalEntryVersions_CreatedBy",
                table: "ClinicalEntryVersions",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_ClinicalEntryVersions_DentistId",
                table: "ClinicalEntryVersions",
                column: "DentistId");

            migrationBuilder.CreateIndex(
                name: "IX_ClinicalEntryVersions_MedicalRecordId",
                table: "ClinicalEntryVersions",
                column: "MedicalRecordId");

            migrationBuilder.CreateIndex(
                name: "UX_ClinicalEntryVersions_Entry_VersionNumber",
                table: "ClinicalEntryVersions",
                columns: new[] { "ClinicalEntryId", "VersionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ClinicalEntryVersionTreatments_TreatmentId",
                table: "ClinicalEntryVersionTreatments",
                column: "TreatmentId");

            migrationBuilder.CreateIndex(
                name: "IX_Treatments_DentistId",
                table: "Treatments",
                column: "DentistId");

            migrationBuilder.CreateIndex(
                name: "IX_Treatments_MedicalRecordId_OccurredAt",
                table: "Treatments",
                columns: new[] { "MedicalRecordId", "OccurredAt" });

            migrationBuilder.Sql("""
                CREATE FUNCTION "PreventClinicalEntryVersionMutation"()
                RETURNS trigger
                LANGUAGE plpgsql
                AS $$
                BEGIN
                    RAISE EXCEPTION 'Clinical entry versions are immutable.'
                        USING ERRCODE = '55000';
                END;
                $$;

                CREATE FUNCTION "PreventClinicalEntryVersionTreatmentMutation"()
                RETURNS trigger
                LANGUAGE plpgsql
                AS $$
                DECLARE
                    version_transaction_id bigint;
                BEGIN
                    IF TG_OP = 'INSERT' THEN
                        SELECT "CreatedTransactionId" INTO version_transaction_id
                        FROM "ClinicalEntryVersions"
                        WHERE "Id" = NEW."ClinicalEntryVersionId";

                        IF version_transaction_id = pg_current_xact_id()::text::bigint THEN
                            RETURN NEW;
                        END IF;
                    END IF;

                    RAISE EXCEPTION 'Clinical entry version treatment links are immutable.'
                        USING ERRCODE = '55000';
                END;
                $$;

                CREATE TRIGGER "TR_ClinicalEntryVersions_Immutable"
                BEFORE UPDATE OR DELETE ON "ClinicalEntryVersions"
                FOR EACH ROW EXECUTE FUNCTION "PreventClinicalEntryVersionMutation"();

                CREATE TRIGGER "TR_ClinicalEntryVersionTreatments_Immutable"
                BEFORE INSERT OR UPDATE OR DELETE ON "ClinicalEntryVersionTreatments"
                FOR EACH ROW EXECUTE FUNCTION "PreventClinicalEntryVersionTreatmentMutation"();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ClinicalEntryTreatments");

            migrationBuilder.DropTable(
                name: "ClinicalEntryVersionTreatments");

            migrationBuilder.DropTable(
                name: "TreatmentTeeth");

            migrationBuilder.DropTable(
                name: "ClinicalEntryVersions");

            migrationBuilder.DropTable(
                name: "Treatments");

            migrationBuilder.DropTable(
                name: "ClinicalEntries");

            migrationBuilder.Sql("DROP FUNCTION IF EXISTS \"PreventClinicalEntryVersionMutation\"();");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS \"PreventClinicalEntryVersionTreatmentMutation\"();");
        }
    }
}
