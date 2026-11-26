using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sirh.Modules.InternalLife.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddStaffRepresentativesAndRseIdeas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "rse_ideas",
                schema: "internal_life",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AuthorEmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LocationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    IdeaContent = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    AttachmentFileKey = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    AttachmentFileName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    AttachmentContentType = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    AttachmentFileSizeBytes = table.Column<long>(type: "bigint", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    RhComment = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    TreatedByEmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rse_ideas", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "staff_representatives",
                schema: "internal_life",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    FirstName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    LastName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    RepresentativeRole = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    College = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    IsHarassmentAdvisor = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_staff_representatives", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_rse_ideas_AuthorEmployeeId_Status",
                schema: "internal_life",
                table: "rse_ideas",
                columns: new[] { "AuthorEmployeeId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_rse_ideas_LocationId_Status",
                schema: "internal_life",
                table: "rse_ideas",
                columns: new[] { "LocationId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_staff_representatives_CompanyId_Email",
                schema: "internal_life",
                table: "staff_representatives",
                columns: new[] { "CompanyId", "Email" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_staff_representatives_CompanyId_IsHarassmentAdvisor",
                schema: "internal_life",
                table: "staff_representatives",
                columns: new[] { "CompanyId", "IsHarassmentAdvisor" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "rse_ideas",
                schema: "internal_life");

            migrationBuilder.DropTable(
                name: "staff_representatives",
                schema: "internal_life");
        }
    }
}
