# Agent tasks – "Pimp my CLI" (issue #9)

Goal: professional CLI look (Claude-CLI style, Spectre.Console), `--verbose` + `--log`, consistent Entities/CustomApi messages, parallel generation, performance report.
The work is split into three independent tasks. Run them **in order**, one session / one PR each.

| # | File | Summary | Depends on |
|---|---|---|---|
| 1 | [09-1-reporting-verbose-log.md](09-1-reporting-verbose-log.md) | `IGenerationReporter`, `--verbose`, `--log`, secret masking, migrate off `ColorConsole` | – |
| 2 | [09-2-spectre-ui-performance.md](09-2-spectre-ui-performance.md) | Spectre UI (banner, progress, tables, error panel), performance report | 1 |
| 3 | [09-3-async-parallel.md](09-3-async-parallel.md) | Async + parallel metadata retrieval and file generation | 1 (best after 2) |

## Common rules (apply to every task)
- **Never commit.** The user reviews and commits themselves. Suggest a Conventional Commit message at the end (`feat(#9): ...`, specific subject – the release workflow buckets by prefix). End it with the `Co-Authored-By` line from the session's attribution reminder.
- Read `CLAUDE.md` first. Key points: tabs, file-scoped namespaces, explicit types instead of `var` in Core/Cli, Core has `Nullable` disabled while Cli/Tests have it enabled, MSTest 4 (`Assert.ThrowsExactly<T>`).
- Core must **not** reference Spectre. Console output in Core goes only through `IGenerationReporter` (introduced in task 1).
- Do not enable assembly trimming (breaks Newtonsoft config deserialization).
- `ScribanGeneratorTests` baselines (`Tests/DataverseGen.Core.Tests/Generators/Baselines/`) must **not change**. Output of generation must stay byte-identical.
- Keep public/sync APIs used by tests compiling (add optional parameters / async overloads instead of breaking signatures).
- No `python` in Bash; use the Write/Edit tools. If `dotnet restore` fails with 401 on the `d365-crm` feed, pass `--source https://api.nuget.org/v3/index.json`.
- Live Dataverse is not available in CI/sandbox: say explicitly what was **not** verified.

## Definition of done
1. `dotnet build DataverseGen.sln` – 0 errors, no new warnings from the touched code.
2. `dotnet test DataverseGen.sln` – all green, baselines untouched.
3. Manual smoke run of the CLI as described in the task file.
4. `README.md` / `CLAUDE.md` updated where the task says so.
5. Final message lists: what changed, what was verified, what was not, suggested commit message.
