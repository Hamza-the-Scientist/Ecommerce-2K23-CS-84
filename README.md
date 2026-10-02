## Sprint 2: Catalog Data Foundation (local setup)

Design, ERD, routes and decisions are in [`docs/SPRINT_2.md`](docs/SPRINT_2.md).

### Prerequisites
- .NET SDK 8.0
- MySQL 8.0.16+ (CHECK constraints are enforced from this version). `docker compose up -d` starts one.
- `dotnet-ef` tool: `dotnet tool install --global dotnet-ef`

### Environment variables (never commit real values; see `.env.example`)

| Variable | Purpose |
|---|---|
| `ConnectionStrings__Default` | MySQL connection string |
| `Jwt__Key` | JWT signing key, at least 32 characters |
| `SEED_ADMIN_PASSWORD` | Password of the seeded admin `admin@sneakmart.local` |
| `MYSQL_ROOT_PASSWORD` | Only used by `docker-compose.yml` |

PowerShell example:
```powershell
$env:ConnectionStrings__Default = "Server=localhost;Port=3306;Database=sneakmart;User=root;Password=<your-password>"
$env:Jwt__Key = "<random 32+ char string>"
$env:SEED_ADMIN_PASSWORD = "<admin password>"
```
Bash: use `export NAME=value`.

### Database
```bash
# 1) generate the migration once (commit the Migrations/ folder)
dotnet ef migrations add InitialCatalog --project src/SneakMart.Api --output-dir Migrations
# 2) apply it
dotnet ef database update --project src/SneakMart.Api
```
`db/001_catalog_schema.sql` is the same schema as plain SQL for review.

### Seed demonstration data (reproducible on a clean database)
```bash
dotnet run --project src/SneakMart.Api -- --seed
```

### Run the API
```bash
dotnet run --project src/SneakMart.Api
```

### Run the tests (Docker must be running; tests start a throw-away MySQL container)
```bash
dotnet test tests/SneakMart.Tests
```
