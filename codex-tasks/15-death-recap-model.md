# 15: DeathRecapModel（デスリキャップの純ロジック）

> ロードマップ「デス recap」のロジック部分。**純 C#・Unity 非依存・EditMode テストのみ**。
> 死亡時に「直前 N 秒で誰にどれだけ削られたか」を集計してソート済み内訳を返す純関数モデル。
> 被ダメイベントの購読・UI 表示（recap パネル）は Claude 側（Unity）が後で結線する。

## 対象ファイル（新規）
- 実装: `Assets/_Project/Scripts/Combat/DeathRecapModel.cs`
- テスト: `Assets/_Project/Tests/EditMode/DeathRecapModelTests.cs`

## 名前空間・規約
- `namespace Enigma.Combat`
- 標準 C# 規約（PascalCase メソッド/プロパティ、`_camelCase` private）。**UnityEngine 参照禁止**＝完全な plain C#。
- 既存の純ロジック（`Enigma.GameModes.ObjectiveBuffModel` 等）と同じスタイル（イベント追加 + 集計の純メソッド + 時間は呼び出し側から渡す `now`）。

## 確定仕様（曖昧さゼロ）

### 1) struct `DamageEvent`（入力・readonly struct）
- `string SourceId` … 加害者の識別子（チャンピオン名/ミニオン/タワー等。null/空は "Unknown" 扱い）
- `float Amount` … 与ダメ量（<=0 は無視）
- `float Time` … 発生時刻（秒）

### 2) struct `RecapEntry`（出力・readonly struct）
- `string SourceId`
- `float TotalDamage` … その加害者からの合計被ダメ
- `int HitCount` … ヒット回数

### 3) class `DeathRecapModel`（インスタンス・リングバッファ的に保持）
- コンストラクタ `DeathRecapModel(float windowSeconds = 12f, int maxEvents = 128)`
  - `windowSeconds <= 0` は 12 にフォールバック、`maxEvents < 1` は 128 にフォールバック。
- `void Record(string sourceId, float amount, float now)`
  - `amount <= 0` は何もしない。`sourceId` が null/空なら "Unknown"。
  - イベントを追加。保持数が `maxEvents` を超えたら最古を捨てる（FIFO）。
- `IReadOnlyList<RecapEntry> BuildRecap(float now)`
  - `now - Time <= windowSeconds` のイベントのみ対象（古いものは除外）。
  - `SourceId` ごとに `Amount` を合計・`HitCount` を数える。
  - **TotalDamage 降順**でソート（同点は SourceId の序数比較で安定）。空なら空リスト。
- `float TotalInWindow(float now)` … ウィンドウ内の全被ダメ合計。
- `void Clear()` … 全イベント破棄（リスポーン時用）。

### 4) 補足仕様
- 集計は呼び出し時計算（`BuildRecap`/`TotalInWindow` の都度ウィンドウフィルタ）。内部に Unity 型を持たない。
- ソートは決定的（同点順序が実行ごとに変わらないこと）。

## テスト要件（`DeathRecapModelTests.cs`、NUnit）
1. `Record` で amount<=0 / 複数ソースを混在 → `BuildRecap` が SourceId ごとに合計・回数を正しく集計。
2. ソート: TotalDamage 降順。3 ソースで順序検証。同点 2 ソースで SourceId 序数の安定順。
3. ウィンドウ: `windowSeconds=10` で、now-Time>10 のイベントが除外される（境界 10.0 は含む、10.01 は除外）。
4. `maxEvents` 超過で最古が落ちる（FIFO）。
5. null/空 SourceId が "Unknown" に集約される。
6. `TotalInWindow` がウィンドウ内合計と一致。`Clear` 後は空・合計 0。

## 完了条件
- 上記2ファイル作成。`Enigma.Combat`・Unity 非依存。EditMode テスト緑（`dotnet test` か エディタ閉→batchmode、**エディタ起動中は batch 不可**）。件数を report に記載。
- 結果報告 `codex-tasks/15-death-recap-model-report.md`（変更/作成ファイル・要点・テスト緑件数・残課題）。
- **本タスクの範囲を超える既存ファイルの改変はしない**（必要そうな修正を見つけたら report に記すだけ）。

## 補足
- 結線（Claude 側・対象外）: `HealthComponent.TakeDamage(amount, attacker)` の帰属情報から `Record` を呼び、死亡時に `BuildRecap` を recap UI へ。
- 課金: 従量課金サービス・画像生成は使用禁止（純コードのみ）。
