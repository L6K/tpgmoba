# Codex タスク 02: シールド吸収レイヤー（HealthModel 拡張）

> このタスクは **Enigma**（Unity 6.3 URP の 3D MOBA、リポジトリ `D:\Document\smite\smite`、作業ブランチ `develop`）の一部です。
> あなたはこの会話の文脈を持ちません。**この仕様だけ**を根拠に実装してください。AGENTS.md のルール（従量課金禁止・命名規約）に従うこと。

## ゴール

提案D（スキル個性化）の「シールド」効果の土台として、**`HealthModel`（plain C#）にシールド吸収レイヤーを追加**する。
ダメージは「シールド → HP」の順で消費し、シールドは時間で減衰（期限切れ）する。既存のHP/回復/死亡の挙動は**完全に維持**する。

## 対象ファイル（この2つのみ）

1. **改変**: `D:\Document\smite\smite\Assets\_Project\Scripts\Combat\HealthModel.cs`
2. **改変（テスト追記）**: `D:\Document\smite\smite\Assets\_Project\Tests\EditMode\HealthModelTests.cs`

- **必ず両ファイルを最初に Read してから着手する**（既存の API・テストを壊さないため）。
- `.meta` は作らない。他のファイル（HealthComponent.cs 等）は変更しない。
- `UnityEngine` を新たに使わない（HealthModel は `System` のみで完結している。それを維持）。`Mathf` ではなく `System.Math`。

## 命名・規約

- 名前空間 `Enigma.Combat`、`public sealed class HealthModel`（既存）。
- プライベートフィールド `_camelCase`、public API はプロパティ/メソッド PascalCase。
- 既存のイベント `Changed(float current, float max)` / `Died` / `Revived` はシグネチャ・発火タイミングを変えない。

## 既存の挙動（壊さないこと）

現在の `HealthModel`:
- `CurrentHp` / `MaxHp` / `IsDead`（CurrentHp<=0）
- `event Action<float,float> Changed`（current, max）, `event Action Died`, `event Action Revived`
- `TakeDamage(float amount)`: 死亡後は無視、HP を減算、`Changed` 発火、0以下で一度だけ `Died`
- `AddMaxHp(float)`, `Heal(float)`, `Revive()`

## 追加する API

### プロパティ
```csharp
// 現在有効なシールド量の合計（>= 0）。
public float Shield { get; }
```

### メソッド
```csharp
// duration 秒だけ有効な amount のシールドを付与する。amount<=0 または duration<=0 は無視。
// 死亡中（IsDead）は付与しない。
public void AddShield(float amount, float duration);

// 経過時間を進め、期限切れのシールドを取り除く。deltaTime<0 は 0 扱い。
// 期限切れで合計シールドが減ったら ShieldChanged を発火。
public void Tick(float deltaTime);
```

### イベント
```csharp
// シールド合計が変化した時に新しい合計値を通知（付与 / ダメージ吸収 / 期限切れ / Revive クリア）。
// 変化が無い操作では発火しない。
public event System.Action<float> ShieldChanged;
```

## ダメージ消費ロジック（`TakeDamage` を改修）

`TakeDamage(float amount)` の挙動を次に変更する（**シグネチャは変えない**）:

1. 死亡後（既存 `_diedFired` ガード）は従来どおり即 return。
2. `amount <= 0` は何もしない（従来挙動を維持）。
3. **まずシールドで吸収**: 有効なシールドから消費する。複数シールドがある場合は **付与が古い順（FIFO）** に消費する。
4. シールドで吸収しきれなかった残り（`amount - 吸収量`）だけ `CurrentHp` を減算する。
5. シールドが少しでも消費されたら `ShieldChanged`（新合計）を発火。HP が変化したら従来どおり `Changed` を発火。HP が0以下になったら一度だけ `Died`。
6. シールドが全額吸収して HP が変わらない場合、`Changed` は発火しない（HP 不変）。`Died` も当然発火しない。

## 確定仕様（曖昧さを残さない）

1. シールドは複数を独立保持（リスト）。各 `(amount, remaining)`。
2. **FIFO 消費**: 古いシールドから減らす。amount を使い切ったシールドは除去。
3. `AddShield`: `amount<=0` or `duration<=0` or `IsDead` のいずれかで無視（`ShieldChanged` 非発火）。有効な付与なら `ShieldChanged`（新合計）発火。
4. `Tick`: 各シールドの remaining から deltaTime を引き、`<=0` を除去。1つでも除去されたら `ShieldChanged` 発火。`deltaTime<0` は 0 扱い。
5. `Shield` は常に `>= 0`。`amount` 内部値も負にしない。
6. `Heal` と `AddMaxHp` は**シールドに影響しない**（HP のみ）。
7. `Revive()`: 既存挙動（HP 全快・`_diedFired` リセット・`Changed`/`Revived` 発火）に加え、**シールドを全クリア**する。クリアで合計が減ったら `ShieldChanged(0)` を発火。
8. 浮動小数比較はテスト側で許容誤差 `Assert.AreEqual(expected, actual, 1e-4f)`。

## テスト要件（`HealthModelTests` に追記、最低13ケース）

**既存テストは1つも削除・改変しない**。以下を追記:
- `AddShield` で `Shield` 合計が増える
- ダメージがシールド未満: シールドだけ減り HP 不変（`Changed` 非発火を購読カウンタで確認）
- ダメージがシールド超過: シールド0になり残りが HP に通る
- 複数シールドの FIFO 消費（古い方から減る）
- `Tick` でシールドが期限切れ除去され合計が減る
- シールドが全ダメージ吸収 → `IsDead=false`・HP 不変
- `AddShield(amount<=0)` / `AddShield(_,duration<=0)` は無視
- 死亡中の `AddShield` は無視
- `ShieldChanged` イベント: 付与で発火 / 吸収で発火 / 期限切れで発火 / 変化なしでは非発火（カウンタ検証）
- `Heal` / `AddMaxHp` がシールドを変えない
- `Revive` がシールドをクリアする
- `Tick(負値)` は無視される
- シールド有りで致死量ダメージ → シールド分を引いた残りで HP が0以下になり `Died` が一度だけ発火

## 完了条件

1. 上記2ファイルが改修され、**既存テストを含め全て論理的に整合**している。
2. 可能なら Roslyn / `dotnet` でコンパイル確認（Unity 非依存）。Unity EditMode 実機テストは Claude 側が統合時に一括実行する（必須ではない）。
3. 報告は **変更ファイルと要点の箇条書きのみ**。コード全文の貼り付け不要。
4. **結果報告を `D:\Document\smite\smite\codex-tasks\02-shield-absorption-report.md` に書き出す**（変更ファイル一覧・要点・テスト結果の緑/赤と件数・残課題）。

## 連携メモ（変更対象外・参考）

- 後で `HealthComponent`（MonoBehaviour）が Update で `Tick(Time.deltaTime)` を呼び、スキルの `shieldAmount`/`shieldDuration` から `AddShield` を呼ぶ。
- HUD は `ShieldChanged` を購読して HP バー上に白いシールド帯を出す予定。
- だからこのモデルは Unity の Time に依存せず、渡された deltaTime だけで完結させること。
