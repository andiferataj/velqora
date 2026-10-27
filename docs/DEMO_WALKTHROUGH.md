# Demo walkthrough
## Before presenting
Start the Compose stack and inspect Platform health. The database, broker, cache and worker should be healthy. Use a fresh lab run so prior alerts cannot be confused with this demonstration.

## Analyst story
Sign in as analyst@velqora.local using the locally generated password.

Open Simulation Lab and run Login abuse. The run has its own LAB device and account. Wait for Completed and two alerts. The page shows generated event progress separately from detection count; Completed means event generation finished, not that every consumer has drained.

Open Alerts, search the LAB device, and investigate Success after login abuse. Show 12 failure events followed by one success. Explain that this correlation is a deterministic rule, not an AI prediction.

Save triage as Investigating and assign an operator. Add feedback if this were a false positive.

Create an incident. Add a note explaining the sequence and investigation decision. Move the case from Open to Investigating. Show the append-only timeline and version number.

Request Contain workstation against the linked LAB asset. The request remains Pending and cannot be approved by its requester.

## Approver story
Use a different browser profile or sign out, then sign in as admin@velqora.local. Open Response playbooks, approve the pending request, and wait for Executed.

Open the incident and asset inventory. Show the response timeline and simulated isolation flag. No real Windows firewall, account, or device was changed.

Export the case report. It contains the case, alerts, raw evidence, timeline and export timestamp. Show the admin audit trail for creation, edits, approval and execution.

## Optional demonstrations
- Run Benign baseline; default authentication rules should produce no alert.
- Run Telemetry loss; wait at least 30 seconds after the heartbeat before expecting a detection.
- Pause and resume a longer Network event spike run.
- Configure an IncidentCreated playbook with High minimum severity. A new qualifying case creates a pending proposal.
- Replay a scenario. Its new run/device namespace preserves the previous investigation.
- Run the explicit resilience script to test broker failure and worker restart.
- Open Jaeger and locate an event's trace; inspect Prometheus/Grafana while generating traffic.

## Interview points
The engineering challenge is preserving evidence across failures and preventing duplicate effects. Describe the outbox/consumer-inbox crash windows, version conflicts, server-enforced roles, and why local simulated containment is approval-gated.

Explain the tradeoff of serialized detection and database event windows. State the measured throughput only after running the benchmark. Distinguish implemented source from verified acceptance results using IMPLEMENTATION_STATUS.md.
