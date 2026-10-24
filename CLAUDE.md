# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Overview

DataverseGen is a .NET 10 (`net10.0`) Windows CLI that reads Dataverse (Power Platform) metadata and generates early-bound C# or TypeScript code via Scriban templates. Solution: `DataverseGen.sln` with three projects:

- `DataverseGen.Cli` — console entry point (`Program.cs`). Windows-only (uses Windows Credential Manager).
- `DataverseGen.Core` — config, connection-string validation, metadata loading, generators, templates.
- `Tests/DataverseGen.Core.Tests` — MSTest tests for Core (`InternalsVisibleTo` is set, so `internal` members are testable).

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

Pipeline in `Program.Main`:

1. **Config** — `dataversegen.config.json` is read from the current working directory into `ConfigModel` and checked by `ConfigValidation`. If `ConnectionString` is empty, it is read from / prompted into Windows Credential Manager under key `DataverseGen-{Namespace}`. See `DataverseGen.Cli/example-dataversegen.config.json` and README for fields.
2. **Connect** — `DataverseConnector` wraps `ServiceClient`; when `EnableConnectionStringValidation` is on, `ConnectionStringValidatorFactory` picks a validator per `AuthType` (`ConnectionString/Validators/*`).
3. **Load metadata** — `DataverseMetadataConverter` retrieves `EntityMetadata` for each configured entity and maps it into the `Metadata/Mapping*` model (`MappingEntity`, `MappingField`, `MappingEnum`, relationships 1:N / N:1 / M:N). `DataverseCustomApiConverter` queries the `customapi`, `customapirequestparameter`, and `customapiresponseproperty` tables into `CustomApiModel`s, filtered by `CustomApis` (case-insensitive `uniquename`; empty = all).
4. **Generate** — everything is bundled into `Metadata/Context` and passed to `ScribanGenerator` (a `BaseGenerator`). It branches on `TemplateEngine.Type` (`C#`/`ts`) × `IsSingleOutput`:
   - Single output: one embedded template (`Templates/Single/dataversegen-single*.sbncs`) → `XrmServiceContext.cs` or `dataverse.metadata.ts`.
   - Multiple output: per-entity files using `MultipleFileTemplateCsharp` / `MultipleFileTemplateTypescript` (C#: entity, fields, enums + `XrmServiceContext.cs`; TS: attributes, enums, relationships).
   - Afterwards, custom API wrappers are written to `<OutDirectory>/customapi/` by `CustomApiCSharpGenerator` / `CustomApiTypeScriptGenerator`. These are **hand-written `StringBuilder` renderers, not Scriban templates** — change their output in C# code.

### Templates

- Built-in `.sbncs` templates are embedded via `ScribanTemplates.resx` (`ResXFileRef` entries) with the generated `ScribanTemplates.Designer.cs`. Adding a new built-in template requires adding it to the `.resx` and the Designer accessor.
- `TemplateName = "Main"` (case-insensitive) uses the embedded templates; any other name loads files from `<CWD>/<TemplateDirectoryName>/<TemplateName>/` (must contain `entity.sbncs` and `enums.sbncs`, plus the type-specific extras). `build.ps1` copies `Multiple/Main` and `Multiple/TS-Main` into the package as example templates.
- Templates see the C# model through Scriban's `ScriptObject.Import`, which renames members to snake_case (e.g. `entity.logical_name`, `info.welcome_xml`). Renaming a model property changes the template-facing name.
- Rendered output goes through `RemoveEmptyLines` and multi-file output is normalized to CRLF.
- Output paths are built with backslashes (`{CWD}\{OutDirectory}\`), so generation assumes Windows.

## Conventions

- C# code uses tabs and file-scoped namespaces; Core/Cli use explicit types rather than `var` (tests mix both). Core has `Nullable` disabled; Cli and Tests have it enabled.
- Tests are MSTest 4: use `Assert.ThrowsExactly<T>` (`ThrowsException` was removed).
- Commits use Conventional Commit prefixes (`.gitmessage.txt`, PR template): `feat`, `fix`, `refactor`, `perf`, `docs`, `test`, `ci`, `build`, `chore`, `style`, `remove`/`delete`/`drop`, `sec`/`security`. The release workflow buckets commit subjects by these prefixes into Keep-a-Changelog sections, so subjects must be specific. Issue numbers are typically scoped, e.g. `feat(#36): ...`.
- Branching/versioning via GitVersion 6 (`GitVersion.yml`, v6 schema; `build.ps1`/`release.yml` use `SemVer` for ZIP names and tags): tests run on PRs/pushes to `develop`; pushing a `release/x.y.z` branch builds ZIPs, opens a PR to `main`, and publishes a GitHub Release. In Git Bash, pass `MSYS_NO_PATHCONV=1` to `dotnet-gitversion` so `/config`-style switches aren't rewritten into paths.
