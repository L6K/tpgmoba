# 詳細設計: 中央オブジェクト完遂（①）＋ Botマクロ判断（②）の結線

> Claude 側の結線実装指針。Codex 製の純ロジック（07 `ObjectiveBuffModel` / 08 `BotMacroDecisionModel`）の受け側。
> ローカル `docs/` は参照用スナップショット。確定後 Confluence「03_詳細設計」へ同期する。

## 前提（既存）
- `CentralObjectiveDirector`(MonoBehaviour, `Enigma.GameModes`): `ObjectiveSpawnTimerModel` で出現/再出現/予告を制御。`State`/`SecondsUntilSpawn`/`HasObjective` 公開、ボスの表示/Collider/AI を on/off。
- `NeutralBossController.OnDied`(`Enigma.Objective`): 撃破時に `GameServices.TeamBuffs.GrantDamageBuff(killerTeam, 1.15f, 300f, now)`（ダメージ単体バフ）。
- `DamageUtility.ApplyTeamBuff`: `baseDamage × TeamBuffService.GetDamageMultiplier(team) × levelMult × itemMult`。
- `GameServices.TeamBuffs`(`ITeamBuffService`) が composition root。
- `EnemyChampionAI`: `Sense()`(0.3s)→`LaneBotLogic.Decide`→`ApplyMovement`。中立狩りは `MoveDirectlyToward`。CCゲート済み。

---

## ① 中央オブジェクト完遂

### 1-1. バフの正本を ObjectiveBuffModel に寄せる（推奨）
- `GameServices` に `public static ObjectiveBuffModel ObjectiveBuffs` を追加（Initialize で生成、Reset で null）。
- `DamageUtility.ApplyTeamBuff` のダメージ倍率を **`1 + ObjectiveBuffs.GetMagnitude(team, Damage, Time.time)`** に置換（従来の `TeamBuffService.GetDamageMultiplier` 相当）。
- `NeutralBossController.OnDied` のバフ付与を **Director 主導**に移す（後述の付与ポリシー）。OnDied は「撃破された」通知に専念（転倒演出のみ）。
- **TeamBuffService の扱い**: ダメージ倍率の唯一の参照だったので、移行後は不要になる。**Phase 1 では残置**（後方互換・テスト維持）、**Phase 2 で削除 or 薄いアダプタ化**。→ 要ユーザー確認（消すか残すか）。

### 1-2. 撃破報酬の付与ポリシー（Director 内、撃破回数で段階化）
`CentralObjectiveDirector` が boss `Died` を購読（既存 `OnBossKilled`）。撃破した **killerTeam**（= `bossHealth.LastAttacker` のチーム）へ:
- 1回目: `Damage 0.15 / 30s`
- 2回目: `Damage 0.15 + MoveSpeed 0.12 / 30s`
- 3回目以降: `Damage 0.20 + MoveSpeed 0.12 + 全味方へ Shield 一括付与`
- （数値は仮。`ObjectiveBuffModel.Grant(team, type, magnitude, duration, now)` を複数回呼ぶだけ）
- killerTeam が取れない（LastAttacker 不明）場合は付与スキップ。

### 1-3. 各バフ種別の適用先（query 型は都度参照、Shield は付与時1回）
| type | 適用 | 実装箇所 |
|---|---|---|
| Damage | 与ダメ ×(1+mag) | `DamageUtility.ApplyTeamBuff`（1-1） |
| MoveSpeed | 移動 ×(1+mag) | `PlayerController`/`EnemyChampionAI` の速度に乗算（既存の slow/item と合成） |
| Shield | 付与時に全味方 `HealthModel.AddShield(amount, dur)` を1回 | Director の付与時に team の HealthComponent を走査 |
| MinionPower | ミニオン与ダメ/HP ×(1+mag) | Minion 側（**Phase 2 に回す**・初回は未適用でOK） |
| TowerWeaken | 敵タワー与ダメ ×(1−mag) | `TowerAttack`（**Phase 2 に回す**） |
- Phase 1 は **Damage / MoveSpeed / Shield** の3種を実適用、MinionPower/TowerWeaken はモデルに入れるが適用は後続。

