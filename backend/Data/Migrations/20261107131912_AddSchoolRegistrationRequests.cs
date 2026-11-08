using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KonkoorHelp.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSchoolRegistrationRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SchoolRegistrationRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequesterUserId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    City = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(800)", maxLength: 800, nullable: false),
                    Status = table.Column<byte>(type: "tinyint", nullable: false),
                    ReviewerUserId = table.Column<int>(type: "int", nullable: true),
                    ReviewedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    CreatedSchoolId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedWorkspaceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SchoolRegistrationRequests", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SchoolRegistrationRequests_RequesterUserId_Status",
                table: "SchoolRegistrationRequests",
                columns: new[] { "RequesterUserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_SchoolRegistrationRequests_Status_CreatedAt",
                table: "SchoolRegistrationRequests",
                columns: new[] { "Status", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SchoolRegistrationRequests");
        }
    }
}
