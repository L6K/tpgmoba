# Codex タスク 06: レビュー指摘修正の再レビュー

> Enigma（`D:\Document\smite\smite`、ブランチ `develop`）。AGENTS.md 準拠。**レビュー専用・コード修正なし**。
> 05 で指摘した結合層の不具合に対する修正（コミット `fbb858b4`）を再レビューし、各指摘が解消されたか／新たな退行が無いかを確認する。

## 経緯
- 05（[05-integration-review-report.md](05-integration-review-report.md)）で High 2 / Med 4 / Low 2 を検出。
- Claude 側で全件修正し、コンパイル緑＋一部プレイモード検証済み（Med3 吸収時非発火/spill時実HP、Med4 ルート中ダッシュ0・解除後6m、High2 Dormant中ボスCollider全無効）。

## 適用された修正（対象ファイル）
1. **High1** `SkillCaster.cs`: `IsCastValid` を追加し、Targeted は対象null/射程外/味方なら `TryConsume` 前に return（CD/Windup を消費しない）。
2. **High2** `CentralObjectiveDirector.cs`: `_bossCol`(単一)→`_bossColliders`(`GetComponentsInChildren<Collider>(true)`)に変更し、`SetColliders(bool)` で Hide/Spawn 時に全切替。
3. **Med3** `HealthComponent.cs`: `TakeDamage` で `before - Model.CurrentHp` を計算し、実HP減少がある時だけ `Damaged` を発火。
4. **Med4** `PlayerController.cs` / `EnemyChampionAI.cs`: 各 `RequestDash` 冒頭に `if (_statusEffects != null && !_statusEffects.CanMove) return;`。
5. **Med5** `EnemyChampionAI.cs` `MoveDirectlyToward`: `CanMove` で水平移動ゼロ＋`MoveSpeedMultiplier` を速度に乗算。
6. **Med6** `FogOfWarDirector.cs` `Update`: `_teamResolved` が false の間は `Tick()` を実行せず return。
7. **Low7** `StatusEffectController.cs` `OnEnable`: `_health == null` なら再 `GetComponent<HealthComponent>()`。
8. **Low8** `GameHudController.cs` `UpdateHp`: シールド帯を `left = CurrentHp/MaxHp`、`width = min(Shield/MaxHp, 1 - hpFrac)` に変更（現在HPの右に追加耐久）。

## 確認してほしいこと
- 各指摘（1〜8）が**実際に解消**されているか（ロジック上）。
- 修正で**新たなバグ/退行**が入っていないか。特に:
  - High1: Directional/GroundAoe/TargetedAlly が誤って弾かれないか（TargetedAlly は self フォールバックなので常に有効であるべき）。Strike 時点で対象が射程外/死亡した場合の二重ガード整合。
  - High2: `SetColliders` が他の用途のCollider（当たり判定以外）まで切って副作用が無いか。Spawn 時に確実に戻るか。
  - Med3: `Damaged` を購読する被弾演出/ヒットフラッシュが「シールド吸収時に何も出ない」挙動で問題ないか（仕様判断ポイントとして指摘でよい）。
  - Med4: 進行中のダッシュがルート付与で中断されない点（要件次第）。
  - Med6: チーム未解決が長引くと敵が表示されたままになる点の許容可否。

## 報告
- `D:\Document\smite\smite\codex-tasks\06-fix-rereview-report.md` に、各指摘について `解消 / 未解消 / 新規懸念` を1行ずつ＋根拠（ファイル:行）。新規懸念は重大度付き。コード全文貼り付け不要。
- 実機再現が要るものは「要プレイ確認」と明記（既に実機確認済みの項目は上記参照）。
