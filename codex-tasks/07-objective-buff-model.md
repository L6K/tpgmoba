# Codex タスク 07: ObjectiveBuffModel（中央オブジェクト報酬バフの多様化）

> Enigma（`D:\Document\smite\smite`、ブランチ `develop`）。AGENTS.md 準拠。文脈なし・この仕様のみ。
> ※ 提案 P0「中央オブジェクト主役化」の続き。バフを「+15%ダメージ固定」から複数種別に拡張するための純ロジック土台。

## ゴール
中央オブジェクト討伐報酬の **チーム別・複数種別バフを時間管理する plain C# モデル `ObjectiveBuffModel`** と EditMode テストを作る。
**MonoBehaviour・Unity依存・既存ファイル改変なし**（適用は Claude 側の Director が行う）。

## 対象ファイル（この2つのみ・新規）
1. `D:\Document\smite\smite\Assets\_Project\Scripts\GameModes\ObjectiveBuffModel.cs`
2. `D:\Document\smite\smite\Assets\_Project\Tests\EditMode\ObjectiveBuffModelTests.cs`
- `.meta` は作らない。既存ファイル/asmdef 変更不可。

## 規約・依存
- 名前空間 `Enigma.GameModes`。テストは `Enigma.Tests`。
- **UnityEngine を使わない**（`System` / `System.Collections.Generic` のみ）。`Mathf` 不可・`System.Math` 可。
- `TeamId` は既存の `Enigma.Combat.TeamId`（plain enum, Unity非依存）を `using Enigma.Combat;` で使用。
- プライベートは `_camelCase`、public はプロパティ/メソッド PascalCase。

## API
```csharp
public enum ObjectiveBuffType { Damage, MinionPower, MoveSpeed, Shield, TowerWeaken }

public sealed class ObjectiveBuffModel
{
    // team に type のバフを magnitude(強度) で duration 秒付与。now=試合内経過秒。
    // magnitude<=0 または duration<=0 は無視。
    public void Grant(TeamId team, ObjectiveBuffType type, float magnitude, float duration, float now);

    // (team,type) の現在有効な最大 magnitude。無ければ 0。
    public float GetMagnitude(TeamId team, ObjectiveBuffType type, float now);

    // (team,type) の残り秒(有効エントリの最大 expiry − now)。無ければ 0。負にしない。
    public float GetRemainingSeconds(TeamId team, ObjectiveBuffType type, float now);

    // team で現在有効な type の一覧(HUD表示用、重複なし)。順序は問わない。
    public IReadOnlyList<ObjectiveBuffType> GetActiveTypes(TeamId team, float now);

    // 全クリア(試合リセット)。
    public void Clear();
}
```

## 確定仕様
1. 内部は `(team,type)` ごとにエントリ `(magnitude, expiresAt)` のリストを保持。
2. `Grant`: `magnitude<=0 || duration<=0` は無視。有効なら `(magnitude, now+duration)` を追加。**追加時にその (team,type) の期限切れ(expiresAt<=now)エントリを掃除**する(無限増加防止)。
3. **重ね掛け = 最大採用**: `GetMagnitude` は `expiresAt > now` のエントリの magnitude の最大値。無ければ 0。
4. `GetRemainingSeconds` = `expiresAt > now` のエントリの最大 expiresAt − now。無ければ 0。
5. `GetActiveTypes(team)` = その team で `expiresAt > now` のエントリを持つ type 集合（重複排除）。
6. team 同士・type 同士は完全に独立。
7. `Clear` で全エントリ破棄。
8. 浮動小数比較はテストで許容誤差 `1e-4f`。

## テスト要件（最低14ケース）
- Grant→GetMagnitude が強度を返す / 期限超過(now進める)で 0
- 同 (team,type) 2回 Grant(0.2と0.5) → GetMagnitude=0.5、強い方が期限切れ→0.2
- magnitude<=0 / duration<=0 は無視
- team 別独立（Blue に付与しても Red は 0）
- type 別独立（Damage 付与しても MoveSpeed は 0）
- GetRemainingSeconds が正しい / 期限後 0 / 負にならない
- GetActiveTypes が有効 type のみ返す / 期限切れは含まない / 重複なし
- Clear 後は全て 0 / GetActiveTypes 空
- Grant 時に期限切れエントリが掃除される（内部肥大化しない: 同 type を期限切れ→再Grant して GetMagnitude が新値のみ）

## 完了条件 / 報告
1. 2ファイル作成・整合。可能なら Roslyn コンパイル確認（Unity非依存）。EditMode 実機テストは Claude 側。
2. 報告は箇条書き＋`D:\Document\smite\smite\codex-tasks\07-objective-buff-model-report.md` に作成/要点/テスト件数。

## 連携メモ（変更対象外）
- 既存 `Enigma.Data.TeamBuffService`（`GrantDamageBuff`/`GetDamageMultiplier`）は「ダメージバフ単体」を担当中。本モデルは多種バフを汎用管理する後継候補。統合（TeamBuffService を本モデルへ寄せるか併存か）は Claude 側で判断するので、本モデルは独立・純粋に保つこと。
- 適用解釈は Claude 側: 例 Damage→与ダメ ×(1+magnitude)、MoveSpeed→移動 ×(1+magnitude)、TowerWeaken→敵タワー与ダメ ×(1−magnitude)、Shield→付与量、MinionPower→ミニオン強化。モデルは magnitude を返すだけでよい。
