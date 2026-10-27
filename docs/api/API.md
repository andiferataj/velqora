# API reference
All browser endpoints use the session cookie. Mutations require a fresh X-XSRF-TOKEN header obtained from GET /api/auth/csrf. Login itself is CSRF-protected. Responses use camelCase property names and enum names such as High and Investigating. Timestamps are UTC.

Machine ingestion uses X-Ingestion-Key instead of browser authentication. Ingestion is the only API mutation exempt from CSRF.

## Routes
| Method | Path | Permission / behavior |
| --- | --- | --- |
| GET | /health/live | Process liveness |
| GET | /health/ready | API database readiness |
| GET | /api/auth/csrf | Anonymous; returns CSRF token and cookie |
| POST | /api/auth/login | Email/password; login throttle and lockout |
| POST | /api/auth/logout | Signed-in account |
| GET | /api/auth/me | Current ID, email, roles |
| GET | /api/users | Signed-in; account IDs/emails/roles for assignment |
| POST | /api/users | Admin; create local account |
| POST | /api/ingest/events | Machine key; array of 1–1000 version-1 events |
| GET | /api/events | Signed-in; paginated search |
| GET | /api/events/{id} | Event details including normalized attributes |
| GET | /api/events/export | CSV, same filters, maximum 50,000 records |
| GET | /api/alerts | Signed-in; paginated triage search |
| GET | /api/alerts/{id} | Alert, rule, correlated evidence |
| PUT | /api/alerts/{id} | Operator; version, status, ownerId, feedback |
| GET | /api/incidents | Signed-in; paginated case search |
| POST | /api/incidents | Operator; title, summary, severity, alertIds |
| GET | /api/incidents/{id} | Incident, linked alerts, timeline |
| PUT | /api/incidents/{id} | Operator; version, status, severity, ownerId, summary |
| POST | /api/incidents/{id}/comments | Operator; text |
| POST | /api/incidents/{id}/alerts | Operator; alertId |
| GET | /api/incidents/{id}/report | Structured JSON report, audited |
| GET | /api/assets | Signed-in; paginated inventory search |
| POST | /api/assets | Admin; register synthetic lab asset |
| GET | /api/assets/{id} | Asset, recent alerts/events, recent-history risk count |
| GET | /api/rules | Signed-in; rule definitions |
| POST | /api/rules | Admin; create rule |
| PUT | /api/rules/{id} | Admin; versioned configuration |
| GET | /api/playbooks | Signed-in; definitions |
| POST | /api/playbooks | Admin; name, description, action, trigger, minimumSeverity, enabled |
| PUT | /api/playbooks/{id} | Admin; versioned configuration |
| GET | /api/playbook-runs | Signed-in; paginated requests/executions |
| POST | /api/playbook-runs | Operator; playbookId, incidentId, assetId |
| POST | /api/playbook-runs/{id}/decision | Different operator; version, approve |
| GET | /api/scenarios | Signed-in; scenario catalog |
| GET | /api/scenario-runs | Latest 100 runs with detection counts |
| POST | /api/scenario-runs | Operator; scenario name |
| POST | /api/scenario-runs/{id}/reset | Operator; version; preserve old evidence and start a new run |
| PUT | /api/scenario-runs/{id} | Operator; version and pause/resume/cancel action |
| GET | /api/analytics | Signed-in; days=1–90 |
| GET | /api/platform | Signed-in; database/cache/queue/worker/outbox health |
| GET | /api/audit | Admin; paginated, optional resource |
| POST | /api/platform/replay-dead-letters | Admin; replay up to 100 valid IDs |
| WebSocket | /hubs/soc | Signed-in SignalR, changed notification |

Operator means Admin or Analyst. ReadOnly may inspect and export data but cannot modify cases, triage, rules, lab runs or responses.

## Search and pagination
Page endpoints return items, total, pageNumber and pageSize. page defaults to 1; size defaults to 25 and is capped at 100.

Events support q (device/account/source substring), type, from, to and synthetic. Alerts support q, status and severity. Incidents support q and status. Assets support q. Timestamps should include an offset; prefer ISO 8601 UTC strings.

## Event example
```json
[
  {
    "id": "e1431b7a-a286-46de-b44e-59e001cbb003",
    "schemaVersion": 1,
    "occurredAt": "REPLACE_WITH_CURRENT_UTC_TIMESTAMP",
    "source": "authorized-lab",
    "type": "auth.failure",
    "device": "WS-PRISHTINA-01",
    "account": "a.ferataj",
    "ipAddress": "192.0.2.10",
    "attributes": {"reason": "bad_password"},
    "synthetic": true
  }
]
```

A successful response contains accepted and duplicates. Retrying the same ID and canonical payload does not insert another event. Reusing an ID with different data returns 409. Attribute ordering is canonicalized. Scenario run IDs are assigned internally by the simulator and cannot be forged through this input contract.

Event limits: last seven days, no more than two minutes into the future, 24 attributes, 64-character keys, 512-character values. Device/source/type/account and request body limits are enforced server-side. Unknown event types can be stored, but only matching configured rules detect them.

## Playbook policy
Actions: isolate-device, lock-account, notify, assign-approver, escalate-incident.

Triggers: Manual or IncidentCreated. Incident-created proposals require severity at least minimumSeverity and a linked synthetic asset. Every run begins Pending. A manual requester cannot approve their own request. The action/version are snapshotted into the run; current enabled state is checked again at execution.

## Error handling
Validation returns a Problem Details response with a traceId. Authentication returns 401, authorization 403, missing resources 404, stale versions 409, throttling 429. Unexpected failures return a generic 500 and are logged with a request identifier. Error bodies never intentionally return connection strings or passwords.
