# Local setup and demo accounts

## Docker Desktop

Use Linux containers and run from the repository root. Private repository access requires your GitHub credentials.

```powershell
./scripts/init-env.ps1
docker compose up --build --detach --wait --wait-timeout 180
docker compose ps
```

Web: `http://127.0.0.1:18082`. Database: `127.0.0.1:54331`, database/user `aperture`. The database password and demo instructor password are generated into ignored `.env`. Sign in with `instructor@aperture.test` and your local `DEMO_INSTRUCTOR_PASSWORD`. Students register through the UI. Do not paste `.env` into issues or commit it.

PowerShell 5.1 can invoke the initializer with `powershell -NoProfile -ExecutionPolicy Bypass -File ./scripts/init-env.ps1`. On Linux/macOS, create an ignored `.env` containing externally generated strong values for `POSTGRES_PASSWORD` and `DEMO_INSTRUCTOR_PASSWORD`; the instructor value must satisfy the documented Identity policy. Then run the same Compose command.

`WEB_PORT` and `DB_PORT` may be set in `.env` to avoid local conflicts. The API is not published from Compose. The project/volumes use the Aperture namespace. `docker compose stop` pauses containers; `docker compose down` removes containers/network while preserving volumes. Removing volumes deletes data and session keys, so do not use `down -v` to troubleshoot ordinary failures.

Demo provisioning is explicitly gated by Development and `Demo:Enabled`. Changing `.env` after account creation does not reset that user's password. For a genuinely new isolated demo, use a distinct Compose project and ports; preserve existing volumes.

## Native tools

Install .NET SDK 10 and Node 22.13+ or Node 24 LTS. From the root:

```powershell
./scripts/init-env.ps1
docker compose up -d db
dotnet tool restore
dotnet restore
./scripts/start-api.ps1
```

In a second terminal, run `cd frontend`, `npm ci`, then `npm run dev`. Use web port 5175 and API port 5082. The helper loads `.env`, enables migrations/fictional seeding and provisions the local instructor. Normal development uses one API process; stop it with Ctrl+C before replacing a Debug executable on Windows.

## Configuration

| Variable | Purpose |
| --- | --- |
| `ConnectionStrings__Exams` | Native/CI PostgreSQL connection string; required by API |
| `Database__Migrate` | Apply EF migrations on startup when true |
| `Database__Seed` | Optional empty-catalog fictional seeding, Development only |
| `Demo__Enabled`, `Demo__InstructorPassword` | Explicit local instructor provisioning |
| `DataProtection__KeyPath` | Persistent key directory, `/app/keys` in Compose |
| `EXAMS_TEST_CONNECTION` | PostgreSQL administrative connection for isolated test databases; requires CREATEDB |
| `E2E_BASE_URL` | Existing full-stack target for browser tests; otherwise Vite starts on 5175 |
| `PLAYWRIGHT_CHANNEL` | Optional installed browser channel, such as `msedge` |
| `DEMO_INSTRUCTOR_PASSWORD` | Browser test/screenshot credential; local config can read ignored `.env` |

## Troubleshooting

- If Docker reports a reserved Windows port, choose an unused `WEB_PORT`/`DB_PORT` and restart Compose. Do not stop the Library/Inventory services.
- Check `docker compose ps` and `docker compose logs api --tail 50`. Startup waits for database health, migrations and the API before Nginx becomes healthy.
- If Vite starts while its configuration is temporarily removed by a Git branch switch, restart Vite after synchronizing the checkout. The API proxy must serve JSON, not the SPA HTML fallback.
- Use `127.0.0.1` consistently. Mixing it with `localhost` creates different browser cookie hosts. A missing/expired CSRF token produces 400; refresh and retry.
- `npm ci` is the reproducible installation path. No legacy peer setting is required. Retry network failures without changing the lockfile; project lint ignores generated `.vite` caches.
- Attempts are never reset by refreshing, republishing or logging out. Resume through the overview/history. A finalized attempt cannot be reopened.
