# 19: OverclockModel report

## Status
- Completed.
- Added a plain C# overclock charge/cost model under `Enigma.Ability`.
- No UnityEngine dependency was added.

## Changed files
- `Assets/_Project/Scripts/Abilities/OverclockModel.cs`
- `Assets/_Project/Scripts/Abilities/OverclockModel.cs.meta`
- `Assets/_Project/Tests/EditMode/OverclockModelTests.cs`
- `Assets/_Project/Tests/EditMode/OverclockModelTests.cs.meta`

## Summary
- Added `OverclockResult` and `OverclockModel`.
- Supports charge clamping, linear amplification, shield-first cost payment, HP cost fallback, and castability checks.

## Tests
- Added 6 NUnit EditMode tests.
- `dotnet test Enigma.Tests.EditMode.csproj --no-restore`: passed after approved escalation.

## Remaining work
- Skill input charging, resource consumption wiring, and VFX feedback are Unity-side integration work.
