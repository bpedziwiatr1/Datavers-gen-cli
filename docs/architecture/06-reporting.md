# 06 Reporting

- Status: Open
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

(empty)
