# 07 Connection string validation

- Status: Open
- Strength: Speculative

## Files

- `DataverseGen.Core/ConnectionString/` (~400 LOC, 14 files): `ConnectionStringValidator.cs`, `ConnectionStringValidatorFactory.cs`, `ConnectionStringConst.cs`, `Validators/*` (`BaseConnectionStringValidator.cs`, `AdConnectionStringValidator.cs`, `O365ConnectionStringValidator.cs`, `ValidatorTokenHelper.cs`), `Enums.cs`, `IConnectionStringValidator.cs`

## Problem (unverified)

- Validators are fixed sequences of calls (AD ~14 LOC, O365 ~16 LOC): shallow.
- `TryCheckIfTokenIsPresentWithValue` calls the throwing version and catches `ArgumentNullException` (`ValidatorTokenHelper.cs:~329-365`); bare `Exception` thrown widely; factory throws `NotImplementedException` for unknown auth type.
- Token name sets are case-sensitive and hand-listed (`"ClientId"` and `"clientid"`); `integratedSecurityToken.Value == "false"` is case-sensitive (`BaseConnectionStringValidator.cs:~157`), so `False` may pass without a password.
- `ConnectionStringConst.Thumbprint` is internal while sibling sets are public; public validators implement an internal interface.

## Solution

A data-driven table (auth type to required token groups) with one validator replaces per-auth classes.

## Wins

- locality: rules in one table
- tests per auth type become table rows
- fixes the case-sensitivity inconsistency

## Deletion test

Deleting `AdConnectionStringValidator` and `O365ConnectionStringValidator` moves their sequence into a table; little complexity remains, so they are shallow.

## Open questions

- Is the case-sensitivity issue real? Write a failing test first.
- Is the validation error type part of public behaviour (CLI messages)?

## Decision log

(empty)
