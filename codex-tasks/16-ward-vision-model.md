# 16: WardVisionModel（設置型偵察ワードの純ロジック）

> 「ワード(設置型の視界)」のロジック部分。**純 C#・Unity 非依存・EditMode テストのみ**。
> ワードの設置・寿命・本数制限・現在アクティブな視界源リストの管理を純関数モデルで持つ。
> 実際の視界反映は既存 `Enigma.Vision.FogOfWarDirector` / `VisionRevealModel` に「視界源」として
> アクティブワードを供給する形で Claude 側が結線する（本タスクは Unity に触れない）。

## 対象ファイル（新規）
- 実装: `Assets/_Project/Scripts/Vision/WardVisionModel.cs`
- テスト: `Assets/_Project/Tests/EditMode/WardVisionModelTests.cs`

## 名前空間・規約
- `namespace Enigma.Vision`
- 標準 C# 規約（PascalCase メソッド/プロパティ、`_camelCase` private）。**UnityEngine 参照禁止**＝完全な plain C#。
- 既存 `VisionRevealModel`(同 namespace) と同じスタイル（時間は呼び出し側から `now`/`dt` を渡す純ロジック）。
- 座標は Unity 非依存にするため `(float X, float Z)` の2D（XZ平面）で扱う。

## 確定仕様（曖昧さゼロ）

### 1) struct `Ward`（出力・readonly struct）
- `int Id` … 連番ID（モデルが採番）
- `int Team` … 所属チーム（0/1 等の整数。中立は使わない想定）
- `float X`, `float Z` … 設置座標（XZ）
- `float VisionRadius` … この視界源の可視半径
- `float RemainingSeconds` … 残り寿命

### 2) class `WardVisionModel`
コンストラクタ `WardVisionModel(int maxActivePerTeam = 3, float defaultLifetime = 90f, float defaultVisionRadius = 12f)`
- `maxActivePerTeam < 1` → 1、`defaultLifetime <= 0` → 90、`defaultVisionRadius <= 0` → 12 にフォールバック。

メソッド:
- `Ward Place(int team, float x, float z, float now)`
  - 新しいワードを採番して追加。`VisionRadius=defaultVisionRadius`、`RemainingSeconds=defaultLifetime`。
  - そのチームのアクティブ数が `maxActivePerTeam` を**超える**場合、**最も古い**(残寿命が最小ではなく設置が最古=FIFO)1本を取り除いてから追加する。
  - 採番した `Ward` を返す。
- `void Tick(float dt)`
  - `dt <= 0` は無視。全ワードの `RemainingSeconds -= dt`。0 以下になったものを除去する。
- `bool Remove(int id)` … 指定IDを除去（敵ワード破壊=デナイ用）。成功で true。
- `IReadOnlyList<Ward> ActiveWards()` … 現在アクティブな全ワード（設置順）。
- `IReadOnlyList<Ward> ActiveWardsForTeam(int team)` … 指定チームのみ。
- `int CountForTeam(int team)` … 指定チームのアクティブ本数。
- `void Clear()` … 全消去。

### 3) 補足仕様
- FIFO は「設置順（古い順）」。`Place` で IDは単調増加。除去後も再利用しない。
- `RemainingSeconds` は `Place` 直後は `defaultLifetime`、`Tick` で減る。`ActiveWards` は除去済みを含まない。

## テスト要件（`WardVisionModelTests.cs`、NUnit、許容誤差 1e-4）
1. `Place` で採番が単調増加・`Ward` の各値が既定どおり。
2. `maxActivePerTeam=2` で3本目を置くと**最古**が落ち、本数が2に保たれる（残った2本が新しい方2つ）。
3. チーム別: チーム0の上限超過がチーム1のワードに影響しない。
4. `Tick`: 寿命が減り、0以下で除去される（境界: ちょうど0で除去）。
5. `Remove(id)`: 指定が消え、戻り値 true。存在しないIDは false。
6. `ActiveWardsForTeam` / `CountForTeam` / `Clear` の整合。

## 完了条件
- 上記2ファイル作成。`Enigma.Vision`・Unity 非依存。EditMode テスト緑（`dotnet test` か エディタ閉→batchmode、**エディタ起動中は batch 不可**）。件数を report に記載。
- 結果報告 `codex-tasks/16-ward-vision-model-report.md`（変更/作成ファイル・要点・テスト緑件数・残課題）。
- **本タスクの範囲を超える既存ファイルの改変はしない**（必要そうな修正は report に記すだけ）。

## 補足（Claude 側・対象外）
- 結線: ワード設置入力 → `Place`、毎フレーム `Tick`、`ActiveWardsForTeam` を `FogOfWarDirector` の味方視界源に合流（champ14m/minion8m/tower12m に「ward=VisionRadius」を追加）。ミニマップに味方ワードのアイコン表示。敵ワードは可視時のみ表示＋破壊で `Remove`。
- 課金: 従量課金サービス・画像生成は使用禁止（純コードのみ）。
