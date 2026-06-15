# Codex タスク: StatusEffect 基盤（スタン/ルート/スロウ）

> このタスクは **Enigma**（Unity 6.3 URP の 3D MOBA、リポジトリ `D:\Document\smite\smite`、作業ブランチ `develop`）の一部です。
> あなたはこの会話の文脈を持ちません。**この仕様だけ**を根拠に実装してください。AGENTS.md のルール（従量課金禁止・命名規約）に従うこと。

## ゴール

CC（行動阻害）の土台となる **plain C# モデル `StatusEffectModel`** と、その **EditMode テスト**を作成する。
スタン / ルート / スロウを「時間」と「強度」で管理し、移動可否・行動可否・移動速度倍率を返すだけの純ロジック。
**MonoBehaviour・Unity シーン・VFX・既存ファイルの改変は一切しない**（それらは別担当が後で結線する）。

## 厳守事項（スコープ）

- 新規作成するファイルは **2つだけ**:
  1. `D:\Document\smite\smite\Assets\_Project\Scripts\Combat\StatusEffectModel.cs`
  2. `D:\Document\smite\smite\Assets\_Project\Tests\EditMode\StatusEffectModelTests.cs`
- `.meta` ファイルは作らない（Unity 起動時に自動生成される）。
- 既存ファイル（PlayerController, MovementLogic, HealthModel, asmdef 等）は **絶対に変更しない**。
- **Unity API（UnityEngine 名前空間）を使わない**。純 C# のみ（`System` は可）。`Mathf` ではなく `System.Math` を使う。これによりテストが Unity 非依存で確実に走る。

## 命名・規約（既存コードに準拠）

- 本体の名前空間: `Enigma.Combat`、クラス `public sealed class StatusEffectModel`
- テストの名前空間: `Enigma.Tests`、`using NUnit.Framework;`、`[Test]` メソッド、クラスは `public sealed class StatusEffectModelTests`
- プライベートフィールドは `_camelCase`、public API はプロパティ/メソッドで PascalCase
- コメントは WHY が非自明な箇所のみ（日本語可）

## API 仕様（`StatusEffectModel`）

状態を持つ plain C# クラス。コンストラクタは引数なし。内部で経過時間を進める方式。

### 効果の意味（重要）
| 効果 | 移動 | 行動(スキル/AA) | 速度倍率 |
|---|---|---|---|
| **Stun（スタン）** | ❌不可 | ❌不可 | （移動不可なので無関係） |
| **Root（ルート）** | ❌不可 | ✅可 | （移動不可なので無関係） |
| **Slow（スロウ）** | ✅可 | ✅可 | 1 − 強度 |

### メソッド
```csharp
// duration 秒のスタンを付与。duration <= 0 は無視。
public void ApplyStun(float duration);

// duration 秒のルートを付与。duration <= 0 は無視。
public void ApplyRoot(float duration);

// strength01（0〜1、範囲外はクランプ）の減速を duration 秒付与。duration <= 0 は無視。
public void ApplySlow(float strength01, float duration);

// 経過時間を進め、期限切れの効果を取り除く。deltaTime < 0 は無視（0 として扱う）。
public void Tick(float deltaTime);

// 全効果を即時解除（浄化/リスポーン用）。
public void Clear();
```

### プロパティ
```csharp
public bool  IsStunned  { get; }   // スタンが1つ以上有効
public bool  IsRooted   { get; }   // ルートが1つ以上有効
public bool  IsSlowed   { get; }   // スロウが1つ以上有効
public bool  CanMove    { get; }   // !IsStunned && !IsRooted
public bool  CanAct     { get; }   // !IsStunned （ルート中は行動可）
public float MoveSpeedMultiplier { get; } // 後述
```

### イベント（後で VFX/HUD が購読する。テストでも検証する）
```csharp
// 効果集合に変化があった時（付与 or 期限切れ or Clear で実際に何かが変わった時）に発火。
// 何も変化しなかった呼び出し（duration<=0 の無視、効果ゼロでの Tick 等）では発火しない。
public event System.Action Changed;
```

## 確定仕様（曖昧さを残さないこと）

