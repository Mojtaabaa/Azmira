using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KonkoorHelp.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPersonalImprovementAndCoach : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CoachThreads",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkspaceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CoachThreads", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ImprovementSnapshots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkspaceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    SubjectKey = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    PeriodKey = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    AbsoluteScore = table.Column<double>(type: "float", nullable: false),
                    ImprovementPercent = table.Column<double>(type: "float", nullable: false),
                    EvidenceEventCount = table.Column<int>(type: "int", nullable: false),
                    CapturedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImprovementSnapshots", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PersonalBaselines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkspaceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    SubjectKey = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    WindowStart = table.Column<DateTime>(type: "datetime2", nullable: false),
                    WindowEnd = table.Column<DateTime>(type: "datetime2", nullable: false),
                    BaselineScore = table.Column<double>(type: "float", nullable: false),
                    EvidenceEventCount = table.Column<int>(type: "int", nullable: false),
                    EvidenceSummaryJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EstablishedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RebaselinedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PersonalBaselines", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CoachMessages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ThreadId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Role = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Content = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Confidence = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    EvidenceRefsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ProviderCode = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CoachMessages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CoachMessages_CoachThreads_ThreadId",
                        column: x => x.ThreadId,
                        principalTable: "CoachThreads",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CoachMessages_ThreadId_CreatedAt",
                table: "CoachMessages",
                columns: new[] { "ThreadId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_CoachThreads_WorkspaceId_UserId_UpdatedAt",
                table: "CoachThreads",
                columns: new[] { "WorkspaceId", "UserId", "UpdatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ImprovementSnapshots_WorkspaceId_UserId_SubjectKey_PeriodKey",
                table: "ImprovementSnapshots",
                columns: new[] { "WorkspaceId", "UserId", "SubjectKey", "PeriodKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PersonalBaselines_WorkspaceId_UserId_SubjectKey",
                table: "PersonalBaselines",
                columns: new[] { "WorkspaceId", "UserId", "SubjectKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CoachMessages");

            migrationBuilder.DropTable(
                name: "ImprovementSnapshots");

            migrationBuilder.DropTable(
                name: "PersonalBaselines");

            migrationBuilder.DropTable(
                name: "CoachThreads");
        }
    }
}
