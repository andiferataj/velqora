# Changelog

## 0.1.0 - 2026-10-26
- Implemented the Velqora security event, detection, investigation and approval-gated simulated response workflow.
- Added authenticated Angular operations pages, deterministic lab scenarios, reporting, live updates and platform health.
- Added durable outbox/inbox delivery, evidence correlation, concurrency handling and append-only audit/timeline protection.
- Fixed production stylesheet loading, UTC analytics aggregation, exported JSON casing and navigation accessibility during live acceptance.
- Verified 35 unit cases, two real-service integration tests and six browser acceptance workflows.
- Measured 3,000-event ingestion at 100 events/sec with no failed batches and 20.47 ms p95 request latency against the monitored Docker Compose stack.
- Added consolidated release checks and CI coverage for recovery, disposable backup restoration, monitoring and load.

- Passed all seven local release gates, including broker/cache outages, worker restart, disposable backup restoration, exported metrics and cross-service traces.

Git commit, remote configuration and GitHub CI publication remain separate pending steps; see docs/IMPLEMENTATION_STATUS.md.
