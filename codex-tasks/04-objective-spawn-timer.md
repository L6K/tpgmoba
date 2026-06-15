# Codex タスク 04: ObjectiveSpawnTimerModel（中央オブジェクトの出現タイマー）

> このタスクは **Enigma**（Unity 6.3 URP の 3D MOBA、リポジトリ `D:\Document\smite\smite`、ブランチ `develop`）の一部です。
> あなたはこの会話の文脈を持ちません。**この仕様だけ**を根拠に実装してください。AGENTS.md のルール（従量課金禁止・命名規約）に従うこと。
> ※ これは提案書 `codex-tasks/game-feature-proposals-2026-06-15.md` の P0「中央オブジェクト主役化」の純ロジック土台です。

## ゴール

中央オブジェクト（ボス「エニグマ・コア」）の **出現/再出現/予告タイミングを管理する plain C# モデル `ObjectiveSpawnTimerModel`** と **EditMode テスト**を作成する。
試合時間を外から渡して状態を問い合わせるだけの純ロジック。**MonoBehaviour・Unity 依存・既存ファイル改変は一切しない**（実際のスポーン/演出は別担当=Claude 側が後で結線する）。

## 厳守事項（スコープ）

- 新規作成するファイルは **2つだけ**:
  1. `D:\Document\smite\smite\Assets\_Project\Scripts\GameModes\ObjectiveSpawnTimerModel.cs`
  2. `D:\Document\smite\smite\Assets\_Project\Tests\EditMode\ObjectiveSpawnTimerModelTests.cs`
- `GameModes` フォルダは新規作成してよい（`.meta` は作らない＝Unity が自動生成）。
- 既存ファイル・asmdef は**変更しない**。
- **Unity API（UnityEngine 名前空間）を使わない**。純 C# のみ（`System` 可）。`Mathf` 不可、`System.Math` 可。

## 命名・規約

- 名前空間: `Enigma.GameModes`
- プライベートフィールドは `_camelCase`、public API はプロパティ/メソッド PascalCase。
- テストの名前空間: `Enigma.Tests`、`using NUnit.Framework;`、`using Enigma.GameModes;`、クラス `public sealed class ObjectiveSpawnTimerModelTests`。
- コメントは WHY が非自明な箇所のみ（日本語可）。

## 設計（時間は呼び出し側が渡す。内部で時刻を取得しない）

試合内経過秒 `now`（0 起点）を各メソッドに渡す。モデルは「次の出現時刻」と「撃破履歴」だけを保持し、状態は `now` から計算する。

### 状態 enum
```csharp
public enum ObjectiveState { Dormant, Warning, Active }
// Dormant: まだ出現していない(再出現待ち含む) / Warning: 出現直前の予告中 / Active: 出現中(討伐可能)
```

### API
```csharp
// firstSpawnDelay: 試合開始から最初の出現までの秒。
// respawnInterval: 撃破されてから次に出現するまでの秒。
// warningLeadSeconds: 出現の何秒前から Warning にするか。
// いずれも負値は 0 にクランプ。
public ObjectiveSpawnTimerModel(float firstSpawnDelay, float respawnInterval, float warningLeadSeconds);

// 現在(now)の状態。
public ObjectiveState GetState(float now);

// 現在出現中か（GetState(now) == Active と同値）。
public bool IsActive(float now);

// 出現の予告中か（GetState(now) == Warning と同値）。
public bool IsWarning(float now);

// 次の出現までの残り秒。Active 中は 0。負にはならない。
public float SecondsUntilSpawn(float now);

// 中央オブジェクトが撃破されたことを通知する。Active 中のみ有効（それ以外は無視）。
// 次の出現時刻を now + respawnInterval に再設定する。
public void NotifyKilled(float now);

// 全状態を初期化（試合リセット用）。次の出現時刻を firstSpawnDelay に戻す。
public void Reset();
```

## 確定仕様（曖昧さを残さない）

1. 内部に「次の出現時刻」`_nextSpawnAt`（初期値 = `firstSpawnDelay`）を持つ。
2. **Active 判定**: `now >= _nextSpawnAt` なら Active。
3. **Warning 判定**: Active でなく、かつ `(_nextSpawnAt - now) <= warningLeadSeconds`（かつ `_nextSpawnAt > now`）なら Warning。境界（ちょうど warningLead）は Warning に含める。
4. **Dormant**: Active でも Warning でもない（出現まで warningLead より先）。
5. `GetState`: 上記優先順（Active > Warning > Dormant）で1つ返す。
6. `SecondsUntilSpawn(now)`: Active なら 0、そうでなければ `Max(0, _nextSpawnAt - now)`。
7. `NotifyKilled(now)`: `IsActive(now)` が true のときだけ `_nextSpawnAt = now + respawnInterval` に更新。Active でない時の呼び出しは**無視**（状態を変えない）。
8. `Reset()`: `_nextSpawnAt = firstSpawnDelay`。
9. コンストラクタ引数はいずれも `Math.Max(0f, ...)` でクランプ。
10. 浮動小数比較はテスト側で許容誤差 `Assert.AreEqual(expected, actual, 1e-4f)`。境界テストはちょうどの値で判定を確認。

## テスト要件（最低14ケース）

- 初期: `now=0` で firstSpawnDelay>0 なら Dormant（または warningLead 次第で Warning）、Active でない
- `now < firstSpawnDelay - warningLead`: Dormant、SecondsUntilSpawn が正しく減る
- Warning 窓に入る（`firstSpawnDelay - warningLead <= now < firstSpawnDelay`）: Warning
- Warning 境界（ちょうど `firstSpawnDelay - warningLead`）: Warning
- `now == firstSpawnDelay`: Active（境界は Active）
- `now > firstSpawnDelay`: Active
- Active 中の SecondsUntilSpawn は 0
- `NotifyKilled` 後: Dormant に戻り、次出現 = killTime + respawnInterval
- 撃破後の Warning/Active が新しい `_nextSpawnAt` 基準で正しく遷移
- `NotifyKilled` を Active でない時に呼ぶと無視される（状態不変）
- 複数サイクル（出現→撃破→再出現→再撃破）で正しく回る
- `SecondsUntilSpawn` は負にならない
- `Reset` 後は初期状態（次出現 = firstSpawnDelay）に戻る
- コンストラクタ負値クランプ（例: firstSpawnDelay=-5 → 即 Active 相当 / warningLead=-1 → Warning 窓なし）

## 完了条件

1. 上記2ファイルが作成され、論理的に整合している。
2. 可能なら Roslyn / `dotnet` でコンパイル確認（Unity 非依存）。Unity EditMode 実機テストは Claude 側が統合時に一括実行する（必須ではない）。
3. 報告は **変更ファイルと要点の箇条書きのみ**。コード全文の貼り付け不要。
4. **結果報告を `D:\Document\smite\smite\codex-tasks\04-objective-spawn-timer-report.md` に書き出す**。

## 連携メモ（変更対象外・参考）

- 後で Claude 側の Director が `GetState(Time.timeSinceLevelLoad)` 等を毎フレーム参照し、Active で中央オブジェクト(ボス)を有効化、Warning で出現予告UI/演出、撃破時に `NotifyKilled` を呼ぶ。
- チームバフの付与は既存の `Enigma.Data.TeamBuffService`（`GrantDamageBuff`）が担当済み。このモデルは**タイミングのみ**を扱い、バフ内容には関与しない。
- だからこのモデルは Unity の Time に依存せず、渡された `now` だけで完結させること。
