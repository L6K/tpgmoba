# 結果報告: レビュー指摘修正の再レビュー（06）

対象コミット: `fbb858b`（05 の High 2 / Med 4 / Low 2 への修正）
方針: 静的再レビューのみ・コード修正なし。各指摘が解消されたか／新規退行が無いかを確認する。
判定凡例: ✅解消 / ⚠️未解消 / 🔶新規懸念（重大度付き）

## 各指摘の判定

1. **High1 解消 ✅** — `SkillCaster.cs:276` で `TryConsume`（278行）より前に `IsCastValid` を通すため、対象無効時は CD/Windup を消費しない。`IsCastValid`（`SkillCaster.cs:303-310`）は `Targeting==Targeted` のみゲートし、null/射程外/`CanDamageTarget` 偽で false。Directional/GroundAoe/TargetedAlly は無条件 true なので誤弾きなし（TargetedAlly は `CastTargetedAlly` の self フォールバックと整合）。Strike 時点の二重ガードも `CastTargeted.cs:374-380` に残り整合。

2. **High2 解消 ✅（実機1点確認推奨）** — `_bossCol`（単一）→ `_bossColliders = GetComponentsInChildren<Collider>(true)`（`CentralObjectiveDirector.cs:66`）。`SetColliders(bool)`（122-128）で Spawn 時 true（102）/ Hide 時 false（111）に全切替。子 Collider 含め非表示中は当たり判定が消えるため、隠れボスへの AoE 貫通は塞がれる。
   - 🔶**Low**: `GetComponentsInChildren<Collider>(true)` は当たり判定以外の用途（検知トリガ等）の Collider も一括で enable/disable する。Dormant 中はボス由来トリガが全停止する前提で問題ないかは prefab 構成依存 → **要プレイ確認**（Renderer と同じ全切替方針なので整合はしている）。

3. **Med3 解消 ✅（仕様判断点あり）** — `HealthComponent.cs:31-35` で `before - Model.CurrentHp` を算出し、実 HP 減少がある時だけ `Damaged` を発火。`HealthModel.TakeDamage`（`HealthModel.cs:28-41`）はシールド全吸収時 `CurrentHp` 不変なので `dealtToHp==0` で非発火。ロジック整合。
   - 🔶**仕様判断（重大度なし）**: `Damaged` 購読の被弾フラッシュ/ダメージポップは「シールド全吸収時に何も出ない」。吸収を演出したい場合は `ShieldChanged`（`HealthModel.cs:20`）等の別イベントで表現する余地あり。退行ではない。

4. **Med4 解消 ✅** — `PlayerController.cs:43` / `EnemyChampionAI.cs:520` の `RequestDash` 冒頭に `if (_statusEffects != null && !_statusEffects.CanMove) return;` を追加。Root/Stun 中の新規ダッシュ発生を遮断。
   - 注: 既に進行中のダッシュは中断しない（`_dashVelocity` 適用中に Root が付与されても止まらない）。要件次第の許容点だが退行ではない → **要プレイ確認**（仕様確認のみ）。

5. **Med5 解消 ✅** — `EnemyChampionAI.MoveDirectlyToward`（`EnemyChampionAI.cs:472-473`）が `CanMove` で水平移動ゼロ＋`MoveSpeedMultiplier` を速度に乗算。`ApplyMovement`（505-508）と同一パターンで一致。中立狩り経路の CC/Slow 漏れは解消。

6. **Med6 解消 ✅** — `FogOfWarDirector.Update`（`FogOfWarDirector.cs:82-87`）でチーム未解決の間 `Tick()` を実行せず return。味方未確定フレームでの誤隠蔽を回避。
   - 注: チーム解決が恒久的に失敗すると敵が表示されたまま（fail-open）になる。視界遮蔽の喪失より味方誤隠蔽回避を優先した妥当な挙動。退行ではない。

7. **Low7 解消 ✅** — `StatusEffectController.OnEnable`（`StatusEffectController.cs:33`）で `_health == null` 時に再取得。`OnDisable`（38-41）の解除と対称で購読リーク・二重購読なし。Health 後付け構成に追従。残存制約: OnEnable 時点でも Health 未存在なら次の有効化サイクルまで未購読（低リスク・元指摘どおり）。

8. **Low8 解消 ✅** — `GameHudController.cs:369-370` で `left = CurrentHp/MaxHp`、`width = min(Shield/MaxHp, 1-hpFrac)`。`.hud-hp-shield` は `position: absolute`（`GameHud.uss:332-334`）なので inline `left` が効き、現在 HP の右に追加耐久として描画される。バー幅超過もクランプ済み。

## 退行・テストへの影響
- 既存 EditMode（純ロジック層）テストは `HealthModel` / `StatusEffectModel` / `VisionRevealModel` / `ObjectiveSpawnTimerModel` を対象。今回の変更はいずれも MonoBehaviour 結合層（`HealthComponent`/`SkillCaster` 等）に限定され、純ロジックの公開挙動は不変 → 既存テストへの退行なし。
- `HealthModelTests` は `HealthModel.Shield`/吸収を検証するが、変更したのは `HealthComponent`（ラッパ）側のため影響なし。

## 結論
05 で挙げた **High 2 / Med 4 / Low 2 はすべて解消**。新規退行は検出されず。残る確認は実機 1〜2 点（High2 のトリガ Collider 副作用、Med4 進行中ダッシュ／Med6 fail-open の仕様許容）で、いずれもロジック退行ではなく仕様判断。マージブロッカーは無し。
