# 20: RelicLoadoutModel report

## Status
- Completed.
- Added a plain C# relic loadout model under `Enigma.Data`.
- No UnityEngine dependency was added.

## Changed files
- `Assets/_Project/Scripts/Data/RelicLoadoutModel.cs`
- `Assets/_Project/Scripts/Data/RelicLoadoutModel.cs.meta`
- `Assets/_Project/Tests/EditMode/RelicLoadoutModelTests.cs`
- `Assets/_Project/Tests/EditMode/RelicLoadoutModelTests.cs.meta`

## Summary
- Added `RelicEffect`, `Relic`, and `RelicLoadoutModel`.
- Supports catalog dedupe with last duplicate winning, slot limits, select/deselect, selection order, aggregate effects, and clear.

## Tests
- Added 6 NUnit EditMode tests.
- `dotnet test Enigma.Tests.EditMode.csproj --no-restore`: passed after approved escalation.

## Remaining work
- Character-select UI, catalog source data, and match bootstrap effect application are Unity-side integration work.
