# Task 09-2 – Spectre UI and performance report

Common rules: see [README.md](README.md). **Requires task 09-1 to be merged/present** (`IGenerationReporter`, `ItemResult`).

## Goal
Professional, Claude-CLI-like presentation built on Spectre.Console, with a performance report the user can rely on.

## Read first
- `DataverseGen.Core/Reporting/*` (from 09-1), `DataverseGen.Cli/Commands/GenerateCommand.cs`, `DataverseGen.Cli/Program.cs`
- Existing `Stopwatch` usages: `DataverseMetadataConverter`, `DataverseCustomApiConverter`, `ScribanGenerator`

## Scope
1. **`DataverseGen.Cli/Ui/`**
   - `Theme`: palette, icons (✔ ⚠ ✖ ›) with ASCII fallback when the console is not Unicode-capable.
   - `Banner`: compact header (name, version, runtime, CWD, config path, mode: verbose/log/parallel). Replaces the ASCII-art `Title` and `WriteVersion` in `Program.cs`. Keep: own-console-window detection, window resize, pause-on-key, `--no-pause`, `NormalizeArguments`.
   - `SpectreReporter : IGenerationReporter`: `Status` spinner for Connect; `Progress` with one bar per stage (Entities, Custom APIs, Generate) – **same layout for entities and custom APIs**; `-v` adds Debug lines. Degrade to plain line output when output is redirected / non-interactive / CI (`AnsiConsole.Profile.Capabilities`).
   - `SummaryRenderer`: entities table and custom API table with identical columns (Name, counts, retrieve ms, generate ms, files); warnings/failures listed at the end; footer with log path.
   - Error panel: short message + hint (e.g. "Config not found – run `dataversegen init`"); stack trace only with `-v` (always in the log).
2. **Core performance** (`Reporting/PerformanceCollector`, `PerformanceReport`): stage durations, per-item durations, Dataverse request count, degree of parallelism, files/bytes written, throughput (entities/s, files/s), top-3 slowest items, sum of item times vs wall-clock (speed-up from parallelism), peak working set. Collector implements/wraps the reporter so no extra plumbing in converters.
3. **Performance panel** in the summary (`BarChart` of stage durations + key figures) and the same data appended to the `--log` file.
4. Credential prompt (`GetCredentials`) and connection-string validation must run **before** Spectre live rendering starts (live displays and prompts cannot overlap).

## Out of scope
Async/parallel execution itself (09-3) – the report must already work sequentially (DOP = 1, speed-up = 1.0×).

## Tests
- Core.Tests: `PerformanceCollector` aggregates, top-N, speed-up factor, thread-safety.
- New `Tests/DataverseGen.Cli.Tests` (net10.0, MSTest 4, Nullable enabled; add to `DataverseGen.sln`; add `InternalsVisibleTo` to Cli): `SummaryRenderer` and `Banner` on `Spectre.Console.Testing.TestConsole`; `NormalizeArguments` (`-i`, `--init`, `-t`, `--templates`, `--no-pause` stripped).

## Acceptance criteria
- Visual check in a real terminal (Windows Terminal) and with output redirected to a file (no ANSI garbage, no live-rendering exceptions).
- Exit codes unchanged (0 ok, 1 error).
- Build + tests green, baselines unchanged; README documents the output and the performance panel.

## Verification
Cannot run against live Dataverse here; to eyeball the UI, use a throwaway local harness (or the TestConsole output) feeding fake `ItemResult`s through `SpectreReporter` + `SummaryRenderer` – do not commit the harness. State clearly that the real run was not verified.
