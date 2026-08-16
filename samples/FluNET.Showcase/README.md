# FluNET Showcase

This directory is an executable catalog of FluNET's user-facing language and
runtime features. `program.flu` is the integrated application; `apps/` contains
small programs that isolate one area at a time. All generated state stays under
`output/` and `.state/`, which are intentionally ignored by Git.

Run commands from this directory so relative fixture and output paths remain
portable across Windows, macOS and Linux. Individual applications can also be
run directly from `apps/`; `flu run` discovers the nearest showcase fixture
root automatically.

## Quick start

Install the short command once from the repository root:

```powershell
./scripts/install-flu.ps1
```

```bash
./scripts/install-flu.sh
```

Then enter this directory and inspect or run the integrated application:

```text
cd samples/FluNET.Showcase
flu check program.flu
flu explain program.flu
flu graph program.flu
flu run program.flu -vv --store .state/values.db --queue .state/messages.db --blob .state/blobs
```

Without installing the tool, replace `flu` with:

```text
dotnet run --project ../../src/FluNET.Flu --
```

For example:

```text
dotnet run --project ../../src/FluNET.Flu -- run program.flu -v --store .state/values.db --queue .state/messages.db --blob .state/blobs
```

Verbosity is deliberately progressive: `-v` reports step progress, `-vv` adds
the plan and dependencies, and `-vvv` includes dispatch and safe result details.
The flag can appear before or after the file name.

## Application catalog

| Program | What it demonstrates | Runtime needs |
| --- | --- | --- |
| `program.flu` | Integrated local workflow: modules, control flow, ETL, reports, text, files, archives, durable state, blobs, queues, system data and notifications | Run with the three `.state` options above |
| `apps/01-language-tour.flu` | Human-readable sentences, `.`, `;`, assignment, text transformations, `IF`, `ELSE IF`, `ELSE`, `UNLESS`, bounded `WHILE`, `BREAK`, `CONTINUE` and `REPEAT` | Offline |
| `apps/02-data-etl.flu` | CSV input, filtering, sorting, projection, aggregates, JSON/CSV output and parallel `FOR EACH` | Offline |
| `apps/03-files-and-archives.flu` | File and directory copy/move, metadata queries, search, index, hash, ZIP, trash and restore | Offline filesystem |
| `apps/04-public-api-client.flu` | HTTP response envelopes, status assertions, API base aliases and typed JSON collections | Public network |
| `apps/05-durable-state.flu` | Durable key/value state, blob objects and queue messaging | `--store`, `--blob`, `--queue` |
| `apps/06-system-and-process.flu` | Runtime information, metrics, portable paths, time, temporary ownership and direct process execution | `EXECUTE` needs a process-enabled host |
| `apps/07-automation-daemon.flu` | Interval schedules, file watches and event-driven workflows | Automation command; daemon is long-running |
| `apps/08-modules-and-tasks.flu` | Local `IMPORT`, typed `TASK`/`RUN` and hygienic task output | Offline |
| `apps/09-advanced-collections.flu` | `SKIP`, `DEFAULT`, `MAP`, `GROUP`, `SUM`, `JOIN`, `MATCH` and richer pipelines | Offline |
| `apps/10-reliability-policies.flu` | Reusable `POLICY`, `WITH`, retry, timeout, continue-on-error, caching and `ONCE BY` | Offline |
| `apps/11-host-and-clipboard.flu` | Host diagnostics, environment input and clipboard round-trip | Native clipboard provider; changes clipboard text |
| `apps/12-operations-control-tower.flu` | 300-line operations application combining local ETL, public API enrichment, joins, policies, reports, parallel cards, indexing, archives and durable messaging | Public network plus `--store`, `--blob`, `--queue` |

The fixtures are deterministic and local. Only the API application requires
internet access. Host-dependent programs still pass `flu check`; execution is
controlled by the capabilities exposed by the active host.

## Run focused applications

Most examples are simply:

```text
flu run apps/01-language-tour.flu -v
flu run apps/02-data-etl.flu -v
flu run apps/03-files-and-archives.flu -v
flu run apps/08-modules-and-tasks.flu -v
flu run apps/09-advanced-collections.flu -v
flu run apps/10-reliability-policies.flu -vv
```

The durable application uses explicit local backends:

```text
flu run apps/05-durable-state.flu -vv --store .state/values.db --queue .state/messages.db --blob .state/blobs
flu run apps/12-operations-control-tower.flu -vv --store .state/control.db --queue .state/control-queue.db --blob .state/control-blobs
```

The public API and host-integration examples are opt-in:

```text
flu run apps/04-public-api-client.flu -vv
flu run apps/11-host-and-clipboard.flu -v
```

## Long-running automation

Automation has a separate compiler and runner:

```text
flu automation check apps/07-automation-daemon.flu
flu automation daemon apps/07-automation-daemon.flu --state .state/automation.json --interval 1s
```

The daemon emits a heartbeat every 30 seconds and watches JSON fixtures. Stop
it with the terminal interrupt for your platform.

## Desired state and reconciliation

The `desired-state/` directory demonstrates the two declarative execution
modes. Bootstrap creates disposable targets under `output/`; `ENSURE` replaces
a stale text snapshot, while `SYNC` reconciles a keyed JSON collection from
source to target.

```text
flu run desired-state/bootstrap.flu

flu ensure check desired-state/ensure-notes.flu
flu ensure apply desired-state/ensure-notes.flu

flu sync check desired-state/sync-users.flu
flu sync apply desired-state/sync-users.flu
```

Re-run the bootstrap program whenever you want to reset this demonstration.

## Tooling tour

Every normal program can be inspected without executing effects:

```text
flu check apps/09-advanced-collections.flu
flu fmt apps/09-advanced-collections.flu
flu explain apps/10-reliability-policies.flu
flu graph program.flu
flu capabilities program.flu
flu tools
```

The interactive session is available by running `flu` with no arguments. Use
`:begin`/`:end` for a manually entered block or `:paste` to import a multiline
program from the clipboard.

## Coverage map

The catalog covers the major portable surface families:

- language composition: punctuation, pipelines, modules, tasks and policies;
- runtime flow: branches, bounded loops, repetition and parallel item actions;
- data: Text, JSON and CSV, projections, defaults, paging, grouping, joins and aggregates;
- operating system: files, directories, metadata, search, hashes, archives, paths, time, temp artifacts, environment and clipboard;
- integrations: HTTP, notifications, direct processes and host diagnostics;
- persistence: key/value state, SQLite-backed queue selection, blob storage, cache and idempotency;
- services: interval/file-watch automation, desired state and reconciliation;
- tooling: check, format, explain, graph, capability analysis and verbosity levels.

Some engine integrations are intentionally host-owned rather than configured by
the generic CLI: SQL executors, secret stores, authenticated transports,
environment mutation, process sessions and custom event/message providers. See
`samples/FluNET.HostedApp` for the embedding boundary; those providers use the
same language compiler and `SentenceExecutor`, not a separate runtime.
