# 03 Generation output module

- Status: Partial
- Strength: Worth exploring

## Files

- `DataverseGen.Core/Generators/Scriban/ScribanGenerator.cs` (~487 LOC)
- `DataverseGen.Core/Generators/BaseGenerator.cs` (~28 LOC)
- `DataverseGen.Core/Generators/Scriban/MultipleFileTemplate{Base,Csharp,Typescript}.cs` (~80, 52, 44)
- `DataverseGen.Core/Generators/CustomApiTemplates.cs` (~118)

## Problem (unverified)

- One class does orchestration, template loading, file writing, stale-file clearing (`ClearStaleFiles` ~437-483) and the C#/TS fork.
- `SingleFileCSharp`/`SingleFileTypescript` (~236-296) and `MultiOutputGeneratorCSharp`/`TypeScript` (~159-210) are near twins.
- Paths: `CWD + "\\" + OutPath + "\\"` (~70, ~418), the same in `MultipleFileTemplateBase.cs:42` and `CustomApiTemplates.cs:77`; absolute paths and non-Windows break; `ClearStaleFiles` compares string prefixes.
- `BaseGenerator` has one subclass: shallow.
- Trailing newline decided by `outputFileSuffixWithExtension.Contains("ts")` (~366).
- Entity templates skip `HasErrors`, `CustomApiTemplates.Parse` checks it.
- Locating where a file ends up needs five files.

## Solution

Separate path/file writing from template rendering; one place decides output locations.

## Wins

- locality: path rules in one module
- depth: C#/TS twins collapse
- tests need no CWD changes

## Deletion test

Deleting `BaseGenerator` loses nothing, so it is a pass-through. The writing and path logic would concentrate.

## Open questions

- Is Windows-only paths a deliberate constraint (CLAUDE.md says generation assumes Windows)?
- Which of the five jobs becomes its own module?
- Do template-set loading rules change?

## Decision log

- 2026-10-25 (partial): `ScribanGenerator` split into `BaseGenerator` orchestrator + `EntitiesScribanGenerator` + `CustomApiScribanGenerator`; file tracking and `ClearStaleFiles` in `GeneratedFileSink`; entity templates render `EntityTemplateView`. Still open: path handling (CWD + backslashes), single-file C#/TS twins, `HasErrors` check for entity templates.
