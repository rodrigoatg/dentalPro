using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DentalPro.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddClinicScheduleAndAppointmentDuration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DurationMinutes",
                table: "Appointments",
                type: "integer",
                nullable: true);

            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1
                        FROM "Appointments"
                        WHERE EXTRACT(EPOCH FROM ("EndDateTime" - "StartDateTime")) <= 0
                           OR EXTRACT(EPOCH FROM ("EndDateTime" - "StartDateTime")) > 2147483647::numeric * 60
                           OR MOD(EXTRACT(EPOCH FROM ("EndDateTime" - "StartDateTime")), 60) <> 0
                    ) THEN
                        RAISE EXCEPTION 'Cannot backfill Appointment.DurationMinutes: an existing interval is not a positive whole number of minutes.';
                    END IF;
                END $$;
                UPDATE "Appointments"
                SET "DurationMinutes" = (EXTRACT(EPOCH FROM ("EndDateTime" - "StartDateTime")) / 60)::integer;
                """);

            migrationBuilder.AlterColumn<int>(
                name: "DurationMinutes",
                table: "Appointments",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.CreateTable(
                name: "ClinicSchedules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SingletonKey = table.Column<short>(type: "smallint", nullable: false),
                    TimeZoneId = table.Column<string>(type: "text", nullable: false),
                    DefaultAppointmentDurationMinutes = table.Column<int>(type: "integer", nullable: false),
                    StartIncrementMinutes = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClinicSchedules", x => x.Id);
                    table.CheckConstraint("CK_ClinicSchedules_DefaultDuration_Positive", "\"DefaultAppointmentDurationMinutes\" > 0");
                    table.CheckConstraint("CK_ClinicSchedules_Singleton", "\"SingletonKey\" = 1");
                    table.CheckConstraint("CK_ClinicSchedules_StartIncrement_Positive", "\"StartIncrementMinutes\" > 0");
                });

            migrationBuilder.CreateTable(
                name: "ClinicScheduleExceptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ClinicScheduleId = table.Column<Guid>(type: "uuid", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClinicScheduleExceptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClinicScheduleExceptions_ClinicSchedules_ClinicScheduleId",
                        column: x => x.ClinicScheduleId,
                        principalTable: "ClinicSchedules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ClinicWeeklyIntervals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ClinicScheduleId = table.Column<Guid>(type: "uuid", nullable: false),
                    DayOfWeek = table.Column<short>(type: "smallint", nullable: false),
                    Start = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    End = table.Column<TimeOnly>(type: "time without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClinicWeeklyIntervals", x => x.Id);
                    table.CheckConstraint("CK_ClinicWeeklyIntervals_DayOfWeek", "\"DayOfWeek\" BETWEEN 0 AND 6");
                    table.CheckConstraint("CK_ClinicWeeklyIntervals_StartBeforeEnd", "\"Start\" < \"End\"");
                    table.ForeignKey(
                        name: "FK_ClinicWeeklyIntervals_ClinicSchedules_ClinicScheduleId",
                        column: x => x.ClinicScheduleId,
                        principalTable: "ClinicSchedules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ClinicScheduleExceptionIntervals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ClinicScheduleDateExceptionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Start = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    End = table.Column<TimeOnly>(type: "time without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClinicScheduleExceptionIntervals", x => x.Id);
                    table.CheckConstraint("CK_ClinicScheduleExceptionIntervals_StartBeforeEnd", "\"Start\" < \"End\"");
                    table.ForeignKey(
                        name: "FK_ClinicScheduleExceptionIntervals_ClinicScheduleExceptions_ExceptionId",
                        column: x => x.ClinicScheduleDateExceptionId,
                        principalTable: "ClinicScheduleExceptions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.Sql("""
                ALTER TABLE "ClinicWeeklyIntervals"
                ADD CONSTRAINT "EX_ClinicWeeklyIntervals_NoOverlap"
                EXCLUDE USING gist (
                    "ClinicScheduleId" WITH =,
                    "DayOfWeek" WITH =,
                    (tsrange('2000-01-01'::date + "Start", '2000-01-01'::date + "End", '[)')) WITH &&
                ) DEFERRABLE INITIALLY DEFERRED;

                ALTER TABLE "ClinicScheduleExceptionIntervals"
                ADD CONSTRAINT "EX_ClinicScheduleExceptionIntervals_NoOverlap"
                EXCLUDE USING gist (
                    "ClinicScheduleDateExceptionId" WITH =,
                    (tsrange('2000-01-01'::date + "Start", '2000-01-01'::date + "End", '[)')) WITH &&
                ) DEFERRABLE INITIALLY DEFERRED;
                """);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Appointments_DurationMatchesInterval",
                table: "Appointments",
                sql: "\"EndDateTime\" = \"StartDateTime\" + make_interval(mins => \"DurationMinutes\")");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Appointments_DurationMinutes_Positive",
                table: "Appointments",
                sql: "\"DurationMinutes\" > 0");

            migrationBuilder.CreateIndex(
                name: "IX_ClinicScheduleExceptionIntervals_Exception_Start",
                table: "ClinicScheduleExceptionIntervals",
                columns: new[] { "ClinicScheduleDateExceptionId", "Start" });

            migrationBuilder.CreateIndex(
                name: "UX_ClinicScheduleExceptions_Schedule_Date",
                table: "ClinicScheduleExceptions",
                columns: new[] { "ClinicScheduleId", "Date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_ClinicSchedules_SingletonKey",
                table: "ClinicSchedules",
                column: "SingletonKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ClinicWeeklyIntervals_Schedule_Day_Start",
                table: "ClinicWeeklyIntervals",
                columns: new[] { "ClinicScheduleId", "DayOfWeek", "Start" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ClinicScheduleExceptionIntervals");

            migrationBuilder.DropTable(
                name: "ClinicWeeklyIntervals");

            migrationBuilder.DropTable(
                name: "ClinicScheduleExceptions");

            migrationBuilder.DropTable(
                name: "ClinicSchedules");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Appointments_DurationMatchesInterval",
                table: "Appointments");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Appointments_DurationMinutes_Positive",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "DurationMinutes",
                table: "Appointments");
        }
    }
}
