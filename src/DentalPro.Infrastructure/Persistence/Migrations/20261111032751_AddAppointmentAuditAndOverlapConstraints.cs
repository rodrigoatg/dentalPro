using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DentalPro.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAppointmentAuditAndOverlapConstraints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AuditEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    EntityType = table.Column<string>(type: "text", nullable: false),
                    EntityId = table.Column<Guid>(type: "uuid", nullable: false),
                    Action = table.Column<string>(type: "text", nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    RelatedEntityId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AuditEvents_AspNetUsers_ActorId",
                        column: x => x.ActorId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AuditEvents_ActorId_OccurredAt",
                table: "AuditEvents",
                columns: new[] { "ActorId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditEvents_EntityType_EntityId_OccurredAt",
                table: "AuditEvents",
                columns: new[] { "EntityType", "EntityId", "OccurredAt" });

            migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS btree_gist;");

            migrationBuilder.Sql(
                "ALTER TABLE \"Appointments\" " +
                "ADD CONSTRAINT \"EX_Appointments_Dentist_NoOverlap\" " +
                "EXCLUDE USING gist (\"DentistId\" WITH =, " +
                "tstzrange(\"StartDateTime\", \"EndDateTime\", '[)') WITH &&) " +
                "WHERE (\"Status\" IN ('Scheduled', 'Confirmed', 'Completed')) " +
                "DEFERRABLE INITIALLY DEFERRED;");

            migrationBuilder.Sql(
                "ALTER TABLE \"Appointments\" " +
                "ADD CONSTRAINT \"EX_Appointments_Patient_NoOverlap\" " +
                "EXCLUDE USING gist (\"PatientId\" WITH =, " +
                "tstzrange(\"StartDateTime\", \"EndDateTime\", '[)') WITH &&) " +
                "WHERE (\"Status\" IN ('Scheduled', 'Confirmed', 'Completed')) " +
                "DEFERRABLE INITIALLY DEFERRED;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "ALTER TABLE \"Appointments\" DROP CONSTRAINT \"EX_Appointments_Patient_NoOverlap\";");
            migrationBuilder.Sql(
                "ALTER TABLE \"Appointments\" DROP CONSTRAINT \"EX_Appointments_Dentist_NoOverlap\";");

            migrationBuilder.DropTable(
                name: "AuditEvents");
        }
    }
}
