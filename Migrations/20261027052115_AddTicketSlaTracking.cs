using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpsHub.Migrations
{
    /// <inheritdoc />
    public partial class AddTicketSlaTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ResolvedAtUtc",
                table: "SupportTickets",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SlaDueAtUtc",
                table: "SupportTickets",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "SlaMet",
                table: "SupportTickets",
                type: "INTEGER",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ResolvedAtUtc",
                table: "SupportTickets");

            migrationBuilder.DropColumn(
                name: "SlaDueAtUtc",
                table: "SupportTickets");

            migrationBuilder.DropColumn(
                name: "SlaMet",
                table: "SupportTickets");
        }
    }
}
