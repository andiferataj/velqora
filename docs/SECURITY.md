# Security boundaries and review notes
This is a single-workspace localhost portfolio lab. Only authorized or explicitly synthetic events should be submitted.

## Controls in source
- Identity password hashing, local roles and eight-hour session cookies.
- Server authorization, CSRF checks on browser API mutations, global/login rate limits and account lockout.
- Bounded event schemas, batch/body limits and parameterized EF queries.
- Constant-time digest comparison for the ingestion key.
- Two-operator manual response approval and execution-time checks.
- Snapshotted actions, local synthetic device/account changes, append-only timeline/audit protections.
- CSV formula neutralization and safe JSON case export.
- Generated local secrets, .env exclusion and loopback service bindings.
- Non-root application containers and restrictive dashboard response headers.
- Outbox/inbox persistence and durable queue acknowledgement rules.

## Threat model
| Threat | Control / remaining limit |
| --- | --- |
| Anonymous case mutation | Cookie authentication and server role policies |
| Cross-site browser mutation | Antiforgery token and SameSite cookies |
| Read-only user changing state | Operator/Admin policies, negative acceptance tests |
| Stale analyst update | Version check plus database optimistic concurrency |
| Duplicate event delivery | Unique raw IDs, canonical hash checks, inbox record |
| Broker outage | Durable transactional outbox; fail/recovery test required |
| Unapproved containment | Pending state and independent approval checked again by worker |
| Playbook edited after request | Action and playbook version captured in request |
| Malicious spreadsheet cells | Formula escaping in CSV export |
| Audit deletion through app/database | Append-only guards and PostgreSQL triggers; database owner remains trusted |
| Redis data loss | Database detection evidence remains authoritative |
| Stolen localhost session | HttpOnly cookie, finite expiry; local HTTP remains a development limitation |
| Unbounded lab storage | Explicit retain-all policy; no automated retention job yet |

## Release review still required
Run all real-service and browser tests; inspect dependency advisories and image digests; verify monitoring and crash recovery; review cookie and proxy settings for the actual deployment; test backups. Password reset, MFA, passkeys, multi-tenancy, external response integrations, production retention and cryptographically anchored auditing are not implemented.

Do not publish the default HTTP/local credential arrangement as an internet service. Publishing requires a separate concrete deployment configuration and review.
