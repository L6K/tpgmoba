# 14: PlayerHitFeedbackModel report

## Status
- Completed.
- Implemented as plain C# logic under `Enigma.Vfx`.
- No UnityEngine / Mathf dependency was added.

## Changed files
- `Assets/_Project/Scripts/Vfx/PlayerHitFeedbackModel.cs`
- `Assets/_Project/Scripts/Vfx/PlayerHitFeedbackModel.cs.meta`
- `Assets/_Project/Tests/EditMode/PlayerHitFeedbackModelTests.cs`
- `Assets/_Project/Tests/EditMode/PlayerHitFeedbackModelTests.cs.meta`
- `Assets/_Project/Scripts/Vfx/VfxDynamicsModels.cs`

## Notes
- Added `HitFeedback` readonly struct with `FlashAlpha`, `FlashSeconds`, `VignetteStrength`, and `DirectionDegrees`.
- Added `PlayerHitFeedbackModel.Evaluate(...)` and `NormalizeAngle(...)`.
- Kept the implementation independent from scene, HUD, textures, and Unity runtime state.
- Fixed an existing mismatch in `HitStopModel`: `maxHp <= 0` now uses damage ratio `0`, matching the existing EditMode test expectation.
- Did not touch the existing uncommitted task-13 `AttackJuice.cs` combo/VFX work.

## Tests
- Added 7 NUnit EditMode tests for the new model.
- `dotnet test Enigma.Tests.EditMode.csproj --no-restore`: passed.
- First sandboxed run failed on Windows SDK path access; the same command passed after approved escalation.

## Remaining work
- Scene/HUD wiring is intentionally out of scope for this task.
- Claude/Unity side should connect this model to the actual hit flash texture/UI and play-mode visual tuning.
