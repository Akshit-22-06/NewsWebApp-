using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NewsWebApp.Migrations
{
    /// <inheritdoc />
    public partial class AddNewsletterAutomationTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "UnsubscribeToken",
                table: "NewsletterSubscribers",
                type: "TEXT",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "UnsubscribedAt",
                table: "NewsletterSubscribers",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "NewsletterEditions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    EditionDate = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Subject = table.Column<string>(type: "TEXT", maxLength: 250, nullable: false),
                    HtmlContent = table.Column<string>(type: "TEXT", nullable: false),
                    PlainTextContent = table.Column<string>(type: "TEXT", nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    TotalRecipients = table.Column<int>(type: "INTEGER", nullable: false),
                    SuccessfulDeliveries = table.Column<int>(type: "INTEGER", nullable: false),
                    FailedDeliveries = table.Column<int>(type: "INTEGER", nullable: false),
                    ErrorDetails = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    SentAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CuratedJson = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NewsletterEditions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "NewsletterSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    IsAutoSendEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    SendHour = table.Column<int>(type: "INTEGER", nullable: false),
                    SendMinute = table.Column<int>(type: "INTEGER", nullable: false),
                    TimeZoneId = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    SenderName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    SenderEmail = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    AdminTestEmail = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    LastRunAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    NextRunAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LastRunStatus = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NewsletterSettings", x => x.Id);
                });

            migrationBuilder.Sql("UPDATE NewsletterSubscribers SET UnsubscribeToken = lower(hex(randomblob(16))) WHERE UnsubscribeToken = '' OR UnsubscribeToken IS NULL;");

            migrationBuilder.CreateIndex(
                name: "IX_NewsletterSubscribers_UnsubscribeToken",
                table: "NewsletterSubscribers",
                column: "UnsubscribeToken",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NewsletterEditions_EditionDate",
                table: "NewsletterEditions",
                column: "EditionDate",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NewsletterEditions");

            migrationBuilder.DropTable(
                name: "NewsletterSettings");

            migrationBuilder.DropIndex(
                name: "IX_NewsletterSubscribers_UnsubscribeToken",
                table: "NewsletterSubscribers");

            migrationBuilder.DropColumn(
                name: "UnsubscribeToken",
                table: "NewsletterSubscribers");

            migrationBuilder.DropColumn(
                name: "UnsubscribedAt",
                table: "NewsletterSubscribers");
        }
    }
}
