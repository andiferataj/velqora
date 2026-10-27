# Velqora SOC
Security operations, threat detection, and incident response for a local, authorized security lab.

Velqora follows a security event from ingestion through detection, analyst investigation, incident management, and approval-gated simulated response. The Angular dashboard reads the ASP.NET Core API; a separate worker processes durable RabbitMQ deliveries. PostgreSQL is the system of record and Redis holds rebuildable last-seen state.

**Current delivery status:** All seven local release gates pass: builds, 35 unit tests, two integration tests, all six browser workflows, dependency audits, load, outage/restart recovery, backup restoration and monitoring. The monitored Compose load stored all 3,000 events at 100 events/sec with 20.47 ms p95 ingestion latency. Ready for local use and repository publication; Git commit/remote/GitHub CI are not yet completed. See [implementation status](docs/IMPLEMENTATION_STATUS.md) and [release evidence](docs/release/local-acceptance-2026-10-26.json).

## Start locally
Use PowerShell 7, Docker Desktop with Linux containers/WSL2, and an internet connection for the first image build. Allow approximately 8 GB of RAM for Docker without monitoring, 12 GB with monitoring, and 15 GB of free disk space. These are setup budgets, not measured runtime requirements.

From this folder:

```powershell
./infrastructure/scripts/bootstrap.ps1 -Start
```

PostgreSQL uses host port `55432` so it can run alongside projects using `5432`. Set `POSTGRES_PORT` in `.env` to override it; developer mode uses the same setting.

Open **http://localhost:4200**. Accounts are:

| Account | Role |
| --- | --- |
| admin@velqora.local | Admin |
| analyst@velqora.local | Analyst |
| viewer@velqora.local | ReadOnly |

Read `DEMO_PASSWORD` from the generated `.env` locally. All three initial lab accounts use that generated password. It is never committed. Changing the environment value later does not change existing account passwords.

To include monitoring:

```powershell
./infrastructure/scripts/bootstrap.ps1 -Start -Monitoring
```

The first startup applies the initial EF Core migration and seeds rules, playbooks, accounts, and three sample assets. It does not invent historical event data. Use the Simulation Lab to populate the dashboard.

## Demonstrate the full workflow
1. Sign in as the analyst and open **Simulation Lab**.
2. Run **Login abuse**: 12 failed logins followed by a successful login.
3. Expect the repeated-failure and success-after-abuse rules to create two alerts.
4. Investigate the critical alert, review all 13 evidence events, and create an incident.
5. Add a note, move the case to Investigating, and request **Contain workstation**.
6. Sign in as the admin in a separate browser profile or after signing out.
7. Open **Response playbooks**, approve the pending request, and wait for Executed.
8. Inspect the case asset: the local simulated Isolated flag is true. Export the incident report to review evidence, timeline, and response.

See the detailed [walkthrough](docs/DEMO_WALKTHROUGH.md). No response adapter contacts a real device or identity provider.

## Technology and boundaries
| Layer | Implementation |
| --- | --- |
| Runtime | .NET SDK 10.0.300, C#, ASP.NET Core |
| Frontend | Angular 21.2, TypeScript 5.9, Angular Material, CDK |
| Persistence | PostgreSQL 17, EF Core / Npgsql |
| Messaging | RabbitMQ 4, durable quorum queue, publisher confirms |
| Cache | Redis 7, rebuildable last-seen keys |
| Live updates | Authenticated SignalR notifications plus REST refresh |
| Authentication | ASP.NET Core Identity, HttpOnly session cookie, CSRF tokens |
| Telemetry | OpenTelemetry → Collector → Jaeger / Prometheus → Grafana |
| Tests | Executable core checks, xUnit v3, Testcontainers, Playwright, k6 |
| Deployment | Docker Compose, localhost bindings, generated local secrets |

The API combines identity, ingestion, alerts, incidents, assets, and configuration as modules. The worker combines dispatch, detection, simulation, automation, and health reporting. This follows the plan's initial modular deployment option. There is no YARP gateway or separate service for every domain. [Architecture and tradeoffs](docs/architecture/ARCHITECTURE.md).