1. **ハードCC（Stun / Root）の重ね掛け**: それぞれ単一の「残り時間」で保持し、付与時は `残り = Max(現在の残り, 新規 duration)`（リフレッシュ）。Stun と Root は独立。
2. **スロウの重ね掛け**: 各スロウは独立に保持し独立に期限切れする（リストで管理）。
   - `MoveSpeedMultiplier = 1 − (有効なスロウ強度の最大値)`。有効なスロウが無ければ `1`。
   - 結果は `[0, 1]` にクランプ。
   - 例: 強度0.3と0.5が同時有効 → 倍率 0.5。0.5 が期限切れ後 → 倍率 0.7。
3. **strength01 のクランプ**: `< 0` → 0、`> 1` → 1。
4. **`Tick`**: すべての有効効果の残り時間から `deltaTime` を引く。残りが `<= 0` になった効果は除去。`deltaTime < 0` は 0 扱い。
5. **`CanAct`** はルート中 `true`（ルートは移動だけ封じる）。スタン中は `false`。
6. **Changed 発火条件**: 「有効な効果の集合が実際に変化した」時のみ。具体的には ①新規付与で有効効果が増えた / 強度や残り時間が変化した時、②`Tick` で1つ以上の効果が期限切れ除去された時、③`Clear` で1つ以上除去された時。変化が無い操作では発火しない。
7. 浮動小数比較はテスト側で許容誤差（`Assert.AreEqual(expected, actual, 1e-4f)`）を使う。

## テスト要件（`StatusEffectModelTests`、最低18ケース）

最低限、以下を網羅すること（必要なら追加可）:
- 初期状態: CanMove=true, CanAct=true, MoveSpeedMultiplier=1, IsStunned/IsRooted/IsSlowed すべて false
- ApplyStun 後（期限内）: CanMove=false, CanAct=false, IsStunned=true
- ApplyStun → Tick で期限超過: 通常状態に復帰
- ApplyRoot 後: CanMove=false, **CanAct=true**, IsRooted=true
- ApplyRoot → 期限超過で復帰
- スタンのリフレッシュ: 1.0s 付与 → Tick(0.5) → 1.0s 再付与 → Tick(0.6) でまだスタン中（Max リフレッシュの確認）
- スロウ単体: ApplySlow(0.4, 2) → MoveSpeedMultiplier ≈ 0.6
- スロウ2重: 0.3 と 0.5 → 倍率 ≈ 0.5。強い方を期限切れさせる → 倍率 ≈ 0.7
- スロウ強度クランプ: ApplySlow(1.5, 2) → 倍率 0、ApplySlow(-0.5,2) は実質スロウなし扱い（倍率1のまま、IsSlowed の扱いは強度0として有効でよいが倍率は1）
- duration <= 0 の付与は無視（状態が変わらない）
- スタン+スロウ同時: CanMove=false（スタン優先）、スタン期限切れ後はスロウだけ残り CanMove=true・倍率が下がる
- Clear: すべて解除され通常状態へ
- Changed イベント: 付与で発火 / 変化なしの操作では非発火 / Tick の期限切れで発火 / Clear で発火（発火回数をカウンタで検証）
- Tick(負値) は無視される

## 完了条件

1. 上記2ファイルが作成されている。
2. **Unity の EditMode テストが全て緑**（あなたの環境で Unity を起動できない場合は、テストコードが NUnit 規約に厳密準拠し、`StatusEffectModel` の公開 API と論理的に整合していることを保証する。Unity 非依存に書いてあるので `dotnet` でコンパイル確認できるなら望ましいが必須ではない）。
3. 報告は **変更ファイルと要点の箇条書きのみ**。コード全文の貼り付け・巨大な表は不要。
4. **結果報告を `D:\Document\smite\smite\codex-tasks\01-statuseffect-model-report.md` に書き出す**（作成/変更ファイル一覧・要点・テスト結果の緑/赤と件数・残課題）。

## 連携メモ（実装の参考。ここは変更対象ではない）

- 後で `PlayerController.Update`（`Assets/_Project/Scripts/Characters/PlayerController.cs`）が `CanMove` で移動入力を無視し、`MoveSpeedMultiplier` を移動量に乗算する予定。
- HealthModel と同様、このモデルは MonoBehaviour（後で作る `StatusEffectController`）が `Tick(Time.deltaTime)` を毎フレーム呼ぶ前提。
- だからこのモデルは Unity の Time/フレームに依存せず、渡された deltaTime だけで完結させること。
