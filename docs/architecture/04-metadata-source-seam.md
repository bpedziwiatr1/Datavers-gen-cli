# 04 Metadata source seam

- Status: Open
- Strength: Worth exploring

## Files

- `DataverseGen.Core/DataConverter/DataverseConnector.cs` (~125 LOC)
- `DataverseGen.Core/DataConverter/ServiceClientPool.cs` (~58)
- `DataverseGen.Core/DataConverter/DataverseMetadataConverter.cs` (~249)
- `DataverseGen.Core/DataConverter/DataverseCustomApiConverter.cs` (~591)

## Problem (unverified)

- `new ServiceClient(...)` is created inline (`DataverseConnector.cs:~65`); `CreatePool` returns a concrete `ServiceClient`. No seam.
- No tests for `DataverseMetadataConverter`, the pool or the connector. Activity-party insertion (~225-242), duplicate handling and relationship exclusion are exercised only against a live org.
- Connector validates connection strings (reporter dropped, ~89) and recommends parallelism; connect and validate happen in the constructor.
- Pool is a pass-through at DOP 1.
- Sync wrappers `GetMappedEntities`/`GetCustomApis` use `.GetAwaiter().GetResult()`; production uses async only.

## Solution

Introduce a seam between converters and the Dataverse client, so a fake adapter can serve metadata in tests.

## Wins

- locality: Dataverse access in one module
- two adapters (live, in-memory) justify the seam
- converter logic testable without an org

## Deletion test

Deleting the pool at DOP 1 loses nothing; at DOP>1 its complexity would move into every converter.

## Open questions

- Which operations does the seam cover (RetrieveEntity, query custom API tables, request counting)?
- Do sync wrappers go?
- Where does connection string validation belong?

## Decision log

(empty)
