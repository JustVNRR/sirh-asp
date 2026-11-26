using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sirh.Modules.Health.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialHealth : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "health");

            migrationBuilder.CreateTable(
                name: "medical_visits",
                schema: "health",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VisitType = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    VisitDateTimeUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ValidityDate = table.Column<DateOnly>(type: "date", nullable: true),
                    MedicalCenter = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CarriedOut = table.Column<bool>(type: "bit", nullable: false),
                    Comments = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ConvocationFileKey = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    ConvocationFileName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    ConvocationContentType = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    ConvocationFileSizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    ReportFileKey = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    ReportFileName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ReportContentType = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    ReportFileSizeBytes = table.Column<long>(type: "bigint", nullable: true),
                    ReminderSentAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_medical_visits", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_medical_visits_CarriedOut_ReminderSentAtUtc_VisitDateTimeUtc",
                schema: "health",
                table: "medical_visits",
                columns: new[] { "CarriedOut", "ReminderSentAtUtc", "VisitDateTimeUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_medical_visits_EmployeeId_VisitDateTimeUtc",
                schema: "health",
                table: "medical_visits",
                columns: new[] { "EmployeeId", "VisitDateTimeUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "medical_visits",
                schema: "health");
        }
    }
}
