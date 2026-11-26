using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sirh.Modules.HumanResources.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddEmergencyContactsAndPositions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "emergency_contacts",
                schema: "hr",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FullNameEncrypted = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    RelationshipEncrypted = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    MobilePhoneEncrypted = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    HomePhoneEncrypted = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    WorkPhoneEncrypted = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    PriorityOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_emergency_contacts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_emergency_contacts_employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalSchema: "hr",
                        principalTable: "employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "positions",
                schema: "hr",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Slug = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ManagerId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LocationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    JobTitle = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    StartAt = table.Column<DateOnly>(type: "date", nullable: false),
                    EndAt = table.Column<DateOnly>(type: "date", nullable: true),
                    ContractLabel = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    Coefficient = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Rate = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    Supervisor = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    TimeContractType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    TimeContractAmount = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    HierarchicalPosition = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    DepartmentsCsv = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_positions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_positions_employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalSchema: "hr",
                        principalTable: "employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_positions_employees_ManagerId",
                        column: x => x.ManagerId,
                        principalSchema: "hr",
                        principalTable: "employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_emergency_contacts_EmployeeId",
                schema: "hr",
                table: "emergency_contacts",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_positions_EmployeeId",
                schema: "hr",
                table: "positions",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_positions_LocationId",
                schema: "hr",
                table: "positions",
                column: "LocationId");

            migrationBuilder.CreateIndex(
                name: "IX_positions_ManagerId",
                schema: "hr",
                table: "positions",
                column: "ManagerId");

            migrationBuilder.CreateIndex(
                name: "IX_positions_Slug",
                schema: "hr",
                table: "positions",
                column: "Slug",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "emergency_contacts",
                schema: "hr");

            migrationBuilder.DropTable(
                name: "positions",
                schema: "hr");
        }
    }
}
