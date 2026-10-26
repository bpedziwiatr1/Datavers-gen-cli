# 03 Generation output module

- Status: Done
- Strength: Worth exploring

## Files

- `DataverseGen.Core/Generators/BaseGenerator.cs` (orchestrator)
- `DataverseGen.Core/Generators/GeneratorPaths.cs` (output and template locations)
- `DataverseGen.Core/Generators/GeneratedFileSink.cs` (writes, stale-file clearing)
- `DataverseGen.Core/Generators/Scriban/EntitiesScribanGenerator.cs`, `CustomApiScribanGenerator.cs`
- `DataverseGen.Core/Generators/Scriban/Templates/TemplateFileManager/` (`TemplateParser`, `MultipleFileTemplate*`, `CustomApiTemplates`)

## Resolved

- Paths: `GeneratorPaths` is the only place that joins the base directory (default CWD, injectable via the optional `baseDirectory` of `ScribanGenerator`) with `OutDirectory`, the custom API directory and template directories. `Path.Combine`/`GetFullPath`, so absolute paths work.
- Entity C#/TS twins: one `WriteEntityFiles` loop over `EntityOutput(Template, FileSuffix, TrailingNewline)`; the trailing newline is an explicit flag instead of `Contains("ts")`.
- `TemplateParser` validates `HasErrors` for every template (entity, enums, fields, context, relationships, single file, custom API).

## Decision log

- 2026-10-25 (partial): `ScribanGenerator` split into `BaseGenerator` orchestrator + `EntitiesScribanGenerator` + `CustomApiScribanGenerator`; file tracking and `ClearStaleFiles` in `GeneratedFileSink`; entity templates render `EntityTemplateView`.
- 2026-10-26 (done): `GeneratorPaths` (absolute paths, injectable base directory), shared `TemplateParser` with `HasErrors`, entity output loop and explicit trailing newline. Baselines unchanged.
