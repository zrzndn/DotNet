using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpsHub.Migrations
{
    /// <inheritdoc />
    public partial class AddTicketDetails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AssignedTechnician",
                table: "SupportTickets",
                type: "TEXT",
                maxLength: 150,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "SupportTickets",
                type: "TEXT",
                maxLength: 2000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "DueAtUtc",
                table: "SupportTickets",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Requester",
                table: "SupportTickets",
                type: "TEXT",
                maxLength: 150,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ResolutionNotes",
                table: "SupportTickets",
                type: "TEXT",
                maxLength: 2000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAtUtc",
                table: "SupportTickets",
                type: "TEXT",
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE SupportTickets
                SET Description = 'Legacy request imported before descriptions were tracked.'
                WHERE Description = '';
                """);

            migrationBuilder.Sql(
                """
                UPDATE SupportTickets
                SET Requester = 'Unspecified'
                WHERE Requester = '';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AssignedTechnician",
                table: "SupportTickets");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "SupportTickets");

            migrationBuilder.DropColumn(
                name: "DueAtUtc",
                table: "SupportTickets");

            migrationBuilder.DropColumn(
                name: "Requester",
                table: "SupportTickets");

            migrationBuilder.DropColumn(
                name: "ResolutionNotes",
                table: "SupportTickets");

            migrationBuilder.DropColumn(
                name: "UpdatedAtUtc",
                table: "SupportTickets");
        }
    }
}
