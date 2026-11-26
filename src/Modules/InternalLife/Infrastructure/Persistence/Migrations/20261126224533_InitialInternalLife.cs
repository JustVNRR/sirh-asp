using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sirh.Modules.InternalLife.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialInternalLife : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "internal_life");

            migrationBuilder.CreateTable(
                name: "common_documents",
                schema: "internal_life",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ContractTypeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Category = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    IsWelcomeKit = table.Column<bool>(type: "bit", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    FileKey = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    FileSizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    FileDate = table.Column<DateOnly>(type: "date", nullable: false),
                    IsArchived = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_common_documents", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_common_documents_Category_IsArchived",
                schema: "internal_life",
                table: "common_documents",
                columns: new[] { "Category", "IsArchived" });

            migrationBuilder.CreateIndex(
                name: "IX_common_documents_CompanyId_IsWelcomeKit_IsArchived",
                schema: "internal_life",
                table: "common_documents",
                columns: new[] { "CompanyId", "IsWelcomeKit", "IsArchived" });

            migrationBuilder.CreateIndex(
                name: "IX_common_documents_ContractTypeId",
                schema: "internal_life",
                table: "common_documents",
                column: "ContractTypeId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "common_documents",
                schema: "internal_life");
        }
    }
}