## Developer mode
Install the pinned .NET SDK and Node 24.15.0. Then:

```powershell
npm ci
dotnet restore Velqora.slnx
./infrastructure/scripts/dev.ps1 infra
```

Use separate terminals:

```powershell
./infrastructure/scripts/dev.ps1 api
./infrastructure/scripts/dev.ps1 worker
./infrastructure/scripts/dev.ps1 web
```

Do not run the Compose API/worker alongside developer instances unless you intend to exercise multi-instance concurrency.

## Release verification
Run `./infrastructure/scripts/release.ps1` for all seven release gates and saved evidence. This rebuilds the corrected images and includes recovery, disposable backup restoration, monitoring and load acceptance. See [release details](docs/RELEASE.md).

## Verification
```powershell
# Runs without downloading application dependencies:
./infrastructure/scripts/verify.ps1 -Offline

# Build/unit/model and static browser checks without Docker engine access:
./infrastructure/scripts/verify.ps1 -WithoutContainers -UiSmoke

# Requires network access and Docker for Testcontainers:
./infrastructure/scripts/verify.ps1

# Requires the full Compose application running:
./infrastructure/scripts/verify.ps1 -E2E

# Explicitly stops/restarts this project's broker and worker:
./infrastructure/scripts/resilience.ps1

# Generate 1,000 additional synthetic events:
./infrastructure/scripts/generate-events.ps1 -Count 1000
```

The offline verifier performs core behavior checks, C# syntax checks, and Compose validation. Build output used by the verification script is written to .local/dashboard-build to support restricted Windows execution sessions. It does not compile the API/worker or Angular templates. The full verifier fails on the first failed layer. CI contains backend, database/broker/cache integration, browser, and failure-recovery jobs. All seven gates, including monitored container recovery, backup restoration and observability, passed on 2026-10-26. Run the release command again after material changes.

[Testing and performance](docs/TESTING.md) describes coverage, commands, and the unmeasured benchmark baseline.

## Local endpoints
| Service | Address |
| --- | --- |
| Dashboard | http://localhost:4200 |
| API | http://localhost:8080 |
| Worker developer health | http://localhost:8081/health/ready |
| RabbitMQ management | http://localhost:15672 |
| Grafana (monitoring profile) | http://localhost:3000 |
| Jaeger (monitoring profile) | http://localhost:16686 |
| Prometheus (monitoring profile) | http://localhost:9090 |
| PostgreSQL | localhost:55432 |
| Redis | localhost:6379 |

RabbitMQ and Grafana usernames are `velqora`; their generated passwords are in `.env`. Management services bind to loopback. [API reference](docs/api/API.md) and [event schema](docs/api/security-event.v1.schema.json).

## Operations
`docker compose down` stops the application and keeps named data volumes. Avoid `down -v` for data you want to retain.

`./infrastructure/scripts/backup.ps1` creates a PostgreSQL custom-format dump. Restore requires an explicit replacement switch:

```powershell
./infrastructure/scripts/restore.ps1 -Backup .local/backups/YOUR_BACKUP.dump -ReplaceDatabase
```

[Operations guide](docs/OPERATIONS.md) covers backups, recovery, data retention, and troubleshooting. Do not expose the default local HTTP configuration on a public network.

## Repository
- `src/backend/Velqora.Core`: contracts, domain entities, deterministic scenarios, pure detection rules.
- `src/backend/Velqora.Infrastructure`: EF mappings and migration, Identity persistence, messaging, detection and operations services.
- `src/backend/Velqora.Api`: authenticated REST modules, live hub, reporting.
- `src/backend/Velqora.Worker`: outbox dispatch, RabbitMQ consumer, simulation/automation cycles.
- `src/frontend/velqora-dashboard`: routed analyst application.
- `tests`: shared behavioral specifications, xUnit wrappers, Testcontainers integration, browser acceptance, k6 load scenario.
- `infrastructure`: images, Compose support, monitoring, PowerShell scripts.
- `docs`: status, sprint mapping, architecture, API, demo, security and operational guidance.
