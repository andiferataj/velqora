# Local release procedure

The release target is the localhost Velqora SOC portfolio lab described in README. All seven local release gates passed on 2026-10-26, including final container recovery, backup restoration and monitoring. The measured evidence is preserved in [local acceptance](release/local-acceptance-2026-10-26.json). Repeat these checks after material code or dependency changes.

## Run the release gates
Open a normal terminal in the project folder with Docker Desktop running in Linux-container mode. Use the installed .NET SDK, Node and PowerShell prerequisites from README.

```powershell
.\infrastructure\scripts\release.ps1
```

The command performs seven gates:
1. Build and start the full stack with monitoring.
2. Compile, run unit/model and isolated integration tests, test production CSP, and run all six browser workflows.
3. Validate locked NuGet dependencies and audit npm/NuGet.
4. Submit 3,000 synthetic events at 100/sec; check latency, stored count and durable drain.
5. Test broker outage, worker restart and Redis outage recovery.
6. Restore a database dump into a disposable database and inspect its schema, accounts, migration and append-only triggers.
7. Check actual exported metrics and a distributed trace joining API ingestion, outbox publication and worker consumption.

The command stops on a failed gate and writes .local/release/release-results.json. Detailed load and monitoring evidence is written beside it. Container image identities and one resource snapshot are captured after success. Keep the failure output when reporting a problem.

The outage tests briefly interrupt Velqora services. The load and acceptance tests append synthetic lab data. Named application volumes persist. The backup rehearsal restores into a separate generated database and removes that disposable database afterward.

## Review and publish the repository
After the seven local gates pass, review README, SECURITY, OPERATIONS and the implementation status. Confirm .env, local caches, build outputs and test artifacts remain ignored.

Commit from a terminal with Git metadata write access, add the repository URL you intend to use as origin, and push. Require both GitHub Actions jobs to pass before tagging the release. This authoring session has no remote configured and cannot write .git, so it has not created a commit, pushed code or produced a green GitHub run.

CI runs backend/integration and monitored browser/load/recovery/restore checks and uploads release evidence. Its temporary database volumes are removed after the job.

## Public deployment
This local release configuration uses loopback HTTP and simulated response. A public deployment needs a separate reviewed configuration with HTTPS, secure cookies, trusted reverse proxy handling, rotated secrets, image vulnerability/digest review, operational storage/retention limits and a backup policy.
