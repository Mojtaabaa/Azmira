using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KonkoorHelp.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddLearningIntelligence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AiPlanSuggestions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkspaceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<byte>(type: "tinyint", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ScheduleJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FactsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ProviderCode = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ResolvedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiPlanSuggestions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AiRecommendations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkspaceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    Kind = table.Column<byte>(type: "tinyint", nullable: false),
                    Status = table.Column<byte>(type: "tinyint", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Body = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PayloadJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FactsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ProviderCode = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ResolvedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ResolvedByUserId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiRecommendations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AiWeeklyReports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkspaceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    Kind = table.Column<byte>(type: "tinyint", nullable: false),
                    PeriodKey = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    FactsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Narrative = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RecommendationsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ProviderCode = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    GeneratedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiWeeklyReports", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AiWorkspaceSettings",
                columns: table => new
                {
                    WorkspaceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Enabled = table.Column<bool>(type: "bit", nullable: false),
                    ProviderCode = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Model = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    AllowUnderAge = table.Column<bool>(type: "bit", nullable: false),
                    MinAgeYears = table.Column<int>(type: "int", nullable: false),
                    SystemPromptExtra = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiWorkspaceSettings", x => x.WorkspaceId);
                });

            migrationBuilder.CreateTable(
                name: "Assessments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkspaceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(800)", maxLength: 800, nullable: false),
                    Kind = table.Column<byte>(type: "tinyint", nullable: false),
                    Status = table.Column<byte>(type: "tinyint", nullable: false),
                    Subject = table.Column<string>(type: "nvarchar(90)", maxLength: 90, nullable: false),
                    Chapter = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    Topic = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    ClassId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    TimeLimitMinutes = table.Column<int>(type: "int", nullable: true),
                    PassScore = table.Column<double>(type: "float", nullable: false),
                    AvailableFrom = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AvailableTo = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Assessments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MasteryNodes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkspaceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    Subject = table.Column<string>(type: "nvarchar(90)", maxLength: 90, nullable: false),
                    Chapter = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    Topic = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    Level = table.Column<byte>(type: "tinyint", nullable: false),
                    ScoreEma = table.Column<double>(type: "float", nullable: false),
                    EvidenceCount = table.Column<int>(type: "int", nullable: false),
                    CorrectCount = table.Column<int>(type: "int", nullable: false),
                    ReviewSuccessCount = table.Column<int>(type: "int", nullable: false),
                    ReviewFailCount = table.Column<int>(type: "int", nullable: false),
                    LastEvidenceAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MasteryNodes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SpacedRepetitionCards",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkspaceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    Subject = table.Column<string>(type: "nvarchar(90)", maxLength: 90, nullable: false),
                    Chapter = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    Topic = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    AlgorithmCode = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    AlgorithmStateJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Difficulty = table.Column<byte>(type: "tinyint", nullable: false),
                    Confidence = table.Column<byte>(type: "tinyint", nullable: false),
                    IntervalDays = table.Column<int>(type: "int", nullable: false),
                    EaseFactor = table.Column<double>(type: "float", nullable: false),
                    Repetition = table.Column<int>(type: "int", nullable: false),
                    NextReviewDate = table.Column<DateOnly>(type: "date", nullable: false),
                    LastReviewedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SpacedRepetitionCards", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SpacedRepetitionProfiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkspaceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Code = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    AlgorithmCode = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    ConfigJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsSystem = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SpacedRepetitionProfiles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AssessmentAttempts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkspaceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssessmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<byte>(type: "tinyint", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SubmittedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DurationSeconds = table.Column<int>(type: "int", nullable: false),
                    Score = table.Column<double>(type: "float", nullable: false),
                    MaxScore = table.Column<double>(type: "float", nullable: false),
                    Percent = table.Column<double>(type: "float", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssessmentAttempts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AssessmentAttempts_Assessments_AssessmentId",
                        column: x => x.AssessmentId,
                        principalTable: "Assessments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AssessmentQuestions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssessmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Ordinal = table.Column<int>(type: "int", nullable: false),
                    Type = table.Column<byte>(type: "tinyint", nullable: false),
                    Prompt = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OptionsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CorrectAnswerJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Points = table.Column<double>(type: "float", nullable: false),
                    Subject = table.Column<string>(type: "nvarchar(90)", maxLength: 90, nullable: false),
                    Chapter = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    Topic = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssessmentQuestions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AssessmentQuestions_Assessments_AssessmentId",
                        column: x => x.AssessmentId,
                        principalTable: "Assessments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SpacedRepetitionReviews",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CardId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkspaceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    Outcome = table.Column<byte>(type: "tinyint", nullable: false),
                    DifficultyRating = table.Column<byte>(type: "tinyint", nullable: false),
                    ConfidenceRating = table.Column<byte>(type: "tinyint", nullable: false),
                    Successful = table.Column<bool>(type: "bit", nullable: false),
                    IntervalBeforeDays = table.Column<int>(type: "int", nullable: false),
                    IntervalAfterDays = table.Column<int>(type: "int", nullable: false),
                    AlgorithmCode = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    StateBeforeJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    StateAfterJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ReviewedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SpacedRepetitionReviews", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SpacedRepetitionReviews_SpacedRepetitionCards_CardId",
                        column: x => x.CardId,
                        principalTable: "SpacedRepetitionCards",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AssessmentAnswers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AttemptId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    QuestionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ResponseJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsCorrect = table.Column<bool>(type: "bit", nullable: true),
                    PointsAwarded = table.Column<double>(type: "float", nullable: false),
                    DurationSeconds = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssessmentAnswers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AssessmentAnswers_AssessmentAttempts_AttemptId",
                        column: x => x.AttemptId,
                        principalTable: "AssessmentAttempts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AssessmentAnswers_AssessmentQuestions_QuestionId",
                        column: x => x.QuestionId,
                        principalTable: "AssessmentQuestions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AiPlanSuggestions_WorkspaceId_UserId_Status_CreatedAt",
                table: "AiPlanSuggestions",
                columns: new[] { "WorkspaceId", "UserId", "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AiRecommendations_WorkspaceId_UserId_Status_CreatedAt",
                table: "AiRecommendations",
                columns: new[] { "WorkspaceId", "UserId", "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AiWeeklyReports_WorkspaceId_UserId_PeriodKey",
                table: "AiWeeklyReports",
                columns: new[] { "WorkspaceId", "UserId", "PeriodKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AssessmentAnswers_AttemptId_QuestionId",
                table: "AssessmentAnswers",
                columns: new[] { "AttemptId", "QuestionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AssessmentAnswers_QuestionId",
                table: "AssessmentAnswers",
                column: "QuestionId");

            migrationBuilder.CreateIndex(
                name: "IX_AssessmentAttempts_AssessmentId",
                table: "AssessmentAttempts",
                column: "AssessmentId");

            migrationBuilder.CreateIndex(
                name: "IX_AssessmentAttempts_WorkspaceId_UserId_StartedAt",
                table: "AssessmentAttempts",
                columns: new[] { "WorkspaceId", "UserId", "StartedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AssessmentQuestions_AssessmentId_Ordinal",
                table: "AssessmentQuestions",
                columns: new[] { "AssessmentId", "Ordinal" });

            migrationBuilder.CreateIndex(
                name: "IX_Assessments_WorkspaceId_Status_Kind",
                table: "Assessments",
                columns: new[] { "WorkspaceId", "Status", "Kind" });

            migrationBuilder.CreateIndex(
                name: "IX_MasteryNodes_WorkspaceId_UserId_Subject_Chapter_Topic",
                table: "MasteryNodes",
                columns: new[] { "WorkspaceId", "UserId", "Subject", "Chapter", "Topic" },
                unique: true,
                filter: "[Chapter] IS NOT NULL AND [Topic] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_SpacedRepetitionCards_WorkspaceId_UserId_NextReviewDate",
                table: "SpacedRepetitionCards",
                columns: new[] { "WorkspaceId", "UserId", "NextReviewDate" });

            migrationBuilder.CreateIndex(
                name: "IX_SpacedRepetitionCards_WorkspaceId_UserId_Subject_Topic",
                table: "SpacedRepetitionCards",
                columns: new[] { "WorkspaceId", "UserId", "Subject", "Topic" });

            migrationBuilder.CreateIndex(
                name: "IX_SpacedRepetitionProfiles_WorkspaceId_Code",
                table: "SpacedRepetitionProfiles",
                columns: new[] { "WorkspaceId", "Code" });

            migrationBuilder.CreateIndex(
                name: "IX_SpacedRepetitionReviews_CardId_ReviewedAt",
                table: "SpacedRepetitionReviews",
                columns: new[] { "CardId", "ReviewedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AiPlanSuggestions");

            migrationBuilder.DropTable(
                name: "AiRecommendations");

            migrationBuilder.DropTable(
                name: "AiWeeklyReports");

            migrationBuilder.DropTable(
                name: "AiWorkspaceSettings");

            migrationBuilder.DropTable(
                name: "AssessmentAnswers");

            migrationBuilder.DropTable(
                name: "MasteryNodes");

            migrationBuilder.DropTable(
                name: "SpacedRepetitionProfiles");

            migrationBuilder.DropTable(
                name: "SpacedRepetitionReviews");

            migrationBuilder.DropTable(
                name: "AssessmentAttempts");

            migrationBuilder.DropTable(
                name: "AssessmentQuestions");

            migrationBuilder.DropTable(
                name: "SpacedRepetitionCards");

            migrationBuilder.DropTable(
                name: "Assessments");
        }
    }
}
