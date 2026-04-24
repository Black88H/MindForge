using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MindForge.Migrations
{
    /// <inheritdoc />
    public partial class AddTokenUsage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TokenUsage",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Provider = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    TaskType = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    TokensUsed = table.Column<int>(type: "INTEGER", nullable: false),
                    CostUSD = table.Column<double>(type: "REAL", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TokenUsage", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TokenUsage");
        }
    }
}
