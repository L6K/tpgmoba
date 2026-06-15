# Task 08 Report: BotMacroDecisionModel

## Summary
- Added `BotMacroContext`, `BotMacroAction`, and `BotMacroDecisionModel.Decide` as UnityEngine-free plain C# under `Enigma.Character`.
- Implemented ordered macro rules for Retreat, GroupForObjective, Push, Defend, and Farm using the specified constants and boundary behavior.
- Added 18 NUnit EditMode tests under `Enigma.Tests` covering priority, thresholds, and fallback behavior.

## Verification
- Passed: `dotnet test Enigma.Tests.EditMode.csproj --no-restore`.
