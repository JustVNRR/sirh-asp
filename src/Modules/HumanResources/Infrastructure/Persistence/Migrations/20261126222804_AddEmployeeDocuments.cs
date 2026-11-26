using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sirh.Modules.HumanResources.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddEmployeeDocuments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "employee_documents",
                schema: "hr",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Category = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    DocumentType = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    FileKey = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    FileSizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    FileDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Commentary = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_employee_documents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_employee_documents_employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalSchema: "hr",
                        principalTable: "employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "resident_permit_details",
                schema: "hr",
                columns: table => new
                {
                    DocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PermitNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ExpiryDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ExpiryReminder30SentAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ExpiryReminder7SentAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_resident_permit_details", x => x.DocumentId);
                    table.ForeignKey(
                        name: "FK_resident_permit_details_employee_documents_DocumentId",
                        column: x => x.DocumentId,
                        principalSchema: "hr",
                        principalTable: "employee_documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "rqth_details",
                schema: "hr",
                columns: table => new
                {
                    DocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExpiryDate = table.Column<DateOnly>(type: "date", nullable: false),
                    WorkplaceAccommodation = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ExpiryReminder30SentAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ExpiryReminder7SentAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rqth_details", x => x.DocumentId);
                    table.ForeignKey(
                        name: "FK_rqth_details_employee_documents_DocumentId",
                        column: x => x.DocumentId,
                        principalSchema: "hr",
                        principalTable: "employee_documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_employee_documents_DocumentType",
                schema: "hr",
                table: "employee_documents",
                column: "DocumentType");

            migrationBuilder.CreateIndex(
                name: "IX_employee_documents_EmployeeId_Category",
                schema: "hr",
                table: "employee_documents",
                columns: new[] { "EmployeeId", "Category" });

            migrationBuilder.CreateIndex(
                name: "IX_resident_permit_details_ExpiryDate",
                schema: "hr",
                table: "resident_permit_details",
                column: "ExpiryDate");

            migrationBuilder.CreateIndex(
                name: "IX_rqth_details_ExpiryDate",
                schema: "hr",
                table: "rqth_details",
                column: "ExpiryDate");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "resident_permit_details",
                schema: "hr");

            migrationBuilder.DropTable(
                name: "rqth_details",
                schema: "hr");

            migrationBuilder.DropTable(
                name: "employee_documents",
                schema: "hr");
        }
    }
}
