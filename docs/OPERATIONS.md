# Local operations
## Prerequisites and ports
Use Docker Desktop in Linux-container mode. PostgreSQL 55432 (override with POSTGRES_PORT in .env), Redis 6379, RabbitMQ 5672/15672, API 8080 and dashboard 4200 must be available. Monitoring additionally uses 3000, 9090 and 16686. Developer worker health uses 8081.

## Startup and secrets
bootstrap.ps1 creates random values only when .env does not exist, then optionally starts the stack. It does not overwrite existing credentials. Store .env outside source control and never paste its values into issue reports or screenshots.

Named PostgreSQL/RabbitMQ volumes keep their original initialized credentials. Editing .env after initialization without rotating service credentials causes authentication failures. Existing Identity passwords also do not change when DEMO_PASSWORD changes.

## Troubleshooting
| Symptom | Check |
| --- | --- |
| NuGet/npm host lookup fails | Restore internet/DNS access in the execution environment; retry the real install |
| Docker named-pipe permission denied | Confirm the shell/session can access Docker Desktop's Linux engine |
| API never becomes ready | docker compose logs api; inspect migration and database credentials |
| Events stored but no alerts | Check rule enablement/types, worker heartbeat, outbox and queue backlog |
| Consumer repeatedly fails | Inspect worker logs and dead-letter count; repair the cause before replay |
| CSRF errors after switching users | Reload the app; obtain a fresh /api/auth/csrf token after login |
| Live updates offline | Check /hubs/soc reverse proxy and session expiry; REST pages also refresh |
| Telemetry loss alert absent | Wait 30 seconds; this detector currently applies to the lab scenario only |
| Host port conflict | For PostgreSQL, set POSTGRES_PORT in .env and rerun bootstrap; developer mode reads the same setting. For other services, update the Compose binding and relevant development URLs |
| Grafana/Jaeger unavailable | Start with --profile monitoring |

## Backups and restore
backup.ps1 creates a PostgreSQL custom-format dump using pg_dump inside the database container, then copies the binary archive to the selected local folder. Back up .env securely as well. Data-protection keys affect cookie continuity; a database backup alone does not preserve active browser sessions.

restore.ps1 requires -ReplaceDatabase and stops the application while restoring into the existing local database. A failed restore leaves services stopped so the operator can inspect it. Test the procedure against a disposable deployment before relying on a backup. The release backup rehearsal passed on 2026-10-26 using a separate disposable database; the live database replacement path was not needed for that check.

## Recovery and retention
Published outbox records and inbox records survive worker restarts. Poison deliveries are routed by the quorum delivery limit to a dead-letter queue. Admin replay republishes up to 100 valid event IDs with publisher confirms; invalid bodies remain for investigation.

Default retention is explicit **retain-all** for events, evidence, audits, timelines and scenarios. There is no background cleanup job in the current source. Disk usage grows with generation volume. A future retention job must preserve referenced evidence, outstanding outbox work and idempotency semantics, and must be proven by recovery tests. Deleting data ad hoc is not part of routine startup.

Cancel stops scenario generation; reset cancels the active run and starts an isolated new one; replay starts an isolated new run after completion. A destructive reset is deliberately not automated. docker compose down preserves data; removing volumes deletes the local database and broker state.

## Exposure model
The configuration is for one localhost lab on an authorized machine. Public hosting requires HTTPS, secure-cookie enforcement, a trusted proxy policy, credential rotation, network isolation, updated dependencies and a separate deployment review.
