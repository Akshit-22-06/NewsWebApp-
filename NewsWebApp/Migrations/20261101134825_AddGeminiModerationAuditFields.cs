using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NewsWebApp.Migrations
{
    /// <inheritdoc />
    public partial class AddGeminiModerationAuditFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ModeratedAt",
                table: "NewsArticles",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModerationCategories",
                table: "NewsArticles",
                type: "TEXT",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModerationModel",
                table: "NewsArticles",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ModerationQualityScore",
                table: "NewsArticles",
                type: "INTEGER",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ModeratedAt",
                table: "NewsArticles");

            migrationBuilder.DropColumn(
                name: "ModerationCategories",
                table: "NewsArticles");

            migrationBuilder.DropColumn(
                name: "ModerationModel",
                table: "NewsArticles");

            migrationBuilder.DropColumn(
                name: "ModerationQualityScore",
                table: "NewsArticles");
        }
    }
}
