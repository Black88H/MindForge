using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace MindForge.Migrations
{
    /// <inheritdoc />
    public partial class AddV1Entities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Challenges",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    Icon = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Type = table.Column<int>(type: "INTEGER", nullable: false),
                    XpReward = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ExpiresDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    RequiredProgress = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Challenges", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LearningMethods",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    Icon = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Type = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LearningMethods", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LearningPlans",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    UserId = table.Column<string>(type: "TEXT", nullable: false),
                    SubjectId = table.Column<Guid>(type: "TEXT", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    PlannedDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    DifficultyLevel = table.Column<int>(type: "INTEGER", nullable: false),
                    DaysAvailable = table.Column<int>(type: "INTEGER", nullable: false),
                    MinutesPerDay = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LearningPlans", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LearningPlans_Subjects_SubjectId",
                        column: x => x.SubjectId,
                        principalTable: "Subjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "MaterialLibrary",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    SubjectId = table.Column<Guid>(type: "TEXT", nullable: true),
                    FileName = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    FileType = table.Column<int>(type: "INTEGER", nullable: false),
                    ContentOrUrl = table.Column<string>(type: "TEXT", maxLength: 5000, nullable: false),
                    UploadedDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    SizeBytes = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MaterialLibrary", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MaterialLibrary_Subjects_SubjectId",
                        column: x => x.SubjectId,
                        principalTable: "Subjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Notifications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Message = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    Type = table.Column<int>(type: "INTEGER", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Read = table.Column<bool>(type: "INTEGER", nullable: false),
                    XpAmount = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notifications", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OCRDocuments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    UploadedFilePath = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    ExtractedText = table.Column<string>(type: "TEXT", nullable: false),
                    Confidence = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    ProcessedDate = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OCRDocuments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SpacedRepetitionItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserProgressId = table.Column<Guid>(type: "TEXT", nullable: false),
                    NextReviewDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    IntervalDays = table.Column<int>(type: "INTEGER", nullable: false),
                    EaseFactor = table.Column<decimal>(type: "TEXT", nullable: false),
                    Repetitions = table.Column<int>(type: "INTEGER", nullable: false),
                    LastQuality = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SpacedRepetitionItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SpacedRepetitionItems_UserProgress_UserProgressId",
                        column: x => x.UserProgressId,
                        principalTable: "UserProgress",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserLearningProfiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    PreferredMethods = table.Column<string>(type: "TEXT", nullable: true),
                    LearningStyle = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    ShortExplanations = table.Column<bool>(type: "INTEGER", nullable: false),
                    NeedsExamples = table.Column<bool>(type: "INTEGER", nullable: false),
                    NeedsExercises = table.Column<bool>(type: "INTEGER", nullable: false),
                    NeedsFormulas = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedDate = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserLearningProfiles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserTestHistory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    TestId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Score = table.Column<double>(type: "REAL", nullable: false),
                    TotalQuestions = table.Column<int>(type: "INTEGER", nullable: false),
                    CorrectAnswers = table.Column<int>(type: "INTEGER", nullable: false),
                    TimeTaken = table.Column<TimeSpan>(type: "TEXT", nullable: false),
                    Attempts = table.Column<int>(type: "INTEGER", nullable: false),
                    LastAttempt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    WrongAnswers = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserTestHistory", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserTestHistory_Tests_TestId",
                        column: x => x.TestId,
                        principalTable: "Tests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserChallenges",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    ChallengeId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Progress = table.Column<int>(type: "INTEGER", nullable: false),
                    Completed = table.Column<bool>(type: "INTEGER", nullable: false),
                    CompletedDate = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserChallenges", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserChallenges_Challenges_ChallengeId",
                        column: x => x.ChallengeId,
                        principalTable: "Challenges",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LearningPlanMethods",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    LearningPlanId = table.Column<Guid>(type: "TEXT", nullable: false),
                    LearningMethodId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Order = table.Column<int>(type: "INTEGER", nullable: false),
                    DailyMinutes = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LearningPlanMethods", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LearningPlanMethods_LearningMethods_LearningMethodId",
                        column: x => x.LearningMethodId,
                        principalTable: "LearningMethods",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LearningPlanMethods_LearningPlans_LearningPlanId",
                        column: x => x.LearningPlanId,
                        principalTable: "LearningPlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "Achievements",
                columns: new[] { "Id", "Description", "Icon", "IsUnlocked", "Name", "Rarity", "TriggerKey", "TriggerValue", "UnlockedAt", "XpReward" },
                values: new object[,]
                {
                    { new Guid("00000000-0000-0000-0000-000000000011"), "Ersten Lernplan erstellt", "📅", false, "Planer", 0, "plans_created", 1, null, 50 },
                    { new Guid("00000000-0000-0000-0000-000000000012"), "5 Dokumente gescannt", "🔍", false, "OCR-Meister", 1, "ocr_scans", 5, null, 150 },
                    { new Guid("00000000-0000-0000-0000-000000000013"), "Erste Challenge abgeschlossen", "🏆", false, "Challenger", 0, "challenges_completed", 1, null, 75 },
                    { new Guid("00000000-0000-0000-0000-000000000014"), "10 Lernpläne erstellt", "🤖", false, "Lernmaschine", 2, "plans_created", 10, null, 400 }
                });

            migrationBuilder.InsertData(
                table: "LearningMethods",
                columns: new[] { "Id", "Description", "Icon", "Name", "Type" },
                values: new object[,]
                {
                    { new Guid("10000000-0000-0000-0000-000000000001"), "Aktives Erinnern ohne Hinweise", "🧠", "Active Recall", 0 },
                    { new Guid("10000000-0000-0000-0000-000000000002"), "25 Min lernen, 5 Min Pause", "🍅", "Pomodoro", 1 },
                    { new Guid("10000000-0000-0000-0000-000000000003"), "SM-2 Algorithmus für optimale Wiederholung", "🔄", "Spaced Repetition", 2 },
                    { new Guid("10000000-0000-0000-0000-000000000004"), "Verschiedene Themen abwechseln", "🔀", "Interleaving", 3 },
                    { new Guid("10000000-0000-0000-0000-000000000005"), "Prüfungssimulation und Tests", "📝", "Practice Test", 4 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_LearningPlanMethods_LearningMethodId",
                table: "LearningPlanMethods",
                column: "LearningMethodId");

            migrationBuilder.CreateIndex(
                name: "IX_LearningPlanMethods_LearningPlanId",
                table: "LearningPlanMethods",
                column: "LearningPlanId");

            migrationBuilder.CreateIndex(
                name: "IX_LearningPlans_SubjectId",
                table: "LearningPlans",
                column: "SubjectId");

            migrationBuilder.CreateIndex(
                name: "IX_LearningPlans_UserId",
                table: "LearningPlans",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_MaterialLibrary_SubjectId",
                table: "MaterialLibrary",
                column: "SubjectId");

            migrationBuilder.CreateIndex(
                name: "IX_MaterialLibrary_UserId",
                table: "MaterialLibrary",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_UserId_Read",
                table: "Notifications",
                columns: new[] { "UserId", "Read" });

            migrationBuilder.CreateIndex(
                name: "IX_SpacedRepetitionItems_NextReviewDate",
                table: "SpacedRepetitionItems",
                column: "NextReviewDate");

            migrationBuilder.CreateIndex(
                name: "IX_SpacedRepetitionItems_UserProgressId",
                table: "SpacedRepetitionItems",
                column: "UserProgressId");

            migrationBuilder.CreateIndex(
                name: "IX_UserChallenges_ChallengeId",
                table: "UserChallenges",
                column: "ChallengeId");

            migrationBuilder.CreateIndex(
                name: "IX_UserChallenges_UserId",
                table: "UserChallenges",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserTestHistory_TestId",
                table: "UserTestHistory",
                column: "TestId");

            migrationBuilder.CreateIndex(
                name: "IX_UserTestHistory_UserId",
                table: "UserTestHistory",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LearningPlanMethods");

            migrationBuilder.DropTable(
                name: "MaterialLibrary");

            migrationBuilder.DropTable(
                name: "Notifications");

            migrationBuilder.DropTable(
                name: "OCRDocuments");

            migrationBuilder.DropTable(
                name: "SpacedRepetitionItems");

            migrationBuilder.DropTable(
                name: "UserChallenges");

            migrationBuilder.DropTable(
                name: "UserLearningProfiles");

            migrationBuilder.DropTable(
                name: "UserTestHistory");

            migrationBuilder.DropTable(
                name: "LearningMethods");

            migrationBuilder.DropTable(
                name: "LearningPlans");

            migrationBuilder.DropTable(
                name: "Challenges");

            migrationBuilder.DeleteData(
                table: "Achievements",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000011"));

            migrationBuilder.DeleteData(
                table: "Achievements",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000012"));

            migrationBuilder.DeleteData(
                table: "Achievements",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000013"));

            migrationBuilder.DeleteData(
                table: "Achievements",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000014"));
        }
    }
}
