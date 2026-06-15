# Codex タスク 08: BotMacroDecisionModel（Bot のマクロ判断）

> Enigma（`D:\Document\smite\smite`、ブランチ `develop`）。AGENTS.md 準拠。文脈なし・この仕様のみ。
> ※ 提案 P1「Bot マクロ判断」。集合/撤退/押し引き/オブジェクト寄りを決める純ロジック。

## ゴール
試合状況から Bot のマクロ行動を1つ決める **純関数 `BotMacroDecisionModel.Decide` と入力 struct `BotMacroContext`、enum `BotMacroAction`** ＋ EditMode テストを作る。
**MonoBehaviour・Unity依存・既存ファイル改変なし**（結線は Claude 側が EnemyChampionAI で行う）。

## 対象ファイル（この2つのみ・新規）
1. `D:\Document\smite\smite\Assets\_Project\Scripts\Characters\BotMacroDecisionModel.cs`
2. `D:\Document\smite\smite\Assets\_Project\Tests\EditMode\BotMacroDecisionModelTests.cs`
- `.meta` は作らない。既存ファイル/asmdef 変更不可。

## 規約・依存
- 名前空間 `Enigma.Character`（既存 `LaneBotLogic` と同じ層）。テストは `Enigma.Tests`。
- **UnityEngine を使わない**（`System` のみ。Vector等は使わず、距離等は float で受け取る）。
- プライベートは `_camelCase`、public は PascalCase。`Decide` は副作用なしの純関数（static）。

## API
```csharp
public enum BotMacroAction { Farm, Push, GroupForObjective, Retreat, Defend }

public readonly struct BotMacroContext
{
    public readonly float SelfHpFraction;        // 0..1
    public readonly int   AlliesAlive;           // 自分を含む生存味方数
    public readonly int   EnemiesAlive;          // 生存敵数
    public readonly bool  ObjectiveActiveOrSoon; // 中央オブジェクトが出現中 or まもなく(Warning)
    public readonly float DistanceToObjective;   // 自分→中央オブジェクトの距離(m)
    public readonly bool  AlliedMinionsPresent;  // 進軍先レーンに味方ミニオンが居る
    public readonly bool  UnderTowerThreat;      // 敵タワー射程内など危険下にいる
    public BotMacroContext(float selfHpFraction, int alliesAlive, int enemiesAlive,
                           bool objectiveActiveOrSoon, float distanceToObjective,
                           bool alliedMinionsPresent, bool underTowerThreat) { ... 代入 ... }
}

public static class BotMacroDecisionModel
{
    public const float LowHpFraction       = 0.35f; // 撤退を考える HP 閾値
    public const float SafeHpFraction      = 0.45f; // 攻めに出てよい HP 閾値
    public const float ObjectiveJoinRange  = 35f;   // 中央へ寄る最大距離

    public static BotMacroAction Decide(in BotMacroContext ctx);
}
```

## 判定ロジック（優先順・上から評価し最初に合致したものを返す）
1. **Retreat**: `SelfHpFraction < LowHpFraction` かつ（`EnemiesAlive >= AlliesAlive` または `UnderTowerThreat`）。
2. **GroupForObjective**: `ObjectiveActiveOrSoon` かつ `SelfHpFraction >= SafeHpFraction` かつ `EnemiesAlive <= AlliesAlive`（人数不利でない）かつ `DistanceToObjective <= ObjectiveJoinRange`。
3. **Push**: `AlliesAlive > EnemiesAlive` かつ `SelfHpFraction >= SafeHpFraction` かつ `AlliedMinionsPresent`。
4. **Defend**: `UnderTowerThreat` かつ `!AlliedMinionsPresent`（盾が無いので無理に出ず守る）。
5. **Farm**: 上記いずれにも該当しないデフォルト。

## 確定仕様
- 評価は上記1→5の順。最初に true になった行動を返す（排他）。
- 境界はテストで厳密確認（例: `SelfHpFraction == LowHpFraction` は Retreat 条件の `<` を満たさない＝撤退しない、`== SafeHpFraction` は `>=` を満たす）。
- 閾値は上記 const を使用（マジックナンバー直書き禁止）。
- 入力の妥当性検証は不要（呼び出し側が正しい値を渡す前提）。ただし計算は渡された値のみで完結。

## テスト要件（最低14ケース）
- 低HP＋人数不利 → Retreat / 低HP＋タワー脅威 → Retreat
- 低HPだが人数有利かつタワー脅威なし → Retreat にならない（次の条件へ）
- HP境界: `SelfHpFraction == LowHpFraction` は撤退しない
- オブジェクト出現中＋安全HP＋人数互角以上＋射程内 → GroupForObjective
- オブジェクト出現中でも射程外 → GroupForObjective にならない
- オブジェクト出現中でも人数不利 → GroupForObjective にならない
- 人数有利＋安全HP＋味方ミニオン有 → Push
- 人数有利でも味方ミニオン無 → Push にならない
- タワー脅威＋味方ミニオン無 → Defend
- どれにも該当しない平常 → Farm
- 優先順: Retreat 条件と Push 条件が両立する状況で Retreat が勝つ（低HP優先）
- `SelfHpFraction == SafeHpFraction` 境界で Push/Group が成立する

## 完了条件 / 報告
1. 2ファイル作成・整合。可能なら Roslyn コンパイル確認。EditMode 実機テストは Claude 側。
2. 報告は箇条書き＋`D:\Document\smite\smite\codex-tasks\08-bot-macro-decision-model-report.md`。

## 連携メモ（変更対象外）
- 後で `EnemyChampionAI` が毎知覚 tick 程度で `BotMacroContext` を組み立て `Decide` を呼び、結果に応じて既存の `LaneBotLogic`（Push/Engage/Retreat）や中央オブジェクトへの移動を上書き/補正する。
- `ObjectiveActiveOrSoon` は `ObjectiveSpawnTimerModel`（task04）の State から、人数は TeamTag 走査から Claude 側が算出する。本モデルは値を受け取るだけ。
