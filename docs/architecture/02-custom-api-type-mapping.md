# 02 Custom API type-mapping module

- Status: Open
- Strength: Strong

## Files

- `DataverseGen.Core/DataConverter/DataverseCustomApiConverter.cs` (~591 LOC): `NormalizeCustomApiParameterType` (~457-549), `TryNormalizeCustomApiResponseType` (~398-455), `ParseParameter` (~290-334), `ToPascalCase` (~551-590)
- `DataverseGen.Core/Generators/CustomApiCSharpGenerator.cs` (`ToPascalCase` ~260)
- `DataverseGen.Core/Metadata/MetadataNamingExtensions.cs` (~217 LOC)
- `CustomApiParameterModel` (CSharpTypeName, TypeScriptType, WebApiTypeName, WebApiStructuralProperty)
- Tests: `Tests/DataverseGen.Core.Tests/DataConverter/DataverseCustomApiConverterTests.cs`

## Problem (unverified)

- Type tables live inside a class that queries Dataverse.
- The request mapping matches by substring in fixed order: `Contains("collection")` is tested before `entity`, so `EntityCollection` may map wrongly.
- The response table patches the request mapping (Picklist, Money, StringArray fall through to `string` on requests).
- Four derived type strings are re-read by two generators: one concept spread over converter, model and generators.
- Three `ToPascalCase` copies; `capitalizeFirstWord` appears unused.
- Pure helpers are tested; the call-site ordering is not.

## Solution

One module maps a Dataverse custom API type to every target representation, with exact matching and a single table.

## Wins

- locality: one table, one place for the EntityCollection bug
- leverage: converter and generators ask one interface
- tests cover the full mapping rather than helpers

## Deletion test

Deleting the two normalizers would force the same tables into both generators, so the module concentrates complexity.

## Open questions

- Is the substring order bug reproducible with a test first?
- Do `Baselines/customapi-*` outputs change?
- Where do naming helpers (`ToPascalCase`) belong?

## Decision log

(empty)
