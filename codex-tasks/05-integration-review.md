# Codex タスク 05: 結合コードの静的レビュー（提案D/E/P0）

> このタスクは **Enigma**（Unity 6.3 URP の 3D MOBA、リポジトリ `D:\Document\smite\smite`、ブランチ `develop`）の一部です。
> あなたはこの会話の文脈を持ちません。AGENTS.md のルールに従うこと。
> **これはレビュー専用タスク。コードは修正せず、所見をレポートに書き出すだけ**（修正は実機検証できる Claude 側が行う）。

## 目的

提案D（スキル個性化/CC/シールド/ダッシュ）・E（視界）・P0（中央オブジェクト）で追加された
**MonoBehaviour 結合層**（EditMode テストが無く、プレイモード検証のみの層）を静的レビューし、
バグ・リスクを洗い出す。純ロジック層（下記）は既に EditMode 361 件で担保済みなのでレビュー対象外でよい。

- 対象外（テスト済み）: `StatusEffectModel`, `HealthModel`(shield), `VisionRevealModel`, `ObjectiveSpawnTimerModel`

## レビュー対象ファイル（読むのはこれら + 必要に応じ参照先）

1. `Assets/_Project/Scripts/Combat/StatusEffectController.cs`
2. `Assets/_Project/Scripts/Combat/HealthComponent.cs`（Update で Model.Tick 追加）
3. `Assets/_Project/Scripts/Characters/Projectile.cs`（SetStatusEffects / ApplyStatusTo）
4. `Assets/_Project/Scripts/Combat/TelegraphCircle.cs`（SetStatusEffects / ApplyStatusTo）
5. `Assets/_Project/Scripts/Abilities/SkillCaster.cs`（CC適用 / ApplySelfBuffs / TryDash / CastTargetedAlly / ResolveAllyUnderCursor / isInstant）
6. `Assets/_Project/Scripts/Characters/PlayerController.cs`（RequestDash / ダッシュ上書き / CC移動ゲート）
7. `Assets/_Project/Scripts/Characters/AutoAttack.cs`（CanAct ゲート）
8. `Assets/_Project/Scripts/Characters/EnemyChampionAI.cs`（FaceAndAttack/ApplyMovement のCCゲート、CastBot* のCC/self-buff、RequestDash）
9. `Assets/_Project/Scripts/Vision/FogOfWarDirector.cs`（自動生成 / Tick で reveal-hide / IsHidden）
10. `Assets/_Project/Scripts/Minimap/MinimapController.cs`（FoW IsHidden 連動）
11. `Assets/_Project/Scripts/GameModes/CentralObjectiveDirector.cs`（出現ライフサイクル / ボス表示制御）
12. `Assets/_Project/Scripts/UI/GameHudController.cs`（シールド帯 UpdateHp / 中央コア UpdateObjective）

## 観点（重点的に見る）

- **イベント購読/解除の対称性**: `+=` した購読が `OnDisable`/`OnDestroy` で確実に外れるか。ドメインリロードや再生成での二重購読・リーク。
  - 特に StatusEffectController（HealthComponent.Model.Revived）、CentralObjectiveDirector（boss Died）、FogOfWarDirector の static Instance のライフサイクル。
- **null 安全 / 初期化順**: `GetOrAdd`、遅延生成 Model、`Instance` 参照、シーン未生成タイミング（このプロジェクトは初期化前 Update で過去に NRE バーストの履歴あり）。
- **static Instance の扱い**: 複数シーン/再生成/プレイ終了時に古い参照が残らないか（FogOfWarDirector / CentralObjectiveDirector）。
- **CC/効果適用の抜け**: 味方誤爆（TeamRules.CanDamage）、rank0、duration<=0、TargetedAlly の self フォールバックの妥当性。
- **ダッシュ**: 重力との合成、移動ロック/スタン中の扱い、距離0/方向0、CharacterController.Move の符号。
- **中央オブジェクト**: 隠蔽中にダメージが通らないか（collider off）、撃破→再出現の Revive と転倒リセットの整合、`_bossActive` の状態遷移、HasObjective false 時の HUD 非表示。
- **FoW**: 静的構造物を誤って隠さないか（CharacterController 判定）、味方/中立の分類、リンガー、毎 tick の FindObjectsByType のコストと破棄済み参照の掃除。
- **HUD**: ポーリング更新の例外安全、要素 null 時の早期 return、クラスの付け外し（warning/active/low）の取りこぼし。
- **既存挙動の退行**: 触ったメソッドの元の責務を壊していないか（特に HealthModel.TakeDamage の amount<=0 早期 return、SkillCaster の Windup ガード等）。

## 完了条件 / 報告

- **コードは変更しない**。
- 所見を `D:\Document\smite\smite\codex-tasks\05-integration-review-report.md` に書き出す。形式:
  - 各指摘: `ファイル:行` / 重大度（High/Med/Low）/ 症状 / 想定される再現条件 / 修正案（1-2行）。
  - 重大度順に並べる。問題なしの観点は「OK: <観点>」と1行で。
  - 全体所感を末尾に数行。
- コード全文の貼り付けは不要。指摘は具体的に（曖昧な「改善できそう」は避け、バグ/リスクに絞る）。

## 備考

- 実行時/プレイモードのデバッグはできない前提でよい（静的レビューに徹する）。実機再現が要る指摘は「要プレイ確認」と明記。
- 既に実機検証で確認済みの挙動: プレイヤーCC移動ゲート・シールド吸収・ダッシュ6m・スキル発射CC（テレグラフ）・FoW reveal/hide・中央オブジェクト出現ライフサイクル・ボットダッシュ。これらと矛盾する指摘は再確認の上で。
