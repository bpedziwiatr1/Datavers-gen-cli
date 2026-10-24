# Task 09-3 – Async and parallel retrieval / generation

Common rules: see [README.md](README.md). **Requires 09-1** (reporter); best done after 09-2 so progress bars show the parallelism.

## Goal
Cut wall-clock time by retrieving Dataverse metadata and writing generated files in parallel, without changing the generated output.

## Read first
- `DataverseGen.Core/DataConverter/{DataverseConnector,DataverseMetadataConverter,DataverseCustomApiConverter}.cs`
- `DataverseGen.Core/Generators/Scriban/ScribanGenerator.cs`, `Generators/CustomApi*Generator.cs`
- `DataverseGen.Cli/Commands/GenerateCommand.cs`
- `Tests/DataverseGen.Core.Tests/Generators/ScribanGeneratorTests.cs` and `Baselines/`

## Scope
1. `DataverseMetadataConverter.GetMappedEntitiesAsync(CancellationToken)`:
   - `Parallel.ForEachAsync` over unique entities; each worker uses its own `ServiceClient.Clone()` (dispose them).
   - Preserve behaviour: duplicate warnings emitted before dispatch; `ThrowOnEntityNotFound` (first failure cancels the rest); extra `activityparty` retrieval after the parallel phase when an Activity/ActivityParty entity was retrieved and `activityparty` isn't present; result ordering identical to today (`OrderBy DisplayName`) so output is deterministic.
   - Keep sync `GetMappedEntities()` as a wrapper.
2. `DataverseCustomApiConverter.GetCustomApisAsync(CancellationToken)`: the three `RetrieveMultiple` queries (`customapi`, `customapirequestparameter`, `customapiresponseproperty`) via `Task.WhenAll` on cloned clients. Keep sync wrapper.
3. `ScribanGenerator`: per-entity file rendering/writing in parallel (parsed `Template` objects are shared read-only; every render gets its own `TemplateContext`); custom API generation runs concurrently with entities; `XrmServiceContext.cs` is written last. Keep sync `GenerateTemplate()`.
4. `GenerateCommand` → `AsyncCommand<Settings>`: run Entities and Custom APIs retrieval concurrently; honour `CancellationToken` (Ctrl+C → clean stop, exit code 130, log flushed).
5. New option `-p|--parallel <N>`: default `ServiceClient.RecommendedDegreesOfParallelism` clamped to 1..8; `1` = fully sequential (diagnostics / safe fallback). Report the DOP in the banner and performance panel.
6. Reporter calls from workers must be thread-safe (document the contract on `IGenerationReporter`).

## Out of scope
Changing templates or the model; paging of `RetrieveMultiple`; retry/backoff beyond what the SDK already does.

## Tests
- Generate every baseline scenario with DOP = 1 and DOP = N → files byte-identical to existing baselines (no baseline update!).
- Cancellation stops generation and leaves no half-written file list claiming success.
- `ThrowOnEntityNotFound = true` still throws in parallel mode; `false` skips and warns.
- Reporter receives exactly one `ItemCompleted` per item under concurrency.

## Acceptance criteria
- Build + tests green, baselines untouched.
- `--parallel 1` and default produce identical output directories.
- README documents `--parallel` and the Ctrl+C behaviour.

## Not verifiable here (state it in the final message)
`ServiceClient.Clone()` behaviour under Dataverse service-protection throttling (HTTP 429) and the real-world speed-up. Ask the user to compare `--parallel 1` vs default on a real environment (performance panel shows the speed-up) and to `git diff` the output directory.
