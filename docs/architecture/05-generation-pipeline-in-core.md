# 05 Generation pipeline in Core

- Status: Done
- Strength: Worth exploring

## Files

- `DataverseGen.Cli/Commands/GenerateCommand.cs` (~319 LOC, `Run` ~53-319)
- `DataverseGen.Cli/Ui/SpectreReporter.cs`
- Tests: `Tests/DataverseGen.Cli.Tests` (`ProgramTests.cs` ~41 LOC; nothing covers `GenerateCommand`)

## Problem (unverified)

- The retrieve-then-generate pipeline lives in Cli, which references converters, `Context`, `MappingEntity`, `CustomApiModel`, `ScribanGenerator`, `ConfigModel`.
- Mixed concerns: config load, Credential Manager prompt (`GetCredentials` ~267-297, loops while `newCreds is null`), Ctrl+C, DOP resolution, concurrency (~198-212), error rendering.
- `switch (config.TemplateEngine.Name)` has a `default` that logs "Unsupported generator" then runs Scriban anyway (~221-233); `ScribanRun` duplicated.
- Config is mutated in place (`ClearOutputDirectory`, `ConnectionString`).
- `Run` takes 8 parameters.

## Solution

Move the pipeline into Core as one module; Cli keeps prompts, console and cancellation wiring.

## Wins

- locality: pipeline order and concurrency in one module
- pipeline testable without Spectre
- a second host reuses it

## Deletion test

Deleting `GenerateCommand`'s pipeline body would move it, not remove it; the benefit is the seam to Cli.

## Open questions

- Does it depend on candidate 04 (seam for the metadata source)?
- Where do credentials resolution and validation sit given that they must run before `SpectreReporter.Run`?
- Do other engines than Scriban exist or are planned (the dead `default`)?

## Decision log

- Motive is locality (order and concurrency in one module), not a second host.
- Done independently of 04: the pipeline keeps the concrete `DataverseConnector` and converters; no `IMetadataSource`.
- Unknown `TemplateEngine.Name` keeps the soft behaviour (warning, run Scriban), now in `GenerationPipeline.CreateGenerator`.
- Credentials (`GetConfig`, `GetCredentials`) stay in Cli, before `SpectreReporter.RunAsync`.
- `Core/Pipeline/GenerationPipeline.RunAsync` + `GenerationOptions` replace `Generate`/`ScribanRun`; `--no-clear` no longer mutates the config.
