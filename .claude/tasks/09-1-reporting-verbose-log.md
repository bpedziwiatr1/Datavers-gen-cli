# Task 09-1 – Reporting abstraction, `--verbose`, `--log`

Common rules: see [README.md](README.md). Branch: `feature/9-upgrade-cli-visuals`.

## Goal
Replace the static `ColorConsole` / scattered `Console.WriteLine` calls in Core with an injectable `IGenerationReporter`, so the CLI controls presentation, verbosity and a log file. Add `-v|--verbose` and `--log [PATH]`. Make Entity and CustomApi messages uniform. Stop leaking secrets.

## Read first
- `DataverseGen.Core/ColorConsole.cs`
- `DataverseGen.Core/DataConverter/{DataverseConnector,DataverseMetadataConverter,DataverseCustomApiConverter}.cs`
- `DataverseGen.Core/Generators/Scriban/ScribanGenerator.cs`, `Generators/CustomApi{CSharp,TypeScript}Generator.cs`
- `DataverseGen.Core/Generators/Scriban/Templates/TemplateFileManager/{CustomApiTemplates,MultipleFileTemplateBase}.cs`
- `DataverseGen.Core/ConnectionString/ConnectionStringValidator.cs`
- `DataverseGen.Cli/Commands/GenerateCommand.cs`, `DataverseGen.Cli/Program.cs`

## Scope
1. **Core `Reporting/`** (no Spectre dependency):
   - `LogLevel` (Debug, Info, Success, Warning, Error), `Stage` (Connect, Entities, CustomApis, Generate).
   - `ItemResult`: `Kind` (Entity|CustomApi), `Name`, `Status` (Ok/Warning/NotFound/Failed), `Detail`, `Elapsed`, `Files`, `Bytes`.
   - `IGenerationReporter`: `Log(LogLevel, string)`, `StageStarted(Stage, int total)`, `ItemCompleted(ItemResult)`, `StageCompleted(Stage, TimeSpan)`.
   - `NullReporter` (default), `CompositeReporter`, `FileLogReporter` (timestamp + level, thread-safe, always logs Debug and full exception stack traces regardless of console verbosity).
   - `SecretMasker`: masks `ClientSecret`, `Password`, `Secret`, `Key`, `Thumbprint` values in connection strings.
2. **Bug fix**: `DataverseConnector.CheckIfThereWasErrorOnConnecting` puts the full connection string (with secrets) into the message/exception. Mask it everywhere (console, exception message, log).
3. **Migrate** all `ColorConsole`/`Console.WriteLine` usages in Core to the reporter. Inject through constructors or optional parameters defaulting to `NullReporter` so existing tests compile unchanged. Use one message format for both item kinds (e.g. `✔ account · 143 fields`, `⚠ Custom API not found: x`).
4. **Cli**: simple `ConsoleReporter` that reproduces today's look (fancy UI is task 09-2). Remove `ColorConsole` once unused (check Cli, Core, Tests).
5. **Options** on `generate`: `-v|--verbose` (Debug lines on console), `--log [PATH]` (`FlagValue<string>`; default `dataversegen.log` in CWD; overwritten per run; always full detail). Errors: short message by default, full stack trace with `-v` and always in the log. Do **not** call `SetApplicationVersion` (Spectre would register `-v|--version`, clashing with `--verbose`).

## Out of scope
Spectre progress/tables/banner (09-2), async/parallelism (09-3), performance report (09-2).

## Tests (Core.Tests, MSTest 4)
- `SecretMasker` (each key, case-insensitive, no secret remains).
- `FileLogReporter` (levels, concurrent writes from many threads, stack trace present, no secrets).
- `CompositeReporter` fan-out.
- `DataverseCustomApiConverter.FilterCustomApiEntities` reports a Warning for a missing API through the reporter.

## Acceptance criteria
- No `ColorConsole`/`Console.Write*` left in `DataverseGen.Core` (except where the reporter is the console sink, which lives in Cli).
- `dataversegen --log` produces a file with Debug entries and no secrets; `-v` shows Debug on console.
- Build + tests green, baselines unchanged.

## Verification
- `dotnet build` / `dotnet test`.
- Run in an empty temp dir: `dataversegen` → readable "Config not found" error; with `-v --log` the log contains the stack trace.
- Not verifiable here: real Dataverse run (say so).
