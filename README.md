# OpsHub

<<<<<<< HEAD
OpsHub is a web application built with **.NET 10 and Blazor**, using SQLite for local database storage.

## Tech Stack

- **Framework:** .NET 10
- **UI:** Blazor Interactive Server
- **Database:** SQLite
- **ORM:** Entity Framework Core
- **Language:** C#

## Project Structure

```text
OpsHub/
├── Components/   # UI components
├── Data/         # Database context
├── Migrations/   # Database migrations
├── Models/       # Data models
├── Services/     # Application services
├── tests/        # Automated tests
├── wwwroot/      # Static assets
├── Program.cs
└── OpsHub.csproj
```

## Getting Started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)

### Run Locally

```bash
git clone <repository-url>
cd OpsHub
dotnet restore
dotnet run
```

Open the local URL displayed in the terminal.

## Database

OpsHub uses SQLite, with the database stored in `opshub.db`. Entity Framework Core migrations are maintained in the `Migrations/` directory.

## Testing

Automated tests are maintained in the `tests/` directory.

## License

No license specified.
=======
OpsHub is an IT service desk built with .NET 10, Blazor Interactive Server, Entity Framework Core, and SQLite.

## Phase 1

The first implementation phase includes:

- Responsive service desk dashboard with ticket totals, status and priority summaries, urgent unresolved tickets, and overdue tickets.
- Ticket creation, detail view, editing, and deletion with confirmation.
- Ticket fields for description, requester, assigned technician, category, priority, status, due date, and resolution notes.
- Search by ticket ID, title, or requester; filters by status, category, and priority; and sorting by date, priority, or status.
- Server-side validation and status-transition rules, including reopening resolved tickets.
- SQLite persistence through an EF Core ticket service.

## Phase 2

- Persistent ticket comments with server-side validation and chronological display, kept separate from resolution notes and audit events.
- Append-only service-generated audit events for ticket creation and changes to status, priority, assignment, resolution, title, description, category, and due date.
- Chronological audit history on ticket details. The nullable `ActorId` field is reserved for future authentication integration; no actor identity is fabricated before authentication exists.
- Existing tickets receive a creation-history entry from their stored creation timestamp when the migration is applied.

## Phase 3

- ASP.NET Core Identity sign-in with Admin, Agent, and Requester roles, secure password policy, lockout, and antiforgery-protected logout.
- Server-side ticket-service authorization: Requesters can create and access only their own active tickets and comment; Agents can manage active support tickets; Admins can access all tickets, archived records, and user administration.
- Ticket ownership, comment actor IDs, and audit actor IDs are derived from the authenticated server-side identity. Existing records keep null ownership/actor values; no historical identities are inferred.
- Admins can create user accounts and change roles. Archiving is Admin-only and preserves ticket comments and audit history.
- Optional first-Admin provisioning uses environment configuration or user secrets; no default account or password is included.

The static GitHub Pages demo remains a separate future deliverable. It will use clearly labeled sample data and will not connect to this application's SQLite database.

## Phase 4: SLA tracking and reporting

- SLA deadlines are persisted per ticket and calculated from its UTC creation time. Changing priority recalculates the deadline and records an audit event; resolution records its UTC timestamp and whether the stored target was met. Reopening clears the current resolution result.
- Initial targets are elapsed calendar hours (24 hours a day, 7 days a week): Critical 4h, High 24h, Medium 72h, Low 120h. These are starting assumptions, not agreed business commitments. No working-hour calendars, holidays, pause rules, or response-time SLAs are modeled.
- Tickets within 2 hours of their deadline show as at risk; past-deadline unresolved tickets show as breached. Targets and the at-risk window can be changed in `appsettings.json` under `Sla` or overridden with standard ASP.NET Core configuration.
- Historical tickets are not assigned invented SLA deadlines. SLA compliance is reported only for resolved tickets with an actual recorded SLA result; historical resolved tickets without resolution timestamps still count toward resolution rate but not average resolution time.
- Dashboard reports can be filtered by inclusive UTC creation dates and show volume, resolution rate, average known resolution time, SLA compliance, and priority/status/category/technician breakdowns. Reports exclude archived tickets and use the same role/ownership scope as ticket viewing.
- Admins, Agents, and Requesters can export CSV; Requester exports include only their own active tickets. CSV fields are quoted/escaped, and formula-leading cell values are neutralized for spreadsheet safety.

## Run locally

Requirements: .NET 10 SDK.

```powershell
dotnet restore
dotnet ef database update
dotnet run
```

The application stores its database in `opshub.db` at the content root. EF Core migrations are additive and preserve existing tickets. The Phase 1 migration assigns legacy tickets an `Unspecified` requester and notes that their original description was not recorded. The Phase 2 migration adds separate comments and audit-history tables and backfills ticket-creation events using existing creation timestamps. Do not delete the database to apply schema changes.

The Identity migration creates the Identity tables and adds nullable ticket ownership, comment actor, and archive fields without assigning existing tickets to any account. The SLA migration adds nullable deadline and resolution fields without backfilling unknown historical values. Apply all pending migrations to an existing database before starting the updated app:

```powershell
dotnet ef database update
```

Configure both bootstrap values only when provisioning the first administrator; either use user secrets or set `Identity__BootstrapAdmin__Email` and `Identity__BootstrapAdmin__Password` in the environment. The password must satisfy the configured 12-character complexity policy. The app creates the Admin, Agent, and Requester roles at startup. Once the administrator account exists, remove the bootstrap password from configuration; subsequent startup does not reset that account's password.

```powershell
dotnet user-secrets init
dotnet user-secrets set "Identity:BootstrapAdmin:Email" "admin@example.com"
dotnet user-secrets set "Identity:BootstrapAdmin:Password" "<your-unique-password>"
```

## Tests

```powershell
dotnet test tests/OpsHub.Tests/OpsHub.Tests.csproj
```

The test project covers ticket validation, status transitions, SQLite-backed ticket service operations, comments, audit history, authenticated actor attribution, ownership and role restrictions, Identity bootstrap, administrator user-management rules, SLA deadlines/outcomes, reporting calculations, report authorization, and safe CSV output using isolated in-memory databases.

## Architecture

- `Components/Pages/`: interactive dashboard and ticket details page.
- `Components/Layout/`: application shell and navigation.
- `Models/SupportTicket.cs`: persisted ticket entity and field constraints.
- `Models/TicketComment.cs` and `Models/TicketAuditEvent.cs`: separate user comments and service-generated audit history.
- `Services/TicketService.cs`: asynchronous CRUD, role and ownership enforcement, comment persistence, audit recording, validation, and status-transition enforcement.
- `Services/IdentityBootstrapper.cs` and `Services/UserAdminService.cs`: role seeding, optional Admin provisioning, and Admin-only account management.
- `Services/SlaCalculator.cs`: priority target deadline and at-risk/breach calculation.
- `Services/TicketReport.cs`: report metrics and breakdown calculations.
- `Services/TicketCsvExporter.cs` and `Controllers/TicketExportController.cs`: safely formatted CSV export behind authenticated, ownership-scoped ticket queries.
- `Services/TicketRules.cs`: shared ticket validation and workflow rules.
- `Data/OpsHubDbContext.cs`: EF Core SQLite context.
- `Migrations/`: versioned database schema changes.
- `tests/OpsHub.Tests/`: unit and service tests.
>>>>>>> 2da1709 (Complete OpsHub SLA reporting and export)
