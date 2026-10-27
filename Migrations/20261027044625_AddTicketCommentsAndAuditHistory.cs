using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpsHub.Migrations
{
    /// <inheritdoc />
    public partial class AddTicketCommentsAndAuditHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TicketAuditEvents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TicketId = table.Column<int>(type: "INTEGER", nullable: false),
                    EventType = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    ActorId = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    OccurredAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TicketAuditEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TicketAuditEvents_SupportTickets_TicketId",
                        column: x => x.TicketId,
                        principalTable: "SupportTickets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TicketComments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TicketId = table.Column<int>(type: "INTEGER", nullable: false),
                    Content = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TicketComments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TicketComments_SupportTickets_TicketId",
                        column: x => x.TicketId,
                        principalTable: "SupportTickets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TicketAuditEvents_TicketId_OccurredAtUtc",
                table: "TicketAuditEvents",
                columns: new[] { "TicketId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_TicketComments_TicketId_CreatedAtUtc",
                table: "TicketComments",
                columns: new[] { "TicketId", "CreatedAtUtc" });

            migrationBuilder.Sql(
                """
                INSERT INTO "TicketAuditEvents"
                    ("TicketId", "EventType", "Description", "OccurredAtUtc", "ActorId")
                SELECT "Id", 'TicketCreated',
                    'Ticket created (history migrated from ticket record).',
                    "CreatedAtUtc", NULL
                FROM "SupportTickets";
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TicketAuditEvents");

            migrationBuilder.DropTable(
                name: "TicketComments");
        }
    }
}
