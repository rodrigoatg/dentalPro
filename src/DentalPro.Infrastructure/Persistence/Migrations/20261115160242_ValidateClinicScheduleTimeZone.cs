using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DentalPro.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ValidateClinicScheduleTimeZone : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddCheckConstraint(
                name: "CK_ClinicSchedules_TimeZoneId_NotBlank",
                table: "ClinicSchedules",
                sql: "length(btrim(\"TimeZoneId\")) > 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_ClinicSchedules_TimeZoneId_NotBlank",
                table: "ClinicSchedules");
        }
    }
}
