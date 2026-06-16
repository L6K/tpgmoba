# 14: PlayerHitFeedbackModel（被弾フィードバックの純ロジック）

> task13 item4「画面被弾フラッシュ」のロジック部分。**純 C#・Unity 非依存・EditMode テストのみ**。
> シーン/HUD 結線・テクスチャ(hit_flash_radial)の重畳は Claude 側（coplay）が担当する。本タスクは
> 「被ダメ情報 → フラッシュ強度・持続・被弾方向・低HPビネット」を決める純関数モデルを作るだけ。

## 対象ファイル（新規）
- 実装: `Assets/_Project/Scripts/Vfx/PlayerHitFeedbackModel.cs`
- テスト: `Assets/_Project/Tests/EditMode/PlayerHitFeedbackModelTests.cs`

## 名前空間・規約
- `namespace Enigma.Vfx`
- 標準 C# 規約（PascalCase メソッド/プロパティ、`_camelCase` private）。Unity API（UnityEngine 参照）禁止＝完全な plain C#。
- 既存の `Assets/_Project/Scripts/Vfx/VfxDynamicsModels.cs`（`HitStopModel` / `ScreenShakeTraumaModel` / `BeamEnvelope`）と同じ「純ロジック＋静的/インスタンスメソッド」スタイルに合わせる。

## 確定仕様（曖昧さゼロ）

### 1) 構造体 `HitFeedback`（戻り値・readonly struct）
フィールド（すべて float、public readonly）:
- `FlashAlpha` … 全画面赤フラッシュの初期アルファ（0..1）
- `FlashSeconds` … フラッシュのフェード時間（秒）
- `VignetteStrength` … 低HP時に残る赤ビネットの強さ（0..1、フラッシュとは別の持続表現）
- `DirectionDegrees` … 被弾方向インジケータの角度（0..360、後述）

### 2) 静的クラス `PlayerHitFeedbackModel`

#### `static HitFeedback Evaluate(float damage, float maxHp, float currentHpAfter, bool isCrit, float directionDegrees)`
- `severity` = `maxHp <= 0 ? 0 : Clamp01(damage / maxHp)`（被ダメのHP割合）
- **FlashAlpha** = `Clamp(0.15 + 1.1 * severity, 0, 0.85)`、ただし `isCrit` の場合は `*= 1.25`（再度 0.85 で上限クランプ）。`damage <= 0` のときは全フィールド 0 の `HitFeedback` を返す（フラッシュなし）。
- **FlashSeconds** = `Clamp(0.12 + 0.5 * severity, 0.12, 0.5)`
- **VignetteStrength**: 残HP割合 `hpFrac = maxHp <= 0 ? 1 : Clamp01(currentHpAfter / maxHp)` に対し、`hpFrac <= 0.30` のとき線形に立ち上げる: `hpFrac >= 0.30 → 0`、`hpFrac <= 0.0 → 1`、その間は `(0.30 - hpFrac) / 0.30`。Clamp01。
- **DirectionDegrees** = `directionDegrees` を `[0,360)` へ正規化（`((d % 360) + 360) % 360` 相当を float で）。

#### `static float NormalizeAngle(float degrees)`
- 任意の度数を `[0,360)` へ正規化する公開ヘルパー（上の正規化に使用、テストからも検証）。

#### Clamp/Clamp01 は private static ヘルパーで自前実装（UnityEngine.Mathf 禁止）。

## テスト要件（`PlayerHitFeedbackModelTests.cs`、NUnit）
最低限以下を網羅（各 Assert に許容誤差 1e-4）:
1. `damage <= 0` → 全フィールド 0。
2. 軽い被弾（severity 小, 例 damage=10,maxHp=1000）→ FlashAlpha ≈ 0.15+0.011、FlashSeconds ≈ 0.12 近傍、上限未達。
3. 重い被弾（severity 大, 例 damage=900,maxHp=1000）→ FlashAlpha が 0.85 でクランプ、FlashSeconds が 0.5 でクランプ。
4. クリット倍率: 同一 damage で `isCrit=true` のほうが FlashAlpha が大きい（ただし 0.85 上限を超えない）。
5. VignetteStrength: `currentHpAfter/maxHp` が 0.30 で 0、0.15 で 0.5、0 で 1（境界＋中間）。
6. DirectionDegrees / NormalizeAngle: `-90 → 270`、`450 → 90`、`360 → 0`、`0 → 0`、`179.5 → 179.5`。
7. クランプ境界: severity=0 と severity=1 での FlashAlpha/FlashSeconds の下限・上限一致。

## 完了条件
- 上記2ファイルを作成。`Enigma.Vfx` 名前空間・Unity 非依存。
- EditMode テストが緑（`dotnet test` もしくはエディタ閉→batchmode `-runTests`。**エディタ起動中は batch 不可**）。テスト件数を report に記載。
- 結果報告 `codex-tasks/14-hit-feedback-model-report.md`（変更/作成ファイル・要点箇条書き・テスト緑件数・残課題）。コード全文の貼付は不要。

## 補足
- 結線（Claude 側・本タスク対象外）: `PlayerHitFeedback` MonoBehaviour（被弾イベント購読）が本モデルを呼び、`hit_flash_radial` テクスチャの全画面 UI を `FlashAlpha`→0 にフェード、低HP時 `VignetteStrength` でビネット、`DirectionDegrees` で被弾方向リングを回す。
- 課金: 従量課金サービス・画像生成は使用禁止（純コードのみ）。
