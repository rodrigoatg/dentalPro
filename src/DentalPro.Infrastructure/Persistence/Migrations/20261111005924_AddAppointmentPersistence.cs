using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DentalPro.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAppointmentPersistence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Appointments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PatientId = table.Column<Guid>(type: "uuid", nullable: false),
                    DentistId = table.Column<Guid>(type: "uuid", nullable: false),
                    StartDateTime = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    EndDateTime = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    RescheduledFromAppointmentId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Appointments", x => x.Id);
                    table.CheckConstraint("CK_Appointments_EndAfterStart", "\"StartDateTime\" < \"EndDateTime\"");
                    table.CheckConstraint("CK_Appointments_RescheduledFrom_Valid", "\"RescheduledFromAppointmentId\" IS NULL OR (\"RescheduledFromAppointmentId\" <> '00000000-0000-0000-0000-000000000000' AND \"RescheduledFromAppointmentId\" <> \"Id\")");
                    table.CheckConstraint("CK_Appointments_Status", "\"Status\" IN ('Scheduled', 'Confirmed', 'Completed', 'Cancelled', 'Rescheduled')");
                    table.ForeignKey(
                        name: "FK_Appointments_Appointments_RescheduledFromAppointmentId",
                        column: x => x.RescheduledFromAppointmentId,
                        principalTable: "Appointments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Appointments_AspNetUsers_DentistId",
                        column: x => x.DentistId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Appointments_Patients_PatientId",
                        column: x => x.PatientId,
                        principalTable: "Patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_DentistId_StartDateTime",
                table: "Appointments",
                columns: new[] { "DentistId", "StartDateTime" });

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_PatientId_StartDateTime",
                table: "Appointments",
                columns: new[] { "PatientId", "StartDateTime" });

            migrationBuilder.CreateIndex(
                name: "UX_Appointments_RescheduledFromAppointmentId",
                table: "Appointments",
                column: "RescheduledFromAppointmentId",
                unique: true,
                filter: "\"RescheduledFromAppointmentId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Appointments");
        }
    }
}
