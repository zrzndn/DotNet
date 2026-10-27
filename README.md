# OpsHub

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
