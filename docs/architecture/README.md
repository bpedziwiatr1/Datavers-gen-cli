# Architecture review: deepening candidates

Source: `improve-codebase-architecture` review. Line numbers and LOC come from a survey and are **not yet verified**.

| # | Candidate | Strength | Status |
|---|---|---|---|
| 01 | [Custom API writer module](01-custom-api-writer.md) | Strong | Done |
| 02 | [Custom API type-mapping module](02-custom-api-type-mapping.md) | Strong | Done |
| 03 | [Generation output module](03-generation-output-module.md) | Worth exploring | Partial |
| 04 | [Metadata source seam](04-metadata-source-seam.md) | Worth exploring | Open |
| 05 | [Generation pipeline in Core](05-generation-pipeline-in-core.md) | Worth exploring | Done |
| 06 | [Reporting](06-reporting.md) | Speculative | Done |
| 07 | [Connection string validation](07-connection-string-validation.md) | Speculative | Open |

Recommended order: 01+02 together, then 03, then 04+05, then 06 and 07.

## Vocabulary

- **module**: anything with an interface and an implementation.
- **interface**: everything a caller must know to use a module.
- **depth**: leverage at the interface. Deep = small interface, large implementation. Shallow = interface nearly as complex as the implementation.
- **seam**: a place where behaviour can be altered without editing in place.
- **adapter**: a concrete thing that satisfies an interface at a seam. One adapter = hypothetical seam, two = real.
- **leverage**: what callers get per unit of interface learned.
- **locality**: change, bugs and knowledge concentrate in one place.
- **deletion test**: would deleting the module concentrate complexity, or just move it?

## Extra notes (not full candidates)

- `ConfigValidation.Validate` (`Core/Config/ConfigValidation.cs`) checks only `TemplateEngine != null`; null `Entities`, `Namespace`, `OutDirectory` surface later as `NullReferenceException`.
- The language flag is a free string parsed in several places (`"cs"`, `"ts"`, `"C#"`): `InitCommand`, `TemplatesCommand`, `ScribanGenerator`, `ConfigModel.TemplateEngine.Type`.
- `Context` is the Scriban root object, so every public member of `MappingEntity` / `MappingField` is template-facing API.
