---
name: wiki-sync
description: Compare recent code changes with the GitHub wiki (C:\repos\github\Datavers-gen-cli.wiki) and update the pages that are out of date. Use after user-facing changes (config fields, CLI options, templates, Custom APIs, reporting, install) or when the user asks to sync the wiki.
argument-hint: "[git range, default: changes since the last wiki update]"
---

# wiki-sync

Bring the wiki in line with the code. The wiki repo is `C:\repos\github\Datavers-gen-cli.wiki`; the mapping of code areas to pages is in the "Wiki" section of `CLAUDE.md` and in the wiki page `Wiki-Maintenance.md`.

Never commit or push either repo. The user reviews and commits (wiki commits use the `docs:` prefix).

## Steps

1. **Pick the range.** Use `$ARGUMENTS` if given (e.g. `main..HEAD`, `HEAD~5`). Otherwise use the branch diff against `main` plus uncommitted changes: `git diff main...HEAD --stat` and `git status --short`.
2. **Find user-facing changes.** Read the diff of: `DataverseGen.Core/Config/ConfigModel.cs`, `DataverseGen.Cli/Commands/*.cs`, `DataverseGen.Core/Templates/**`, custom API generators/views, `DataverseGen.Cli/Ui/**`, `DataverseGen.Core/Reporting/**`, `build.ps1`, `DataverseGen.Cli/*.csproj`, `.github/workflows/**`, `DataverseGen.Cli/example-dataversegen.config.json`. Ignore pure refactors and tests unless they change behaviour.
3. **Map to pages** using the table in `CLAUDE.md`:
   - config field -> `Configuration.md`
   - CLI command/option -> `CLI-Reference.md`
   - template set / model property rename -> `Templates.md`
   - Custom API -> `Custom-APIs.md`
   - console UI, log, parallelism, performance report -> `Output-and-Reporting.md`
   - install, packaging, credentials -> `Getting-Started.md`
   - build, test, architecture, release -> `Development.md`
4. **Verify against code, not memory.** For each affected page, read the page and the current source (option names, defaults, aliases, field types) and list every statement that is now wrong, missing or stale.
5. **Edit the wiki pages** with minimal, matching-style changes. New pages must be linked from `Home.md` and `_Sidebar.md`. Check that internal links still resolve.
6. **Check the README** only if it states something that changed (Quick Start, commands list, minimal config). Keep it short; details belong in the wiki.
7. **Report in Polish:** a short list of changed wiki files (and README if touched) with one line each on what changed, anything you were unsure about, and a reminder that nothing was committed. If nothing needs updating, say so and name what you checked.
