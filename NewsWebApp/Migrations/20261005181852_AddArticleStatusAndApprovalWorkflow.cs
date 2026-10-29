using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NewsWebApp.Migrations
{
    /// <inheritdoc />
    public partial class AddArticleStatusAndApprovalWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RejectionReason",
                table: "NewsArticles",
                type: "TEXT",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "NewsArticles",
                type: "TEXT",
                maxLength: 20,
                nullable: false,
                defaultValue: "Pending");

            migrationBuilder.CreateIndex(
                name: "IX_NewsArticles_IsPublished_Status_PublishedDate",
                table: "NewsArticles",
                columns: new[] { "IsPublished", "Status", "PublishedDate" });

            migrationBuilder.CreateIndex(
                name: "IX_NewsArticles_Status",
                table: "NewsArticles",
                column: "Status");

            // Ensure all previously published starter/system articles are marked as Approved
            migrationBuilder.Sql("UPDATE NewsArticles SET Status = 'Approved' WHERE IsPublished = 1;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_NewsArticles_IsPublished_Status_PublishedDate",
                table: "NewsArticles");

            migrationBuilder.DropIndex(
                name: "IX_NewsArticles_Status",
                table: "NewsArticles");

            migrationBuilder.DropColumn(
                name: "RejectionReason",
                table: "NewsArticles");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "NewsArticles");
        }
    }
}