### 1-4. 演出・HUD
- Warning: 既存 `hud-objective` ラベルが「出現まもなく」を表示済み。加えて `GameSfx` 予告音（任意）。
- Spawn: ラベル「出現中」＋出現バースト（`SkillVfx.SpawnPillar` 等を boss 位置に）。
- Capture（撃破）: 全体アナウンス「〇〇チームが中央コアを制圧！」（HUD の `_announce` ラベル流用 or KillFeed）。獲得チームの色。HUD バフバーに付与バフのアイコン（**画像は5.5待ち**＝当面はテキスト/色で表現）。
- Bot 争奪は ② と連動（GroupForObjective）。

---

## ② Botマクロ判断

### 2-1. BotMacroContext の構築（EnemyChampionAI、Sense と同頻度 0.3s）
- `SelfHpFraction` = `_health.Model.CurrentHp / MaxHp`
- `AlliesAlive` / `EnemiesAlive` = チャンピオン(`PlayerController`/`EnemyChampionAI` 保持 GO)を `TeamTag` でチーム分けし、`HealthComponent.Model.IsDead==false` を数える。**0.3s 毎の全走査で可**（規模小）。自チーム=自分の `_teamTag.Team`。
- `ObjectiveActiveOrSoon` = `CentralObjectiveDirector.Instance != null && (State==Active || State==Warning)`
- `DistanceToObjective` = Director に公開する **`bool TryGetObjectivePosition(out Vector3)`**（boss の `_bossSpawnPos`）からの距離。取れなければ大値。
- `AlliedMinionsPresent` = `_perception` の味方ミニオン情報を流用（LaneBotLogic が使う前進可否と同源）。
- `UnderTowerThreat` = 最寄り敵タワー（`TowerAttack` 保持の敵チーム構造物）が一定距離内か。

### 2-2. 行動マッピング（Update 内、`LaneBotLogic.Decide` の前段で上書き/補正）
`var macro = BotMacroDecisionModel.Decide(ctx);`
- **GroupForObjective**: `TryGetObjectivePosition` へ `MoveDirectlyToward`（中立狩りと同経路）。到達圏内で敵チャンピオンが居れば `FaceAndAttack`。→ Bot 中央争奪が成立。
- **Retreat**: `ApplyMovement(LaneMove.Backward)` を強制（泉/タワー側へ）。
- **Push**: `LaneBotLogic.Decide` の結果を尊重しつつ前進寄りに（既存 Forward）。
- **Defend**: タワーゾーンで待機（既存のタワーゾーン規律を流用、前進しない）。
- **Farm**: 既存挙動（レーン/ジャングル）にフォールバック。
- 実装方針: `macro` を Sense で算出して保持し、Update 冒頭で `GroupForObjective`/`Retreat` のときは専用経路で `return`、それ以外は従来の `LaneBotLogic` フローへ。**最小侵襲**で既存レーン/ジャングル挙動を壊さない。

### 2-3. Director 連携
- `CentralObjectiveDirector` に `public bool TryGetObjectivePosition(out Vector3 pos)` を追加（`_resolved && _boss != null` のとき boss 位置）。
- Active 中のみ「争奪して撃破→ 1-2 の報酬」までつながる。

---

## 検証計画（coplay 実機）
- ①: 撃破→ `ObjectiveBuffModel` に Damage/MoveSpeed が入る／全味方 Shield 付与／DamageUtility と移動に反映（数値ダンプ）。複数回撃破で段階化。HUD アナウンス表示。
- ②: `Decide` の各分岐を実機で（低HP→撤退、Active 中→中央集合、戦力有利→push）。ボットが実際に中央へ寄るかを座標ダンプで確認。
- EditMode 全件緑（07/08 のテスト込み）を batchmode で。

## 要ユーザー確認
1. **TeamBuffService を Phase 2 で削除**してよいか（ObjectiveBuffModel に一本化）／残置か。
2. バフ段階化の**数値・持続**（上記は仮）。
3. MinionPower/TowerWeaken の適用を **Phase 2 送り**でよいか（初回は Damage/MoveSpeed/Shield）。
