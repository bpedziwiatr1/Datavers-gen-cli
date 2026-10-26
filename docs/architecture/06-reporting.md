# 06 Reporting

- Status: Done
- Strength: Speculative

## Files

- `DataverseGen.Core/Reporting/IGenerationReporter.cs` (~24), `PerformanceCollector.cs` (~229), `CompositeReporter.cs` (~59), `NullReporter.cs` (~18), `FileLogReporter.cs` (~74)
- `DataverseGen.Cli/Ui/SpectreReporter.cs` (~319)
- `DataverseGen.Core/.../SecretMasker`

## Problem (unverified)

- Interface has six members, two default-implemented (`ItemStarted`, `RequestCompleted`); `FileLogReporter` silently drops both (no "started" lines or request timings in the log).
- `PerformanceCollector` is both a forwarding decorator and the aggregation store; mutable `DegreeOfParallelism` set from `GenerateCommand.cs:~182`.
- Secret masking is per sink; `DataverseConnector` masks manually (~79). A new sink can leak.
- `SpectreReporter.GetStage(kind, phase)` re-derives what `Stage`, `ItemKind`, `ItemPhase` already encode.
- `ItemKind.Output` is labelled "Entity" (`SpectreReporter.cs:~188`); the activity-party item exceeds the stage total.
- A contract change touches six files.

## Solution

Narrow the reporter interface and centralise masking and aggregation, keeping sinks thin.

## Wins

- locality: masking in one place
- leverage: new sinks implement less

## Deletion test

Deleting `CompositeReporter` or `NullReporter` would spread null checks, so they are not clearly shallow. Weakest case of the seven.

## Open questions

- Is this friction real enough to justify the churn?
- Should masking move to the reporter boundary?

## Decision log

- Motive is the risk of leaking a secret (masking in one place), not interface narrowing.
- Masking moved to the reporter boundary: `Core/Reporting/MaskingReporter` wraps the sink chain and masks message, exception, item name and detail. `FileLogReporter`, `SpectreReporter` and `PerformanceCollector` no longer call `SecretMasker`; `SummaryRenderer`, `ErrorPanel` and `DataverseConnector` still do (they sit outside the reporter path).
- `PerformanceCollector` is a plain sink (no inner reporter, no `DegreeOfParallelism` setter); `Build(degreeOfParallelism)` takes the value, and `GenerationPipeline.RunAsync` returns the resolved parallelism.
- `FileLogReporter` now writes `ItemStarted` and `RequestCompleted`.
- `SpectreReporter` labels `ItemKind.Output` as "File" and grows the bar total when more items complete than announced (the extra `activityparty`).
- Not done: narrowing the interface (default members stay).
