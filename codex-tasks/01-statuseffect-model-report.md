# Codex 結果報告: StatusEffect 基盤

対象: Claude 連携用

## 作成ファイル

- `D:\Document\smite\smite\Assets\_Project\Scripts\Combat\StatusEffectModel.cs`
- `D:\Document\smite\smite\Assets\_Project\Tests\EditMode\StatusEffectModelTests.cs`

## 要点

- `Enigma.Combat.StatusEffectModel` を純 C# で追加。
- Stun / Root は単一の残り時間で管理し、再付与時は `Max(current, duration)` でリフレッシュ。
- Slow は複数を独立管理し、最大強度から `MoveSpeedMultiplier = 1 - strongestSlow` を算出。
- `CanMove`, `CanAct`, `IsStunned`, `IsRooted`, `IsSlowed`, `MoveSpeedMultiplier` を公開。
- `Changed` は付与、期限切れ、`Clear` による実変化時のみ発火。
- `UnityEngine` は未使用。既存ファイル、asmdef、`.meta` は変更/作成していない。

## テスト結果

- 追加テスト数: 21 件。
- Roslyn 直接コンパイル: 緑。
  - 対象2ファイルのみを Unity 同梱 NUnit DLL 参照付きでコンパイル成功。
- Unity EditMode テスト: 未実行。
  - `dotnet build Enigma.Tests.EditMode.csproj --no-restore` は、この環境に `.NET Framework 4.7.1` Targeting Pack が無く失敗。
  - 失敗理由は環境依存で、今回追加コード由来のコンパイルエラーではない。

## 残課題

- Unity Editor 上で `StatusEffectModelTests` の EditMode テストを実行して全緑確認。
- Unity 起動後に `.meta` が自動生成された場合、必要に応じて `.meta` もコミット対象に含める。
