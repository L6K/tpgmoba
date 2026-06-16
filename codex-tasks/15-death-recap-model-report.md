# 15: DeathRecapModel report

## Status
- Completed.
- Added a plain C# death recap aggregation model under `Enigma.Combat`.
- No UnityEngine dependency was added.

## Changed files
- `Assets/_Project/Scripts/Combat/DeathRecapModel.cs`
- `Assets/_Project/Scripts/Combat/DeathRecapModel.cs.meta`
- `Assets/_Project/Tests/EditMode/DeathRecapModelTests.cs`
- `Assets/_Project/Tests/EditMode/DeathRecapModelTests.cs.meta`

## Summary
- Added `DamageEvent`, `RecapEntry`, and `DeathRecapModel`.
- Supports event recording, window filtering, source aggregation, deterministic sorting, total damage calculation, max event trimming, and clear.

## Tests
- Added 6 NUnit EditMode tests.
- `dotnet test Enigma.Tests.EditMode.csproj --no-restore`: passed after approved escalation.

## Remaining work
- UI/event wiring is intentionally left for the Unity/Claude side.
