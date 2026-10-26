# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Overview

DataverseGen is a .NET 10 (`net10.0`) Windows CLI that reads Dataverse (Power Platform) metadata and generates early-bound C# or TypeScript code via Scriban templates. Solution: `DataverseGen.sln` with these projects:

- `DataverseGen.Cli` — console entry point (`Program.cs`). Windows-only (uses Windows Credential Manager).
- `DataverseGen.Core` — config, connection-string validation, metadata loading, generators, templates.
- `Tests/DataverseGen.Core.Tests` — MSTest tests for Core (`InternalsVisibleTo` is set, so `internal` members are testable).
- `Tests/DataverseGen.Cli.Tests` — MSTest tests for the CLI UI on `Spectre.Console.Testing.TestConsole` (`InternalsVisibleTo` via the csproj).

## Critical Guardrails
- **Incremental Updates**: Modify only files directly related to the task. Do not touch or rewrite unrelated modules.
- **Never Assume**: If project context, specifications, or edge cases are unclear, STOP and ask the user. Do not hallucinate placeholder logic.
- **Test-Driven Delivery**: Every functional change or new feature must include a corresponding unit test.
- **Style Enforcement**: Match the existing repository formatting exactly. No conversational comments in code.

## Code Patterns (Bad vs. Good)
- **Bad**: Writing large, deeply nested 200-line functions.
- **Good**: Splitting logic into pure, single-responsibility functions (max 25 lines).

## Commands

```powershell
dotnet build DataverseGen.sln
dotnet test DataverseGen.sln                       # CI runs: dotnet test DataverseGen.sln --configuration Release
dotnet test DataverseGen.sln --filter "FullyQualifiedName~CustomApiTypeScriptGeneratorTests"   # one class
dotnet test DataverseGen.sln --filter "Name=WriteCustomApis_CreatesTypeScriptRequestClass"     # one test
dotnet run --project DataverseGen.Cli              # needs dataversegen.config.json in the CWD and a live Dataverse env
pwsh -NoProfile -File build.ps1                    # release ZIPs + dotnet tool .nupkg into publish/ (requires dotnet-gitversion on PATH)
dotnet pack DataverseGen.Cli/DataverseGen.Cli.csproj -c Release -p:PlatformTarget=AnyCPU -o publish   # tool package only (command: dataversegen)
```

The CLI ships both as a single-file exe (`dotnet publish` in `build.ps1`) and as a dotnet tool (`PackAsTool` in `DataverseGen.Cli.csproj`, PackageId `DataverseGen.Tool`). It only resizes the window and waits for `Console.ReadKey()` when it owns its console window (`GetConsoleProcessList` == 1, i.e. a double-clicked exe); from an existing terminal, or with `--no-pause`, it exits right away. To smoke-test the exe behavior, launch it in a separate window (`Start-Process ... -WindowStyle Minimized`) and check it is still alive (waiting for a key). Do not enable assembly trimming on publish — it breaks Newtonsoft JSON deserialization of the config.

