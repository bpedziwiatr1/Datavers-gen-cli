# Datavers-gen-cli

Dataverse early-bound generator for Power Platform, built as a .NET 10 CLI on top of Scriban templates. Windows only.

## Features

- Early-bound **C#** or **TypeScript** code from Dataverse metadata (entities, fields, option sets, relationships).
- Single-file or multi-file output.
- Typed wrappers for **Custom APIs** (request and response).
- Built-in Scriban templates, fully customizable per project.
- Parallel retrieval and generation, live progress, performance report and log file.

## Quick Start

Install the ZIP from [Releases](https://github.com/bpedziwiatr1/Datavers-gen-cli/releases) or as a dotnet tool (see [Getting Started](https://github.com/bpedziwiatr1/Datavers-gen-cli/wiki/Getting-Started)), then in your project folder:

```powershell
dataversegen init      # create dataversegen.config.json
dataversegen           # generate code
```

Minimal `dataversegen.config.json`:

```json
{
  "Entities": ["account", "contact"],
  "Namespace": "MyApp.Dataverse",
  "OutDirectory": "Dataverse",
  "TemplateName": "Main",
  "TemplateEngine": { "IsSingleOutput": false, "Name": "scriban", "Type": "C#" }
}
```

If `ConnectionString` is omitted, the CLI asks for it and stores it in Windows Credential Manager.

For interactive sign-in (browser, no password in the string) use:

```text
AuthType=OAuth;Url=https://contoso.crm.dynamics.com;AppId=51f81489-12ee-4a9e-aaae-a2591f45987d;RedirectUri=http://localhost;LoginPrompt=Auto;
```

## Commands

```powershell
dataversegen                      # generate (default); -c <path> selects another config
dataversegen init                 # create dataversegen.config.json (-y for defaults, -f to overwrite)
dataversegen templates -t ts -n MyTs   # copy built-in Scriban templates to your project
```

## Documentation

Full documentation is in the [wiki](https://github.com/bpedziwiatr1/Datavers-gen-cli/wiki):

- [Getting Started](https://github.com/bpedziwiatr1/Datavers-gen-cli/wiki/Getting-Started)
- [CLI Reference](https://github.com/bpedziwiatr1/Datavers-gen-cli/wiki/CLI-Reference)
- [Configuration](https://github.com/bpedziwiatr1/Datavers-gen-cli/wiki/Configuration)
- [Templates](https://github.com/bpedziwiatr1/Datavers-gen-cli/wiki/Templates)
- [Custom APIs](https://github.com/bpedziwiatr1/Datavers-gen-cli/wiki/Custom-APIs)
- [Output and Reporting](https://github.com/bpedziwiatr1/Datavers-gen-cli/wiki/Output-and-Reporting)
- [Development](https://github.com/bpedziwiatr1/Datavers-gen-cli/wiki/Development)

## Changelog

Release notes (Added / Changed / Fixed / Removed / Security) are generated from commit messages and published with every [GitHub Release](https://github.com/bpedziwiatr1/Datavers-gen-cli/releases).

## Development

```powershell
dotnet build DataverseGen.sln
dotnet test DataverseGen.sln
```

See the [Development](https://github.com/bpedziwiatr1/Datavers-gen-cli/wiki/Development) page for architecture, conventions and the release process.

## License

[MIT](LICENSE)
