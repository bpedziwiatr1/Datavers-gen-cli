# 01 Custom API writer module

- Status: Done
- Strength: Strong

## Files

Original state (before the 2026-10-25 refactor; see the decision log for the current layout):

- `DataverseGen.Core/Generators/CustomApiCSharpGenerator.cs` (~306 LOC)
- `DataverseGen.Core/Generators/CustomApiTypeScriptGenerator.cs` (~191 LOC)
- `DataverseGen.Core/CustomApi/CustomApiCSharpView.cs`, `CustomApiTypeScriptView.cs`
- Caller: `DataverseGen.Core/Generators/Scriban/ScribanGenerator.cs` (`GenerateCustomApis`, ~387-411)

Current: `Generators/Scriban/CustomApiScribanGenerator.cs`, `Generators/Views/CustomApi{CSharp,TypeScript}ViewBuilder.cs`, `Generators/Views/CustomApiText.cs`, `Generators/GeneratedFileSink.cs`.

## Problem (historical; resolved except where noted in the decision log)

- The two generators duplicate ~80 lines of orchestration: `Parallel.ForEach`, reporter calls, `ItemResult`, files/bytes bookkeeping.
- Bound-parameter synthesis (`GetBoundParameter`) and `EscapeString` are duplicated.
- `onFileWritten?.Invoke(...)` is called twice in a row in both (C# ~59-60 and ~68-69, TS ~54-55 and ~63-64). Harmless while `TrackWrittenFile` is idempotent; shows the copies drift.
- `WriteCustomApis`, `RenderRequestClass`, `RenderResponseClass` overloads exist only for tests.

## Solution

One module owns the custom API write loop; language-specific parts (view building, extension, template) sit behind it.

## Wins

- locality: reporting, parallelism and file bookkeeping live in one place
- leverage: one interface, two languages
- tests hit the writer once instead of twice

## Deletion test

Deleting either generator duplicates its orchestration into the other, so the shared part earns a module.

## Open questions

- Strategy object per language, or generic parameters?
- Do the test-only overloads survive?
- Does the view-model split (C# keeps naming/escaping) stay as is?

## Decision log

- 2026-10-25: write loop moved into `CustomApiScribanGenerator` (one loop for C# and TS); `CustomApiCSharpGenerator`/`CustomApiTypeScriptGenerator` became `CustomApi*ViewBuilder` in `Generators/Views`. Test-only overloads dropped; tests use `CustomApiTestHost`. Double `onFileWritten` gone (`GeneratedFileSink`). Failure path still unchanged (no `ItemStatus.Failed`).
- Open (checked against bbf265a): `GetBoundParameter` is still duplicated in `CustomApiCSharpViewBuilder` and `CustomApiTypeScriptViewBuilder` (`EscapeString` is shared via `CustomApiText`). Test classes still carry the old `CustomApi*GeneratorTests` names.
