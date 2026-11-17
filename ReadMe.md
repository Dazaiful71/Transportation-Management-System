<p align="center">
  <img src="Transportation%20Management%20System/img/logo_gray.png" alt="TMS logo" width="220" />
</p>

# Transportation Management System

[![CI](https://github.com/Dazaiful71/Transportation-Management-System/actions/workflows/ci.yml/badge.svg)](https://github.com/Dazaiful71/Transportation-Management-System/actions/workflows/ci.yml)
![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4)
![WPF](https://img.shields.io/badge/UI-WPF-0C54C2)
![MySQL](https://img.shields.io/badge/database-MySQL%208-4479A1)
![License: AGPL v3](https://img.shields.io/badge/license-AGPL--3.0-blue)

A Windows desktop app for running a freight brokerage along the Windsor–Ottawa corridor in Ontario.
Buyers take contracts from a shared marketplace and turn them into orders. Planners assign carriers and
track trips until delivery. Admins manage carriers, rates, routes, logs and database backups. Every
completed order produces an invoice, and the planner can report on revenue and export it to CSV.

---

## Features

| Role | What they can do |
| --- | --- |
| **Buyer** | Browse marketplace contracts, accept them as orders, add new clients automatically, view active/completed orders, and generate invoices for completed orders. |
| **Planner** | See carriers with capacity in the origin city, assign one or more carriers (each becomes a trip), follow progress, complete orders, view an invoice report with totals, and **export it to CSV**. |
| **Admin** | Add, edit and deactivate carriers and their depot cities; edit OSHT mark-ups and the route table; change the database connection and log folder; back up the database to a `.sql` file; read the activity log. |

Under the hood:

- **Trip planning** splits a journey into corridor legs and simulates the driver's day: 2 h to load, 2 h to unload,
  2 h stops at every city in between for LTL, a maximum of 8 h driving and 12 h on duty per day, and overnight rest.
- **Pricing**: FTL is `rate × km`, LTL is `rate × km × pallets`. The OSHT mark-up goes on top, and reefer vans add the
  carrier's reefer percentage.
- **Security**: passwords are stored as BCrypt hashes, every SQL query is parameterized, and no credentials live in the repository.

## Tech stack

- **C# / .NET 8** with **WPF** (plus a WinForms invoice viewer)
- **MySQL 8** through `MySql.Data`, backups with `MySqlBackup.NET`
- **BCrypt.Net-Next** for password hashing
- **MSTest** for unit and integration tests
- **GitHub Actions** CI that builds, tests and publishes a Windows build on every push

## Getting started

### 1. Requirements

- Windows 10 or 11
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- MySQL 8, either installed locally or run with Docker (below)

### 2. Create the database

**With Docker** (easiest):

```bash
docker compose up -d
```

This starts MySQL on port 3306 and loads the schema, sample data and a local contract marketplace.

**With your own MySQL server:**

```bash
mysql -u root -p < database/01_schema.sql
mysql -u root -p < database/02_seed.sql
mysql -u root -p < database/03_marketplace.sql
```

`03_marketplace.sql` is optional. Use it if you don't have access to a shared contract marketplace.

### 3. Configure the connection

Copy the example settings file and fill in your details:

```bash
copy "Transportation Management System\appsettings.local.example.config" "Transportation Management System\appsettings.local.config"
```

The defaults already match the Docker container. `appsettings.local.config` is git-ignored, so your passwords stay on your machine.

Settings are read in this order:

1. Environment variables: `TMS_SERVER`, `TMS_PORT`, `TMS_USER`, `TMS_PASSWORD`, `TMS_DATABASENAME`, and `TMS_CMPSERVER`, `TMS_CMPUSER`, `TMS_CMPPASSWORD`, … for the marketplace
2. `appsettings.local.config` next to the executable
3. `App.config`

### 4. Run it

```bash
dotnet run --project "Transportation Management System"
```

Sign in with one of the sample accounts from `database/02_seed.sql`:

| Username | Password | Role |
| --- | --- | --- |
| `admin` | `admin` | Admin |
| `buyer` | `buyer` | Buyer |
| `planner` | `planner` | Planner |

Change these passwords before using the app with real data.

## Tests

```bash
# Fast unit tests (no database needed), the same set CI runs
dotnet test --filter "TestCategory!=Integration"

# Everything, including tests against a live database (start MySQL and configure it first)
dotnet test
```

The unit tests cover route and time calculation, pricing (FTL, LTL, reefer, mark-ups, multi-trip orders),
the CSV export and report summary, configuration loading, and connection-string safety.

## Project layout

```
Transportation Management System/
├── Controllers/        Role logic (Admin, Buyer, Planner), TripManager, ReportService, AppSettings, Logger
├── Models/             Domain types (Order, Trip, Carrier, Invoice, ...) and the DAL (all SQL lives here)
├── Views/              WPF windows for login, admin, buyer and planner, plus the invoice viewer
└── App.config          Non-secret defaults
UnitTestProject1/       MSTest unit and integration tests
database/               Schema, sample data and local marketplace scripts
docker-compose.yml      Local MySQL for development
.github/workflows/      CI pipeline
```

## Data model

```
Clients 1─* Orders 1─* Trips *─1 Carriers 1─* CarrierCity
                │
                └─1 Invoices
Routes  (one row per corridor leg)       Rates  (OSHT mark-ups)       Users  (sign-in + role)
```

Cities are numbered west to east: Windsor 0, London 1, Hamilton 2, Toronto 3, Oshawa 4, Belleville 5, Kingston 6, Ottawa 7.

## Changelog

### 2.0.0

- Moved from .NET Framework 4.7.2 to **.NET 8** with SDK-style projects. It now builds with `dotnet build`, and Visual Studio is no longer required.
- Removed committed NuGet packages and build output. Packages are now restored on build.
- **Security:** fixed SQL injection in sign-in and role lookups, removed hard-coded database credentials from the code and config,
  and built connection strings safely.
- **Pricing fixes:** multi-trip orders are now billed for every trip instead of only the last one. LTL now uses the configured
  OSHT LTL rate and charges per pallet. The reefer charge is applied as a percentage instead of multiplying the price down.
- **Routing fixes:** distances now count each corridor leg exactly once in both directions, rows are linked by city instead of
  table order, and time follows explicit driver hours rules.
- Added CSV export and a summary line (count, revenue, average, top client) to the Planner's invoice report.
- Fixed `Rate` throwing `NullReferenceException` from its value constructor. Invoices now record the pallet count.
- Added database scripts (schema, sample data, local marketplace), Docker Compose, GitHub Actions CI, and 55 new unit tests.

## License

Released under the [GNU Affero General Public License v3.0](LICENSE).
