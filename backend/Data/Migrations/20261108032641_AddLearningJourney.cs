using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KonkoorHelp.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddLearningJourney : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "JourneyDefinitions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkspaceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Code = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Locale = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    IsSystem = table.Column<bool>(type: "bit", nullable: false),
                    IsBootstrap = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JourneyDefinitions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "JourneyRegions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    JourneyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    VisualThemeKey = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    SubjectKey = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JourneyRegions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_JourneyRegions_JourneyDefinitions_JourneyId",
                        column: x => x.JourneyId,
                        principalTable: "JourneyDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LearnerJourneyProgress",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkspaceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    JourneyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CurrentStageId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UnlockedStageIdsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CompletedStageIdsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EvidenceJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LearnerJourneyProgress", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LearnerJourneyProgress_JourneyDefinitions_JourneyId",
                        column: x => x.JourneyId,
                        principalTable: "JourneyDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "JourneyStages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RegionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    NarrativeBlurb = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    RequirementsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UnlockPriorStageId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RewardsJson = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JourneyStages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_JourneyStages_JourneyRegions_RegionId",
                        column: x => x.RegionId,
                        principalTable: "JourneyRegions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_JourneyDefinitions_WorkspaceId_Code",
                table: "JourneyDefinitions",
                columns: new[] { "WorkspaceId", "Code" },
                unique: true,
                filter: "[WorkspaceId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_JourneyRegions_JourneyId_SortOrder",
                table: "JourneyRegions",
                columns: new[] { "JourneyId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_JourneyStages_RegionId_SortOrder",
                table: "JourneyStages",
                columns: new[] { "RegionId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_LearnerJourneyProgress_JourneyId",
                table: "LearnerJourneyProgress",
                column: "JourneyId");

            migrationBuilder.CreateIndex(
                name: "IX_LearnerJourneyProgress_WorkspaceId_UserId_JourneyId",
                table: "LearnerJourneyProgress",
                columns: new[] { "WorkspaceId", "UserId", "JourneyId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "JourneyStages");

            migrationBuilder.DropTable(
                name: "LearnerJourneyProgress");

            migrationBuilder.DropTable(
                name: "JourneyRegions");

            migrationBuilder.DropTable(
                name: "JourneyDefinitions");
        }
    }
}
