# Implementation status

## Release verdict
**All seven local release acceptance gates PASS. Ready for local use and repository publication.**
The full monitored Docker Compose release check completed on 2026-10-26. Its saved results, performance measurements, monitoring trace, image identities and resource snapshot are preserved in [the release evidence](release/local-acceptance-2026-10-26.json). Git commit, remote configuration and GitHub-hosted CI are separate publication steps and have not run.

## Evidence from 2026-10-26
| Check | Result |
| --- | --- |
| Full solution Release build | PASS; seven projects, zero warnings/errors |
| xUnit unit specifications | PASS; 35/35, zero skipped |
| Real-service integration | PASS; 2/2 in the release runner with disposable Testcontainers; earlier explicit external-service isolation mode also passed |
| Initial EF migration | PASS; executed on fresh PostgreSQL databases |
| Integration coverage | Durable ingestion/outbox, queue consumption/inbox, replay idempotency, correlated evidence, approval/action snapshots, concurrency, application/database audit protection, dead-letter routing/replay, malformed-body retention |
| EF relational model/snapshot | PASS; 22 tables, 173 columns, no pending model changes |
| C# source/schema checks | PASS; 30 files and 15 domain table comparisons |
| TypeScript syntax | PASS; 18 files |
| Angular production build | PASS; strict template/type checking |
| Production CSP browser smoke | PASS; screen stylesheet loads, desktop columns and mobile stacking work, no CSP/runtime violations or horizontal overflow |
| Browser acceptance | PASS; all 6 tests against the rebuilt monitored Compose stack |
| Browser workflow coverage | Analyst simulation through independent admin approval and containment; all five scenario outcomes; pause/resume/reset isolation; pending automatic proposals; role/CSRF boundaries; 1,000-event idempotency; stale case updates; analytics; every workspace page; report casing; mobile navigation |
| Ingestion load | PASS; 3,000 events, 100 scheduled events/sec for 30 seconds, zero failed batches, all 3,000 stored |
| Measured request latency | p50 13.53 ms; p95 20.47 ms; maximum 36.16 ms |
| Drain after load submission | PASS; 4.14 seconds to empty outbox/queue and durable processed count |
| npm dependency audit | PASS; zero known advisories at every severity |
| NuGet locked restore/full audit | PASS; low-and-above audit enabled, warnings treated as errors |
| Compose/script configuration | PASS; Compose validates and PowerShell scripts parse |
| Broker/cache outage and container restart | PASS; broker outage, worker restart, retained/duplicate events and durable processing during Redis outage |
| Backup restore rehearsal | PASS; dump restored into a disposable database, schema/accounts/migration/append-only triggers checked |
| Monitoring metrics/distributed traces | PASS; all seven expected metric families exported and a trace joins API, outbox and worker |
| All-container local acceptance | PASS; rebuilt monitored stack completed all seven gates |
| Git commit | BLOCKED in this session; .git metadata is read-only |
| Remote / GitHub CI | No remote configured; workflow prepared, no green GitHub run claimed |

## Verification environment
The final release runner rebuilt and started the full Linux-container Compose stack with monitoring on the Windows host. Browser acceptance and the measured load targeted http://localhost:4200. Integration tests used disposable Testcontainers. Earlier verification also exercised explicit external-service isolation mode and a temporary local Release API.

The final 30-second load accepted and stored all 3,000 events at 100 scheduled events/sec with zero failed batches. Request p95 was 20.47 ms, with durable backlog drain in 4.14 seconds after submission. The host has 12 logical CPUs and 16 GiB RAM. These are local acceptance measurements, not cloud capacity or per-alert detection latency. Image identities and a post-test resource snapshot are included in the preserved evidence; the snapshot is not a sustained-load resource profile.

## Fixes found during live acceptance
- Corrected Angular critical CSS optimization: its inline stylesheet onload handler was blocked by the production CSP. Kept the existing policy and disabled only critical CSS inlining.
- Fixed Npgsql grouped UTC-day analytics projection with a parameterized, explicitly UTC aggregate.
- Made exported case JSON use the same camelCase convention as the API.
- Added accessible navigation names and SVG icons.
- Corrected asynchronous/ambiguous acceptance locators and select control locators.
- Moved the database host port to 55432 with POSTGRES_PORT override, keeping developer mode consistent.
- Added real-service external integration mode with isolated resource cleanup, while preserving default disposable-container tests.
- Added repeatable load, backup restore, monitoring, cache/broker outage and consolidated release checks.
- Extended CI to run these checks and preserve release evidence.

## Final acceptance command
Run from the project folder in a regular PowerShell terminal that can access Docker Desktop:

```powershell
.\infrastructure\scripts\release.ps1
```

This rebuilds the corrected images, runs all seven release gates, stops at the first failure, and writes .local/release/release-results.json. Outage tests briefly restart Velqora services and create synthetic test events. The backup rehearsal restores into a newly created disposable database.

## Scope and release limits
Velqora is a single-workspace localhost security lab. Containment and account locks change synthetic asset state; notifications are local case timeline entries. Retention is retain-all. It does not include production EDR/identity adapters, multi-tenancy, MFA/password recovery, automated retention, enterprise SIEM certification, or a reviewed internet deployment.

Container image advisory/digest review and deployment-specific HTTPS/proxy/cookie hardening remain requirements for public hosting. No public production readiness claim is made.
