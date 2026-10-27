# Verification and benchmarks
## What currently passes
The Core project compiles without external packages. The executable specifications share the same source with the xUnit suite and currently pass 35/35 checks. They cover threshold boundaries, event windows, ordering, isolation, duplicate IDs, schema and payload validation, deterministic scenarios, CSV escaping, state transitions, and domain dependency boundaries.

SourceChecks uses the installed SDK's Roslyn parser to check all authored C# files. check-frontend.cjs parses TypeScript. Neither proves external-library type compatibility or Angular template correctness.

The Release solution build and Angular production build now pass. xUnit runs all 35 core cases. ModelChecks validates 22 tables, 173 columns and snapshot consistency. The static compiled-login smoke serves the exact Content Security Policy from nginx.conf. It checks applied grid styling, active screen stylesheets, desktop columns, mobile stacking, form/error feedback, and zero CSP violations/runtime errors. This catches the production-only blocked CSS onload handler found during Docker startup. It does not verify backend workflows.

## Full test layers
| Layer | Location | Purpose |
| --- | --- | --- |
| Pure behavior | tests/Specifications.cs | Domain and contract invariants |
| xUnit runner | tests/Velqora.UnitTests | CI discovery for the shared cases |
| Real integration | tests/Velqora.IntegrationTests | PostgreSQL migration, RabbitMQ transport, Redis cache, idempotency, detection, approvals, optimistic concurrency and audit protection |
| Browser/API acceptance | tests/E2E | Analyst workflow, independent approval, 1,000-event ingestion/idempotency, role boundaries, CSRF, stale edits |
| Failure recovery | infrastructure/scripts/resilience.ps1 | Broker outage, worker restart, durable storage and replayed ingestion |
| Load | tests/Performance/ingestion.js | Reproducible ingestion traffic and latency thresholds |

Run the commands in README. Integration tests create disposable real services with Testcontainers; they must not fall back to an in-memory database. E2E uses the running Compose application. Both real-service integration tests and all six browser acceptance tests now pass. Integration was explicitly run against new isolated databases/vhost on live PostgreSQL/RabbitMQ/Redis; default CI still provisions Testcontainers. The final monitored Compose release run also passed Docker outage/restore/monitoring checks and both default Testcontainers integration tests.

## k6 methodology
Set VELQORA_INGESTION_KEY to INGESTION_API_KEY from the local .env. Run:

```powershell
k6 run tests/Performance/ingestion.js
```

The baseline script attempts 10 requests per second, 10 events per request, for 30 seconds. It checks under 1% failed HTTP requests and p95 ingestion request latency below 1 second. These are test targets, not reported achievements.

Record CPU, RAM, disk, Docker resources, image versions, warmup, batch size, concurrent simulation activity, broker backlog, worker drain time, and dashboard freshness. Ingestion request latency does not measure detection latency. Verify the final stored/processed counts and compare alert identities before and after worker restarts.

The current global per-IP limit is 600 requests/minute, so change the workload and limiter together only through a documented performance configuration. Do not silently disable authentication or durability for a benchmark.

## Release gate
Restore and full builds must pass first. Then run unit, real-service integration, browser, outage, backup restore, monitoring and load verification. Capture actual results in IMPLEMENTATION_STATUS.md. Do not infer success from file generation, syntax checks, or CI configuration.

## Recorded load result
On 2026-10-26 the final Node runner submitted 300 batches of 10 events over 29.92 seconds to the monitored Docker Compose stack at localhost:4200. All 3,000 events were accepted, stored and durably processed. Request p50 was 13.53 ms, p95 20.47 ms, maximum 36.16 ms; remaining backlog drained in 4.14 seconds. This measures ingestion HTTP latency and durable drain, not per-alert detection latency or cloud capacity. Run tools/benchmark.cjs with the generated credential environment variables to repeat it. The consolidated release script does this automatically.
