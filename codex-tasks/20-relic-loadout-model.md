# 20: RelicLoadoutModel（レリック/ルーン・ロードアウトの純ロジック）

> 「レリック(試合前に選ぶ3つのパッシブ)」のロジック部分。**純 C#・Unity 非依存・EditMode テストのみ**。
> レリックのカタログ・選択(枠制限・重複禁止)・効果の集約を純関数で持つ。
> 選択UI・効果の実適用(移動速度/初手シールド等)は Claude 側が結線する。

## 対象ファイル（新規）
- 実装: `Assets/_Project/Scripts/Data/RelicLoadoutModel.cs`
- テスト: `Assets/_Project/Tests/EditMode/RelicLoadoutModelTests.cs`

## 名前空間・規約
- `namespace Enigma.Data`（既存 `CharacterOwnershipService` 等と同所）
- 標準 C# 規約・**UnityEngine 参照禁止**（完全 plain C#）。

## 確定仕様（曖昧さゼロ）

### enum `RelicEffect`
`StartShield, MoveSpeedOnKill, NeutralDamage, CooldownReduction, MaxHpBonus`

### struct `Relic`（readonly struct, カタログ要素）
- `string Id` … 一意ID
- `RelicEffect Effect`
- `float Magnitude` … 効果量（割合 or 実数。意味は結線側が解釈）

### class `RelicLoadoutModel`
コンストラクタ `RelicLoadoutModel(IReadOnlyList<Relic> catalog, int maxSlots = 3)`
- `catalog` が null/空なら空カタログ。`maxSlots < 1` → 1。Id 重複はカタログ構築時に**後勝ちで一意化**（同 Id は1つ）。

メソッド
- `bool TrySelect(string relicId)`
  - カタログに存在し、未選択で、選択数が `maxSlots` 未満なら選択して true。条件を満たさなければ false（満杯/重複/不明ID）。
- `bool Deselect(string relicId)` … 選択中なら外して true。
- `bool IsSelected(string relicId)`
- `int SelectedCount { get; }`
- `IReadOnlyList<Relic> Selected()` … 選択中レリック（選択順）。
- `IReadOnlyDictionary<RelicEffect, float> AggregateEffects()`
  - 選択中レリックの `Effect` ごとに `Magnitude` を合計した辞書（同効果の複数選択は加算）。
- `void Clear()`

### 補足
- 不明IDの `TrySelect`/`Deselect` は false（例外を投げない）。
- `AggregateEffects` は選択が空なら空辞書。

## テスト要件（NUnit、許容誤差 1e-4）
1. カタログから maxSlots=3 まで選べ、4つ目は false（満杯）。
2. 重複選択 false・未選択の Deselect false・不明ID false。
3. `Deselect` 後に空きができ再度選べる。選択順が `Selected()` に反映。
4. `AggregateEffects`: 異なる効果が個別計上、同効果2枠で Magnitude 加算。
5. カタログの Id 重複が後勝ちで一意化される。
6. `Clear` で全解除・SelectedCount=0・空集約。

## 完了条件
- 2ファイル作成。`Enigma.Data`・Unity 非依存。EditMode テスト緑（件数を report）。
- 報告 `codex-tasks/20-relic-loadout-model-report.md`。範囲外の既存改変なし。

## 補足（Claude 側・対象外）
- 結線: ホーム/キャラ選択でカタログ(SO or json)からレリック3枠選択UI→`AggregateEffects` を試合開始時に MatchBootstrap で適用(StartShield=開幕シールド/MoveSpeedOnKill/NeutralDamage=DamageUtility/CooldownReduction/MaxHpBonus)。
- 課金: 従量課金・画像生成は使用禁止（純コードのみ）。
