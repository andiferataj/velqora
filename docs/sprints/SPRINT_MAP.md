# Sprint mapping and learning guide
The original program has sprints 0–15. Source coverage is not sprint acceptance: each gate still needs the tests listed below.

| Sprint | Source delivered | Gate still required |
| --- | --- | --- |
| 0 · Environment | Solution, pinned toolchain, conventions, Git workspace, docs | Fresh restore/build and green CI |
| 1 · Backend | API, worker, EF migration, Compose dependencies, health | All services healthy |
| 2 · Ingestion | Schema, validation, idempotency, outbox, quorum queue | 1,000-event durable pipeline and duplicate recovery |
| 3 · Dashboard | Angular routes, event explorer, live hub, responsive CSS | Angular compile and browser QA |
| 4 · Identity | Identity accounts, roles, session/CSRF/throttle/audit | Unauthorized and role-boundary tests |
| 5 · Detection | Threshold/sequence/privilege/telemetry rules | Real Redis/PostgreSQL acceptance and rule-edit cases |
| 6 · Alerts | Evidence, assignment, triage, feedback | Full investigation test |
| 7 · Incidents | Board, cases, evidence, notes, concurrency | Two-analyst and report acceptance |
| 8 · Inventory | Assets, identities, history, recent risk, last-seen | Inventory/history acceptance |
| 9 · Automation | Conditional proposals, approval, action snapshot, simulated actions | Approval, rejection, edit-after-request and retries |
| 10 · Lab | Five scenarios, progress, pause/resume/cancel/reset/replay | Scenario and concurrent control tests |
| 11 · Reporting | Counts/trends/precision/MTTR, CSV/JSON | Aggregation reconciliation and export tests |
| 12 · Observability | OTLP, collector, Prometheus/Grafana/Jaeger | One trace across ingest/queue/detection; metric-name verification |
| 13 · Hardening | Threat model, bounds, security policies, outage script | Recovery tests, retention implementation, advisory review |
| 14 · Performance | Core checks, xUnit/Testcontainers/Playwright/k6 source | Full passing suites and measured benchmarks |
| 15 · Release | Docker builds, setup/dev/backup/restore, demo/docs/CI | Clean-clone rehearsal, snapshots/locks, GitHub CI |

## Concepts to study in order
1. Core/Contracts.cs: validation happens before storage. Stable IDs let a caller retry safely.
2. IngestionService.cs: a database transaction commits the event and work-to-publish together.
3. PipelineWorker.cs: broker confirmation and consumer acknowledgement protect different crash windows.
4. DetectionService.cs: the inbox makes redelivery safe; event-time rules handle delayed arrivals.
5. InvestigationEndpoints.cs: durable cases connect evidence, people and investigation history.
6. SocDb.cs: concurrency versions prevent silent overwrites; immutable history preserves collaboration.
7. OperationsService.cs: approval and action snapshots prevent unsafe workflow substitutions.
8. Angular api.ts/page.ts: authenticated requests, CSRF, live notifications, polling, error states.
9. PipelineTests.cs and acceptance.spec.ts: behavior becomes credible through real-service tests.
10. Architecture and security docs: explain the limits and why the first deployment stays modular.

## Commit policy
Use meaningful commits after verified increments. The initial source commit is not a declaration that all sprints passed. Do not use the roadmap's release commit wording until acceptance and release rehearsal succeed.