`ScribanGeneratorTests` compares the output of all built-in templates (C#/ts × single/multiple) with files in `Tests/DataverseGen.Core.Tests/Generators/Baselines/`. After an intended template/model change, regenerate them with `DATAVERSEGEN_UPDATE_BASELINES=1` set for `dotnet test` and review the diff. Baselines are excluded from compilation and stored byte-for-byte (`.gitattributes`, mixed LF/CRLF output).

## Architecture

Entry point `Program.Main` hosts a Spectre.Console.Cli `CommandApp` (`DataverseGen.Cli/Commands`: `generate` is the default, `init` writes a starter config, `templates` copies the built-in templates; scaffolding logic lives in `Core/Scaffolding`, with `--init`/`-i` and `--templates`/`-t` mapped to the commands in `NormalizeArguments`). The `generate` pipeline:

1. **Config** — `dataversegen.config.json` is read from the current working directory into `ConfigModel` and checked by `ConfigValidation`. If `ConnectionString` is empty, it is read from / prompted into Windows Credential Manager under key `DataverseGen-{Namespace}`. See `DataverseGen.Cli/example-dataversegen.config.json` and README for fields.
2. **Connect** — `DataverseConnector` wraps `ServiceClient`; when `EnableConnectionStringValidation` is on, `ConnectionStringValidatorFactory` picks a validator per `AuthType` (`ConnectionString/Validators/*`).
3. **Load metadata** — `DataverseMetadataConverter` retrieves `EntityMetadata` for each configured entity and maps it into the `Metadata/Mapping*` model (`MappingEntity`, `MappingField`, `MappingEnum`, relationships 1:N / N:1 / M:N). `DataverseCustomApiConverter` queries the `customapi`, `customapirequestparameter`, and `customapiresponseproperty` tables into `CustomApiModel`s, filtered by `CustomApis` (case-insensitive `uniquename`; empty = none, only a warning).
4. **Generate** — everything is bundled into `Metadata/Context` and passed to `ScribanGenerator`, a `BaseGenerator`. `BaseGenerator` is the engine-neutral orchestrator (stage reporting, output directories, concurrent custom APIs, `XrmServiceContext.cs` last, `ClearStaleFiles` via `GeneratedFileSink`); it asks the subclass for an `IEntityGenerator` (`EntitiesScribanGenerator`) and an `ICustomApiGenerator` (`CustomApiScribanGenerator`) and hands them a `GenerationSession`. Generators render only views built in `Generators/Views` (`EntityViewBuilder` → `EntityTemplateView` with `Namespace`/`Info`/`Entity`, the custom API view builders). `EntitiesScribanGenerator` branches on `TemplateEngine.Type` (`C#`/`ts`) × `IsSingleOutput`:
   - Single output: one embedded template (`Templates/Single/dataversegen-single*.sbncs`) → `XrmServiceContext.cs` or `dataverse.metadata.ts`.
   - Multiple output: per-entity files using `MultipleFileTemplateCsharp` / `MultipleFileTemplateTypescript` (C#: entity, fields, enums + `XrmServiceContext.cs`; TS: attributes, enums, relationships).
   - Afterwards, custom API wrappers are written to `CustomApiOutDirectory` (default `<entities dir>/customapi/`; entities go to `EntitiesOutDirectory`, falling back to `OutDirectory`) as `<classname>.customapi.cs|ts` by `CustomApiScribanGenerator` (one write loop for both languages). Each file is rendered from the `customapi.sbncs` template (`Multiple/Main` for C#, `Multiple/TS-Main` for ts) over a view model (`CustomApiCSharpView` / `CustomApiTypeScriptView`) built by `Generators/Views/CustomApiCSharpViewBuilder` / `CustomApiTypeScriptViewBuilder`, which keep naming/escaping logic in C#. A non-`Main` template set may ship its own optional `customapi.sbncs`; otherwise the built-in one is used (`CustomApiTemplates.Load`). APIs with `ResponseProperties` also get a `<responseclassname>.customapi.cs|ts` file (C# `OrganizationResponse` subclass / TS `interface`) rendered from the optional `customapi.response.sbncs` (`CustomApiTemplates.LoadResponse`) over `CustomApiCSharpResponseView` / `CustomApiTypeScriptResponseView`; parameter and response property types are resolved by `CustomApiTypeMapper` (one exact-match table; only `Entity` differs between request `EntityReference` and response `Entity`). Baselines: `Generators/Baselines/customapi-response-*`. Output is not passed through `RemoveEmptyLines`, so layout is controlled by the template (Scriban does not trim lines that only hold a control tag — start loops inline at the end of the previous line). Baselines: `Generators/Baselines/customapi-*`.

### Reporting

Core never writes to the console. All output goes through `IGenerationReporter` (`Core/Reporting`: `Log`, `StageStarted`, `ItemStarted`, `ItemCompleted`, `StageCompleted`); services take it as an optional last constructor/method parameter defaulting to `NullReporter.Instance`. The CLI composes `Ui/SpectreReporter` (live progress bars; plain lines when output is redirected / non-interactive; Debug and stack traces only with `-v|--verbose`) with `FileLogReporter` (`--log [PATH]`, always full detail), wrapped in `PerformanceCollector`, which aggregates durations, request counts and written files into a `PerformanceReport` (rendered by `Ui/SummaryRenderer`, appended as text to the log). Each item is reported once per `ItemPhase` (`Retrieve` by the converters, `Generate` by the generators with `Files`/`Bytes`); `ItemKind.Output` is for files that belong to no single item (single-file output). Call `ItemStarted` before work on an item begins (the live bar shows the names in flight; several when run in parallel) and report Dataverse round trips with `RequestCompleted`. Credential prompts and connection-string validation must run before `SpectreReporter.Run` (live display and prompts cannot overlap). Both sinks mask secrets via `SecretMasker`; never put a raw connection string into a message or exception. Do not call `SetApplicationVersion` on the `CommandApp` (it would register `-v|--version` and clash with `--verbose`).

### Parallelism

`generate -p|--parallel <N>` (default: `ServiceClient.RecommendedDegreesOfParallelism` clamped to 1..8, resolved by `DataverseConnector.ResolveDegreeOfParallelism` after connecting; 1 = sequential). Reporter calls come from worker threads, so every `IGenerationReporter` must be thread-safe. `GenerateCommand` is an `AsyncCommand`: it connects once (`EnsureConnected`), then runs `GetMappedEntitiesAsync` and `GetCustomApisAsync` concurrently (first failure cancels the other) and `ScribanGenerator.GenerateTemplateAsync`. Converters take the degree as an optional last constructor parameter and rent cloned clients from `ServiceClientPool` (DOP 1 uses the shared client). Entity results keep selection order (plus `activityparty` inserted after the first activity entity), so output is deterministic. The generator writes entity files and custom APIs concurrently and `XrmServiceContext.cs` last. Sync wrappers (`GetMappedEntities`, `GetCustomApis`, `GenerateTemplate`) remain for tests. Ctrl+C is wired in `GenerateCommand` (`Console.CancelKeyPress` → token → exit code 130). `ScribanGeneratorTests` also checks DOP 4 against the baselines; `ParallelGenerationTests` covers many entities, one completion per item and cancellation.

### Templates

- Built-in `.sbncs` templates are embedded via `ScribanTemplates.resx` (`ResXFileRef` entries) with the generated `ScribanTemplates.Designer.cs`. Adding a new built-in template requires adding it to the `.resx` and the Designer accessor.
- `TemplateName = "Main"` (case-insensitive) uses the embedded templates; any other name loads files from `<CWD>/<TemplateDirectoryName>/<TemplateName>/` (must contain `entity.sbncs` and `enums.sbncs`, plus the type-specific extras). `build.ps1` copies `Multiple/Main` and `Multiple/TS-Main` into the package as example templates.
- Templates see the C# model through Scriban's `ScriptObject.Import`, which renames members to snake_case (e.g. `entity.logical_name`, `info.welcome_xml`). Renaming a model property changes the template-facing name.
- Rendered output goes through `RemoveEmptyLines` and multi-file output is normalized to CRLF.
- Output and template paths are resolved by `Generators/GeneratorPaths` (relative to the CWD or the optional `baseDirectory` of `ScribanGenerator`; an absolute `OutDirectory` is honoured). All templates are parsed through `TemplateParser`, which throws on syntax errors.

## Conventions

- C# code uses tabs and file-scoped namespaces; Core/Cli use explicit types rather than `var` (tests mix both). Core has `Nullable` disabled; Cli and Tests have it enabled.
- Tests are MSTest 4: use `Assert.ThrowsExactly<T>` (`ThrowsException` was removed).
- Commits use Conventional Commit prefixes (`.gitmessage.txt`, PR template): `feat`, `fix`, `refactor`, `perf`, `docs`, `test`, `ci`, `build`, `chore`, `style`, `remove`/`delete`/`drop`, `sec`/`security`. The release workflow buckets commit subjects by these prefixes into Keep-a-Changelog sections, so subjects must be specific. Issue numbers are typically scoped, e.g. `feat(#36): ...`.
- Branching/versioning via GitVersion 6 (`GitVersion.yml`, v6 schema; `build.ps1`/`release.yml` use `SemVer` for ZIP names and tags): tests run on PRs/pushes to `develop`; pushing a `release/x.y.z` branch builds ZIPs, opens a PR to `main`, and publishes a GitHub Release. In Git Bash, pass `MSYS_NO_PATHCONV=1` to `dotnet-gitversion` so `/config`-style switches aren't rewritten into paths.

## Wiki

User documentation lives in the GitHub wiki, a separate repo checked out at `C:\repos\github\Datavers-gen-cli.wiki` (source of truth; README is only an overview). After a user-facing change, update the matching page in the same task and tell the user which wiki files changed. Do not commit or push either repo; the wiki is committed separately with a `docs:` prefix.

| Change | Wiki page |
|---|---|
| Config field (`ConfigModel`) | `Configuration.md` |
| CLI command/option (`DataverseGen.Cli/Commands`) | `CLI-Reference.md` |
| Template set / model property rename | `Templates.md` |
| Custom API generation or template variables | `Custom-APIs.md` |
| Console UI, log, parallelism, performance report | `Output-and-Reporting.md` |
| Install, packaging, credentials | `Getting-Started.md` |
| Build, test, architecture, release process | `Development.md` |

New pages must be linked from `Home.md` and `_Sidebar.md`. Process is described in `Wiki-Maintenance.md`. To catch up after several changes, run the `/wiki-sync` skill (`.claude/skills/wiki-sync`).
