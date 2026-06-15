# Codex タスク 09: 直近変更の静的レビュー(中央オブジェクト/バグ修正/MatchHint 等)

> Enigma(`D:\Document\smite\smite`、ブランチ develop)。AGENTS.md 準拠。**レビュー専用・コード修正なし**。所見を report に書く。
> 05 のレビュー以降に追加・変更された結合コード(MonoBehaviour 層、テスト無し)を静的レビューする。
> 純ロジックでテスト済みの層は対象外: `ObjectiveBuffModel`/`MatchHintModel`/`StatusEffectModel`/`HealthModel`/`VisionRevealModel`/`ObjectiveSpawnTimerModel`(EditMode 403件緑)。

## レビュー対象(05以降の主な変更)
1. `Assets/_Project/Scripts/GameModes/CentralObjectiveDirector.cs` — 出現ライフサイクル、撃破報酬の段階バフ付与(GrantKillReward)、全味方Shield一括、CaptureCount/LastCaptureTeam、TryGetObjectivePosition、子Collider全切替。
2. `Assets/_Project/Scripts/Vision/FogOfWarDirector.cs` — 隠すユニットの Collider 無効化追加(CharacterController は対象外で移動維持の前提)、チーム未解決時 Tick 停止、IsHidden。
3. `Assets/_Project/Scripts/Characters/EnemyChampionAI.cs` — BotMacro 結線、ダッシュ(RequestDash/Update上書き)、**周回バグ修正(UpdateStuckEscape を目標WP接近進捗ベースに変更)**、MoveDirectlyToward の CC/Slow 反映、MoveSpeed バフ。
4. `Assets/_Project/Scripts/Minions/MinionAI.cs` — MinionPower 与ダメ増幅。
5. `Assets/_Project/Scripts/Objectives/TowerAttack.cs` — TowerWeaken 与ダメ減衰。
6. `Assets/_Project/Scripts/UI/GameHudController.cs` — シールド帯、中央コア状態ラベル、MatchHint 表示(UpdateHint/AlliedMinionsNearPlayer)、バフ表示(UpdateBuff を ObjectiveBuffs ベースへ)、獲得アナウンス(CaptureCount 増分検知)。
7. `Assets/_Project/Scripts/Characters/PlayerController.cs` — ダッシュ、ジャンプ(JumpSpeed)、ジャンプ演出(スクワッシュ&ストレッチ・モデルscale復元)、CC移動ゲート、MoveSpeed バフ。
8. `Assets/_Project/Scripts/Abilities/SkillCaster.cs` — 効果適用、自己バフ/ダッシュ、CastTargetedAlly/ResolveAllyUnderCursor、AoE 床接地(GroundLevelY)、IsCastValid、**Ctrl+Q/E/R でスキルレベルアップ**。
9. `Assets/_Project/Scripts/Abilities/SkillVfx.cs` — FireDirectionalVisuals 強化、AttachLight。
10. `Assets/_Project/Scripts/Combat/Projectile.cs` / `Combat/TelegraphCircle.cs` — SetStatusEffects/ApplyStatusTo(命中敵へCC)。
11. `Assets/_Project/Scripts/Combat/HealthComponent.cs` — Update で Model.Tick、Damaged を実HP減少量で発火。
12. `Assets/_Project/Scripts/Combat/DamageUtility.cs` / `Core/GameServices.cs` — ObjectiveBuffs 参照に統一、TeamBuffs 削除。
13. `Assets/Editor/BuildAetherRiftMap.cs` — 木コライダーを direction=Z の幹ポールに、プレイヤー初期スポーンを (-52,0) へ。

## 重点観点
- **イベント購読/解除の対称性**とライフサイクル: CentralObjectiveDirector(boss Died)、FogOfWarDirector/static Instance、HealthComponent.Model 購読、再生成・ドメインリロードでの二重購読/リーク。
- **null安全・初期化順**: GameServices.ObjectiveBuffs/Instance 未生成、Awake/Start 前アクセス、モデルスワップ後の参照。
- **周回バグ修正(EnemyChampionAI.UpdateStuckEscape)**: 目標WP変更時の進捗リセット、ジャングラーのピンポン反転との整合、誤って正常進行をスタック扱いしないか。
- **FoW の Collider 無効化**: CharacterController を切っていないか(切ると移動停止)。隠れ→表示で確実に戻るか。AoE(OverlapSphere)が隠れ敵に当たらなくなる仕様変更の妥当性。
- **バフ適用**: MinionPower/TowerWeaken/Damage/MoveSpeed の乗算箇所、TowerWeaken の clamp、自己 vs 味方 vs 敵の取り違え、撃破報酬の段階(回数)ロジック。
- **Ctrl+Q/E/R レベルアップ**: 通常発動と競合しないか(Ctrl押下時に発動/アームしない)、キーリリース時に誤キャストしないか、未習得スロットでも正しくランク上げできるか、ポイント不足時に無反応か。
- **ジャンプ演出**: 多重ジャンプ/モデルスワップ時の基準スケール復元、ダッシュとの併発。
- **AoE 床接地(GroundLevelY)**: 起伏地形での前提(平坦想定)。HUD/UI のポーリングの例外安全。
- **既存挙動の退行**: 触れたメソッドの元責務(攻撃 Windup ガード、ミニオン/タワーの基本挙動 等)を壊していないか。

## 報告
- コード変更なし。`D:\Document\smite\smite\codex-tasks\09-integration-review-2-report.md` に: `ファイル:行 / 重大度(High/Med/Low) / 症状 / 再現条件 / 修正案`、重大度順。問題なしの観点は「OK: …」一行。実機再現要は「要プレイ確認」明記。
- 既に実機/EditModeで確認済み: EditMode 全403件緑、プレイモードで CC/シールド/ダッシュ/スキル発射CC/FoW reveal-hide+透明壁解消/中央オブジェクト出現〜撃破バフ/Bot中央集合/NPC直進化(周回解消)/初期スポーン。これらと矛盾する指摘は再確認の上で。
