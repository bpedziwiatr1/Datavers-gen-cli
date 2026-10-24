# Datavers-gen-cli

Dataverse early-bound generator for Power Platform, built as a .NET 10 CLI on top of Scriban templates.

## What Changed Recently

- The CLI now reads `dataversegen.config.json` from the current working directory.
- If `ConnectionString` is missing, the app falls back to Windows Credential Manager and prompts for one.
- The Scriban generator supports both `C#` and `ts` output types.
- Generation can run in single-file or multi-file mode.
- TypeScript generation also emits custom API request wrappers in `CustomApiOutDirectory` (default `customapi/` next to the entities), one `<name>.customapi.cs|ts` file per API.
- C# generation also emits custom API request wrappers in `CustomApiOutDirectory` (default `customapi/` next to the entities), one `<name>.customapi.cs|ts` file per API.
- Built-in templates are available through `TemplateName = "Main"`, and project templates can be copied under a `Templates` folder.
- Release builds are packaged into self-contained and framework-dependent ZIP archives by `build.ps1`.

## Current Behavior

- The app loads Dataverse metadata for the entities listed in `Entities`.
- Custom APIs are generated only for the unique names listed in `CustomApis`; if it is omitted or empty, nothing is generated and a warning is shown.
- It validates the connection string when `EnableConnectionStringValidation` is enabled.
- It can throw when an entity from `Entities` is missing if `ThrowOnEntityNotFound` is enabled.
- `TemplateEngine.Name` currently supports `scriban`.
- `TemplateEngine.Type` accepts `C#` or `ts`.
- When `TemplateEngine.IsSingleOutput` is `true`, the generator writes one output file per target.
- When `TemplateEngine.IsSingleOutput` is `false`, the generator writes multiple files per entity.

## Quick Start

1. Create `dataversegen.config.json` in the directory where you want to run the generator.
2. Fill in the Dataverse connection string, entity names, namespace, and output directory.
3. Run the CLI from that directory.

Example:

```powershell
dotnet run --project DataverseGen.Cli
```

## Commands

```powershell
dataversegen                      # generate (default); -c <path> selects another config
dataversegen init                 # or --init / -i: create dataversegen.config.json (prompts; -y for defaults, -f to overwrite)
dataversegen templates -t ts -n MyTs   # or --templates / -t: copy built-in Scriban templates to .TemplatesMyTs
```

## Configuration Example

### C# output

```json
{
  "Entities": [
    "account",
    "contact"
  ],
  "ConnectionString": "AuthType=ClientSecret;Url={url};ClientId={ClientId};ClientSecret={ClientSecret}",
  "CustomApis": [
    "newupdatestatus"
  ],
  "Namespace": "sad.Dataverse.DataAccess.Entities",
  "OutDirectory": "Dataverse",
  "EntitiesOutDirectory": "Dataverse/Entities",
  "CustomApiOutDirectory": "Dataverse/CustomApis",
  "LegacyEntityFileNames": false,
  "TemplateName": "Main",
  "TemplateDirectoryName": "Templates",
  "EnableConnectionStringValidation": true,
  "ThrowOnEntityNotFound": true,
  "TemplateEngine": {
    "IsSingleOutput": false,
    "Name": "scriban",
    "Type": "C#"
  }
}
```

