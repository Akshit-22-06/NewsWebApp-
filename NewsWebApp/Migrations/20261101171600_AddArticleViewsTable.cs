using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NewsWebApp.Migrations
{
    /// <inheritdoc />
    public partial class AddArticleViewsTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ArticleViews",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    NewsArticleId = table.Column<int>(type: "INTEGER", nullable: false),
                    ViewedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ViewerHash = table.Column<string>(type: "TEXT", maxLength: 32, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArticleViews", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ArticleViews_NewsArticles_NewsArticleId",
                        column: x => x.NewsArticleId,
                        principalTable: "NewsArticles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ArticleViews_NewsArticleId_ViewedAt",
                table: "ArticleViews",
                columns: new[] { "NewsArticleId", "ViewedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ArticleViews_NewsArticleId_ViewerHash_ViewedAt",
                table: "ArticleViews",
                columns: new[] { "NewsArticleId", "ViewerHash", "ViewedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ArticleViews_ViewedAt",
                table: "ArticleViews",
                column: "ViewedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ArticleViews");
        }
    }
}
