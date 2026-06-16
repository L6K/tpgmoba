# 18: RiftEventModel report

## Status
- Completed.
- Added a plain C# rift event state machine under `Enigma.GameModes`.
- No UnityEngine dependency was added.

## Changed files
- `Assets/_Project/Scripts/GameModes/RiftEventModel.cs`
- `Assets/_Project/Scripts/GameModes/RiftEventModel.cs.meta`
- `Assets/_Project/Tests/EditMode/RiftEventModelTests.cs`
- `Assets/_Project/Tests/EditMode/RiftEventModelTests.cs.meta`

## Summary
- Added `RiftState`, `RiftEffect`, `RiftStatus`, and `RiftEventModel`.
- Supports Dormant, Warning, Open, Captured, and Cooldown transitions.
- Supports capture progress, contested hold, owner/effect status, open count, reset, and cyclic effects.

## Tests
- Added 6 NUnit EditMode tests.
- `dotnet test Enigma.Tests.EditMode.csproj --no-restore`: passed after approved escalation.

## Remaining work
- Portal visuals, shortcut collider opening, team vision, and team haste application are Unity-side integration work.