`EntitiesOutDirectory`, `CustomApiOutDirectory`, `CustomApis` and `LegacyEntityFileNames` are optional (see [Configuration Fields](#configuration-fields)).

### TypeScript output

```json
{
  "Entities": [
    "account",
    "contact"
  ],
  "CustomApis": [
    "newupdatestatus"
  ],
  "ConnectionString": "AuthType=ClientSecret;Url={url};ClientId={ClientId};ClientSecret={ClientSecret}",
  "Namespace": "sad.dataverse.ui.webresource",
  "OutDirectory": "/src/dataversegen/",
  "TemplateName": "dataverse-template",
  "TemplateDirectoryName": "Templates",
  "EnableConnectionStringValidation": true,
  "ThrowOnEntityNotFound": true,
  "TemplateEngine": {
    "IsSingleOutput": true,
    "Name": "scriban",
    "Type": "ts"
  }
}
```

## Custom API

Custom API request wrappers are generated automatically for both `C#` and `ts` output.
Configuration is minimal:

- set `TemplateEngine.Type` to `C#` or `ts`
- keep the normal Dataverse connection string and output directory configuration
- optionally set `CustomApis` to a list of custom API unique names to generate
- make sure the environment contains `customapi`, `customapirequestparameter`, and `customapiresponseproperty` metadata

No extra flag is required in `dataversegen.config.json` and there is no separate custom API switch.
The generator reads the Dataverse metadata and writes custom API wrappers into:

```text
<CustomApiOutDirectory>   (default: <entities folder>/customapi/)
  <class name lowercase>.customapi.cs|ts   e.g. pingrequest.customapi.cs
```

Example TypeScript config that enables the feature:

```json
{
  "Entities": [
    "account",
    "contact"
  ],
  "CustomApis": [
    "newupdatestatus"
  ],
  "ConnectionString": "AuthType=ClientSecret;Url={url};ClientId={ClientId};ClientSecret={ClientSecret}",
  "Namespace": "sad.dataverse.ui.webresource",
  "OutDirectory": "/src/dataversegen/",
  "TemplateName": "dataverse-template",
  "TemplateDirectoryName": "Templates",
  "EnableConnectionStringValidation": true,
  "ThrowOnEntityNotFound": true,
  "TemplateEngine": {
    "IsSingleOutput": true,
    "Name": "scriban",
    "Type": "ts"
  }
}
```

The generated wrappers use:

- the custom API unique name as the operation name
- request parameter names from Dataverse metadata
- `boundParameter` metadata when the custom API is bound to an entity

If `CustomApis` is omitted or empty, no custom APIs are generated and only a warning is shown.

For APIs that define response properties, a typed response is generated next to the request in a separate file, `<name>response.customapi.cs|ts` (e.g. `getstatusresponse.customapi.cs`): in C# a `XxxResponse : OrganizationResponse` class reading `Results`, in TypeScript an `export interface XxxResponse`. APIs without response properties get no response file.

Each selected API is logged (`Found custom API: ...` when loaded, `Generated custom API: ... -> <file>` when written). Files from older versions (`<name>.cs`/`<name>.ts`) are not removed; delete them manually after upgrading.

### Filtering custom APIs

Use `CustomApis` to limit custom API generation to specific `uniquename` values:

```json
{
  "CustomApis": [
    "newupdatestatus",
    "newregistercustomer"
  ]
}
```

The names are matched against the Dataverse `uniquename` field, case-insensitively.

### Using generated custom APIs

Assume a custom API `newgetstatus` with a request parameter `customerid` (string) and response properties `statustext` (string) and `count` (int). The generator produces `GetStatusRequest` and `GetStatusResponse`.

#### C#

The request derives from `OrganizationRequest`, the response from `OrganizationResponse`. `IOrganizationService.Execute` returns the base `OrganizationResponse`, so wrap its `Results` in the generated response class:

```csharp
using Microsoft.Xrm.Sdk;
using MyApp.CustomApis; // the configured Namespace

IOrganizationService service = ...; // e.g. ServiceClient

GetStatusRequest request = new GetStatusRequest("c0ffee00-0000-0000-0000-000000000001");

OrganizationResponse raw = service.Execute(request);
GetStatusResponse response = new GetStatusResponse { Results = raw.Results };

string status = response.StatusText;
int count = response.Count;
```

Bound APIs take the target record as the last constructor argument (`new ProcessStatusRequest(flag, names, new EntityReference("account", id))`), optional parameters are optional constructor arguments. Parameter and response keys are also available as constants, e.g. `GetStatusResponse.ParameterNames.StatusText`.

#### TypeScript

The request is the default export of `<name>request.customapi.ts` and exposes `getInternalRequest()`, which is what `Xrm.WebApi.online.execute` expects. The response is only an interface (`export interface`), so type the parsed JSON body with it:

```ts
import GetStatusRequest from "./customapi/getstatusrequest.customapi";
import { GetStatusResponse } from "./customapi/getstatusresponse.customapi";

async function getStatus(customerId: string): Promise<GetStatusResponse> {
	const request = new GetStatusRequest(customerId);
	const httpResponse = await Xrm.WebApi.online.execute(request.getInternalRequest());

	return (await httpResponse.json()) as GetStatusResponse;
}

const { statustext, count } = await getStatus("c0ffee00-0000-0000-0000-000000000001");
```

The generated request refers to `IWebApiRequest`, `IWebApiInternalRequest`, `WebApiRequestStructuralProperty` and `WebApiRequestOperationType`; these types must exist in your project (they are not generated).

## Configuration Fields

- `Entities` - Dataverse schema names to generate.
- `CustomApis` - Custom API unique names to generate. If omitted or empty, no custom APIs are generated (a warning is shown).
- `ConnectionString` - Dataverse connection string. If omitted, the CLI asks for it and stores it in Windows Credential Manager.
- `Namespace` - Namespace used in generated code.
- `OutDirectory` - Output folder for generated files. Used for entities when `EntitiesOutDirectory` is not set.
- `EntitiesOutDirectory` - Optional output folder for entity files; overrides `OutDirectory`.
- `CustomApiOutDirectory` - Optional output folder for custom API request wrappers. Default: `customapi` inside the entities folder.
- `LegacyEntityFileNames` - Optional, default `false`. Multi-file output writes entity files as `<name>.entity.cs` / `<name>.entity.ts`; set to `true` to get the previous `<name>.cs` / `<name>.attributes.ts`.
- `TemplateName` - Template set to use. `Main` selects the built-in templates.
- `TemplateDirectoryName` - Folder name that contains project templates.
- `EnableConnectionStringValidation` - Enables connection string validation. Default: `false`.
- `ThrowOnEntityNotFound` - Throws when an entity from `Entities` is not found. Default: `false`.
- `TemplateEngine.Name` - Template engine name. Current value: `scriban`.
- `TemplateEngine.Type` - Output type. Use `C#` or `ts`.
- `TemplateEngine.IsSingleOutput` - Controls single-file or multi-file generation.

## Templates

### Built-in templates

Use the built-in template set with:

```json
{
  "TemplateName": "Main"
}
```

### Project templates

Copy the Scriban template files into a `Templates` folder next to your config file:

```text
MyProject
  Templates
    Dataverse-Template
      ...
  dataversegen.config.json
```

Then reference the template folder name in the config:

```json
{
  "TemplateName": "Dataverse-Template",
  "TemplateDirectoryName": "Templates"
}
```

### Custom API template

The custom API request wrappers written to `CustomApiOutDirectory` (default `<entities folder>/customapi/`) are rendered from one template per output type. A project template set may contain an optional `customapi.sbncs`; when it is missing, the built-in one is used. The `Multiple/Main` and `Multiple/TS-Main` example templates include a copy to start from.

Response files use a second optional template, `customapi.response.sbncs`, with the same fallback to the built-in one. Its variables are `namespace` (C# only), `class_name` and `properties` (each with `type`/`type_script_type`, `property_name`, `request_property_name` and, in C#, `summary`).

Variables available in `customapi.sbncs` (Scriban snake_case names):

- `class_name`, `operation_name` (already escaped for a string literal); `namespace` (C# only); `operation_type`, `bound_parameter_name` (TypeScript only).
- `properties` - request properties, the bound entity (if any) first.
- `constructor_parameters` - constructor arguments, the bound entity (if any) last.
- C# items: `type`, `property_name`, `parameter_name`, `request_property_name`.
- TypeScript items: `is_bound`, `request_property_name`, `constructor_parameter_name`, `type_script_type`, `web_api_type_name`, `web_api_structural_property`.

The output is not post-processed (blank lines are kept), so the template controls the exact layout.

## Release Build

- `pwsh -NoProfile -File build.ps1` publishes the CLI in Release mode.
- The script creates both `includedotnet` and `nodotnet` ZIP packages under `publish/`.
- It also packs a dotnet tool package `DataverseGen.Tool.<version>.nupkg` into `publish/`.
- Release automation also publishes those ZIPs and the `.nupkg` as GitHub Release assets.

## Install as a dotnet tool

Besides the ZIP/exe, the generator is available as a global dotnet tool (Windows only, requires the .NET 10 runtime). Download `DataverseGen.Tool.<version>.nupkg` from a GitHub Release into a folder and run:

```powershell
dotnet tool install --global DataverseGen.Tool --add-source <folder-with-nupkg>
dataversegen        # run in the directory that contains dataversegen.config.json
dotnet tool update --global DataverseGen.Tool --add-source <folder-with-nupkg>
dotnet tool uninstall --global DataverseGen.Tool
```

Packages built from non-release branches have a prerelease version (e.g. `2.3.10-feature-name.1`); add `--prerelease` (or `--version <version>`) to `install`/`update`, otherwise the package is reported as not found.

Custom templates are still loaded from `<current directory>/<TemplateDirectoryName>/<TemplateName>/`; example templates ship in the ZIP packages.

The app waits for a key press before exiting only when it runs in its own console window (e.g. the exe was double-clicked). When started from an existing terminal it exits right away; `--no-pause` disables the pause in every case.

## Development Notes

- The solution targets `net10.0` (SDK pinned in `global.json`).
- Run `dotnet test DataverseGen.sln` after changing generator logic.
- The test project is `Tests/DataverseGen.Core.Tests`.
