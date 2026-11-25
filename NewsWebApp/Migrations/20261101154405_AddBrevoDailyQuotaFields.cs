using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NewsWebApp.Migrations
{
    /// <inheritdoc />
    public partial class AddBrevoDailyQuotaFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "LastDeliveredAt",
                table: "NewsletterSubscribers",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LastDeliveredEditionId",
                table: "NewsletterSubscribers",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DailyEmailQuota",
                table: "NewsletterSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "EmailsSentToday",
                table: "NewsletterSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "QuotaResetDateUtc",
                table: "NewsletterSettings",
                type: "TEXT",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "QueuedCount",
                table: "NewsletterEditions",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastDeliveredAt",
                table: "NewsletterSubscribers");

            migrationBuilder.DropColumn(
                name: "LastDeliveredEditionId",
                table: "NewsletterSubscribers");

            migrationBuilder.DropColumn(
                name: "DailyEmailQuota",
                table: "NewsletterSettings");

            migrationBuilder.DropColumn(
                name: "EmailsSentToday",
                table: "NewsletterSettings");

            migrationBuilder.DropColumn(
                name: "QuotaResetDateUtc",
                table: "NewsletterSettings");

            migrationBuilder.DropColumn(
                name: "QueuedCount",
                table: "NewsletterEditions");
        }
    }
}
