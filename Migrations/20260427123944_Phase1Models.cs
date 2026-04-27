using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace MindForge.Migrations
{
    /// <inheritdoc />
    public partial class Phase1Models : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CompletedAt",
                table: "Tests",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CoveragePercent",
                table: "Tests",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "IsSkipped",
                table: "Tests",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<float>(
                name: "Score",
                table: "Tests",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourcePhotoPath",
                table: "Tests",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TestSourceType",
                table: "Tests",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TimeTakenSeconds",
                table: "Tests",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Title",
                table: "Tests",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "UserId",
                table: "Tests",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AlterColumn<float>(
                name: "EaseFactor",
                table: "SpacedRepetitionItems",
                type: "REAL",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "TEXT");

            migrationBuilder.AddColumn<int>(
                name: "Interval",
                table: "SpacedRepetitionItems",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "KnowledgeNodeId",
                table: "SpacedRepetitionItems",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "LastReviewDate",
                table: "SpacedRepetitionItems",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UserId",
                table: "SpacedRepetitionItems",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "GoalDate",
                table: "LearningPlans",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "LearningPlans",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "Badges",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    IconKey = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Requirement = table.Column<string>(type: "TEXT", nullable: false),
                    XPReward = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Badges", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ChatMessages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    SubjectId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Role = table.Column<int>(type: "INTEGER", nullable: false),
                    Content = table.Column<string>(type: "TEXT", nullable: false),
                    ImagePath = table.Column<string>(type: "TEXT", nullable: true),
                    TokensUsed = table.Column<int>(type: "INTEGER", nullable: false),
                    Provider = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChatMessages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChatMessages_Subjects_SubjectId",
                        column: x => x.SubjectId,
                        principalTable: "Subjects",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ChatMessages_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "KnowledgeNodes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    SubjectId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Summary = table.Column<string>(type: "TEXT", nullable: false),
                    MaterialIds = table.Column<string>(type: "TEXT", nullable: false),
                    MasteryLevel = table.Column<float>(type: "REAL", nullable: false),
                    LastReviewed = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KnowledgeNodes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_KnowledgeNodes_Subjects_SubjectId",
                        column: x => x.SubjectId,
                        principalTable: "Subjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_KnowledgeNodes_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Materials",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    SubjectId = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    OriginalFileName = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    OriginalFormat = table.Column<int>(type: "INTEGER", nullable: false),
                    OriginalFilePath = table.Column<string>(type: "TEXT", nullable: false),
                    KiContent = table.Column<string>(type: "TEXT", nullable: false),
                    KiContentHash = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    ExtractedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    TokenCount = table.Column<int>(type: "INTEGER", nullable: false),
                    Tags = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Materials", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Materials_Subjects_SubjectId",
                        column: x => x.SubjectId,
                        principalTable: "Subjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Materials_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SystemSettings",
                columns: table => new
                {
                    Key = table.Column<string>(type: "TEXT", nullable: false),
                    Value = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SystemSettings", x => x.Key);
                });

            migrationBuilder.CreateTable(
                name: "TestQuestions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    TestId = table.Column<Guid>(type: "TEXT", nullable: false),
                    QuestionType = table.Column<int>(type: "INTEGER", nullable: false),
                    QuestionText = table.Column<string>(type: "TEXT", nullable: false),
                    Options = table.Column<string>(type: "TEXT", nullable: true),
                    CorrectAnswer = table.Column<string>(type: "TEXT", nullable: false),
                    UserAnswer = table.Column<string>(type: "TEXT", nullable: true),
                    IsCorrect = table.Column<bool>(type: "INTEGER", nullable: true),
                    Explanation = table.Column<string>(type: "TEXT", nullable: true),
                    OrderIndex = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TestQuestions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TestQuestions_Tests_TestId",
                        column: x => x.TestId,
                        principalTable: "Tests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "XPEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Amount = table.Column<int>(type: "INTEGER", nullable: false),
                    Source = table.Column<int>(type: "INTEGER", nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_XPEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_XPEvents_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserBadges",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    BadgeId = table.Column<Guid>(type: "TEXT", nullable: false),
                    UnlockedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserBadges", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserBadges_Badges_BadgeId",
                        column: x => x.BadgeId,
                        principalTable: "Badges",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserBadges_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FeynmanSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    TestId = table.Column<Guid>(type: "TEXT", nullable: false),
                    KnowledgeNodeId = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserExplanation = table.Column<string>(type: "TEXT", nullable: false),
                    AiAssessment = table.Column<string>(type: "TEXT", nullable: false),
                    GapsIdentified = table.Column<string>(type: "TEXT", nullable: false),
                    MasteryScore = table.Column<float>(type: "REAL", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FeynmanSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FeynmanSessions_KnowledgeNodes_KnowledgeNodeId",
                        column: x => x.KnowledgeNodeId,
                        principalTable: "KnowledgeNodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FeynmanSessions_Tests_TestId",
                        column: x => x.TestId,
                        principalTable: "Tests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "KnowledgeEdges",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    FromNodeId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ToNodeId = table.Column<Guid>(type: "TEXT", nullable: false),
                    RelationType = table.Column<int>(type: "INTEGER", nullable: false),
                    Strength = table.Column<float>(type: "REAL", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KnowledgeEdges", x => x.Id);
                    table.ForeignKey(
                        name: "FK_KnowledgeEdges_KnowledgeNodes_FromNodeId",
                        column: x => x.FromNodeId,
                        principalTable: "KnowledgeNodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_KnowledgeEdges_KnowledgeNodes_ToNodeId",
                        column: x => x.ToNodeId,
                        principalTable: "KnowledgeNodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LearningTasks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    PlanId = table.Column<Guid>(type: "TEXT", nullable: false),
                    KnowledgeNodeId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Title = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: false),
                    ScheduledDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    TaskType = table.Column<int>(type: "INTEGER", nullable: false),
                    DurationMinutes = table.Column<int>(type: "INTEGER", nullable: false),
                    Priority = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LearningTasks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LearningTasks_KnowledgeNodes_KnowledgeNodeId",
                        column: x => x.KnowledgeNodeId,
                        principalTable: "KnowledgeNodes",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_LearningTasks_LearningPlans_PlanId",
                        column: x => x.PlanId,
                        principalTable: "LearningPlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "Badges",
                columns: new[] { "Id", "Description", "IconKey", "Name", "Requirement", "XPReward" },
                values: new object[,]
                {
                    { new Guid("b0000001-0000-0000-0000-000000000001"), "Erstes Material hochgeladen", "upload", "Erster Upload", "{\"type\":\"materials_uploaded\",\"count\":1}", 50 },
                    { new Guid("b0000001-0000-0000-0000-000000000002"), "10 Materialien hochgeladen", "books", "Wissensdurst", "{\"type\":\"materials_uploaded\",\"count\":10}", 200 },
                    { new Guid("b0000001-0000-0000-0000-000000000003"), "Ersten Test abgeschlossen", "test", "Testpilot", "{\"type\":\"tests_completed\",\"count\":1}", 50 },
                    { new Guid("b0000001-0000-0000-0000-000000000004"), "100% in einem Test", "star", "Perfektionist", "{\"type\":\"perfect_score\",\"count\":1}", 200 },
                    { new Guid("b0000001-0000-0000-0000-000000000005"), "5 Feynman-Sessions bestanden", "brain", "Feynman-Meister", "{\"type\":\"feynman_passed\",\"count\":5}", 300 },
                    { new Guid("b0000001-0000-0000-0000-000000000006"), "7 Tage in Folge gelernt", "fire", "Wochenstreak", "{\"type\":\"streak\",\"count\":7}", 100 },
                    { new Guid("b0000001-0000-0000-0000-000000000007"), "30 Tage in Folge gelernt", "fire2", "Monatsstreak", "{\"type\":\"streak\",\"count\":30}", 500 },
                    { new Guid("b0000001-0000-0000-0000-000000000008"), "50 Chat-Nachrichten gesendet", "chat", "KI-Flüsterer", "{\"type\":\"chat_messages\",\"count\":50}", 150 },
                    { new Guid("b0000001-0000-0000-0000-000000000009"), "Ersten Lernplan erstellt", "calendar", "Planer", "{\"type\":\"plans_created\",\"count\":1}", 50 },
                    { new Guid("b0000001-0000-0000-0000-000000000010"), "Level 10 erreicht", "trophy", "Level 10", "{\"type\":\"level\",\"count\":10}", 1000 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Tests_UserId",
                table: "Tests",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_SpacedRepetitionItems_KnowledgeNodeId",
                table: "SpacedRepetitionItems",
                column: "KnowledgeNodeId");

            migrationBuilder.CreateIndex(
                name: "IX_ChatMessages_SubjectId",
                table: "ChatMessages",
                column: "SubjectId");

            migrationBuilder.CreateIndex(
                name: "IX_ChatMessages_UserId",
                table: "ChatMessages",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_FeynmanSessions_KnowledgeNodeId",
                table: "FeynmanSessions",
                column: "KnowledgeNodeId");

            migrationBuilder.CreateIndex(
                name: "IX_FeynmanSessions_TestId",
                table: "FeynmanSessions",
                column: "TestId");

            migrationBuilder.CreateIndex(
                name: "IX_KnowledgeEdges_FromNodeId",
                table: "KnowledgeEdges",
                column: "FromNodeId");

            migrationBuilder.CreateIndex(
                name: "IX_KnowledgeEdges_ToNodeId",
                table: "KnowledgeEdges",
                column: "ToNodeId");

            migrationBuilder.CreateIndex(
                name: "IX_KnowledgeNodes_SubjectId",
                table: "KnowledgeNodes",
                column: "SubjectId");

            migrationBuilder.CreateIndex(
                name: "IX_KnowledgeNodes_UserId",
                table: "KnowledgeNodes",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_LearningTasks_KnowledgeNodeId",
                table: "LearningTasks",
                column: "KnowledgeNodeId");

            migrationBuilder.CreateIndex(
                name: "IX_LearningTasks_PlanId",
                table: "LearningTasks",
                column: "PlanId");

            migrationBuilder.CreateIndex(
                name: "IX_Materials_SubjectId",
                table: "Materials",
                column: "SubjectId");

            migrationBuilder.CreateIndex(
                name: "IX_Materials_UserId",
                table: "Materials",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_TestQuestions_TestId",
                table: "TestQuestions",
                column: "TestId");

            migrationBuilder.CreateIndex(
                name: "IX_UserBadges_BadgeId",
                table: "UserBadges",
                column: "BadgeId");

            migrationBuilder.CreateIndex(
                name: "IX_UserBadges_UserId",
                table: "UserBadges",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_XPEvents_UserId",
                table: "XPEvents",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_SpacedRepetitionItems_KnowledgeNodes_KnowledgeNodeId",
                table: "SpacedRepetitionItems",
                column: "KnowledgeNodeId",
                principalTable: "KnowledgeNodes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Tests_Users_UserId",
                table: "Tests",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SpacedRepetitionItems_KnowledgeNodes_KnowledgeNodeId",
                table: "SpacedRepetitionItems");

            migrationBuilder.DropForeignKey(
                name: "FK_Tests_Users_UserId",
                table: "Tests");

            migrationBuilder.DropTable(
                name: "ChatMessages");

            migrationBuilder.DropTable(
                name: "FeynmanSessions");

            migrationBuilder.DropTable(
                name: "KnowledgeEdges");

            migrationBuilder.DropTable(
                name: "LearningTasks");

            migrationBuilder.DropTable(
                name: "Materials");

            migrationBuilder.DropTable(
                name: "SystemSettings");

            migrationBuilder.DropTable(
                name: "TestQuestions");

            migrationBuilder.DropTable(
                name: "UserBadges");

            migrationBuilder.DropTable(
                name: "XPEvents");

            migrationBuilder.DropTable(
                name: "KnowledgeNodes");

            migrationBuilder.DropTable(
                name: "Badges");

            migrationBuilder.DropIndex(
                name: "IX_Tests_UserId",
                table: "Tests");

            migrationBuilder.DropIndex(
                name: "IX_SpacedRepetitionItems_KnowledgeNodeId",
                table: "SpacedRepetitionItems");

            migrationBuilder.DropColumn(
                name: "CompletedAt",
                table: "Tests");

            migrationBuilder.DropColumn(
                name: "CoveragePercent",
                table: "Tests");

            migrationBuilder.DropColumn(
                name: "IsSkipped",
                table: "Tests");

            migrationBuilder.DropColumn(
                name: "Score",
                table: "Tests");

            migrationBuilder.DropColumn(
                name: "SourcePhotoPath",
                table: "Tests");

            migrationBuilder.DropColumn(
                name: "TestSourceType",
                table: "Tests");

            migrationBuilder.DropColumn(
                name: "TimeTakenSeconds",
                table: "Tests");

            migrationBuilder.DropColumn(
                name: "Title",
                table: "Tests");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "Tests");

            migrationBuilder.DropColumn(
                name: "Interval",
                table: "SpacedRepetitionItems");

            migrationBuilder.DropColumn(
                name: "KnowledgeNodeId",
                table: "SpacedRepetitionItems");

            migrationBuilder.DropColumn(
                name: "LastReviewDate",
                table: "SpacedRepetitionItems");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "SpacedRepetitionItems");

            migrationBuilder.DropColumn(
                name: "GoalDate",
                table: "LearningPlans");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "LearningPlans");

            migrationBuilder.AlterColumn<decimal>(
                name: "EaseFactor",
                table: "SpacedRepetitionItems",
                type: "TEXT",
                nullable: false,
                oldClrType: typeof(float),
                oldType: "REAL");
        }
    }
}
