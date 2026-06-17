# 詳細設計: キャラ試用シーン ＋ デスリキャップ / レリック / 次元リフト / オーバークロック

> 2026-06-17 実装分の結線指針。純ロジックは Codex 納品（`DeathRecapModel` / `RelicLoadoutModel` / `RiftEventModel` / `OverclockModel`）。
> ローカル `docs/` は参照用スナップショット。確定後 Confluence「03_詳細設計」へ同期する。

---

## 0. キャラ試用シーン（Sandbox）

- **シーン**: `Assets/Scenes/Sandbox.unity`。生成メニュー `Enigma/Build Sandbox Scene`。
- **設計**: `BuildAetherRiftMap` を `partial class` 化し、別ファイル `Assets/Editor/BuildSandbox.cs` から本番ビルダーの private ヘルパー（`AttachUnityChanModel` / `CreateWorldHealthBar` / `GetOrCreate*Mat` / `SetMat`）を再利用。プレイヤー構築200行の複製と本番ビルダー破壊を回避。
- **共有プレハブ**: AA/スキル弾は永続資産（`AaBeam` / `Projectile` / `Telegraph` / `TargetRing`）を `AssetDatabase.LoadAssetAtPath` で読込。
- **司令塔**: `CharacterSandbox`（`Enigma.Sandbox`）。`M` キーでキャラ一覧（IMGUI）→ `ChampionModelSwapper.Apply` + `SkillCaster.SetSkills` + ステータス適用。全スキル解放は `SkillProgression.GrantAllRanks(1)`（R の Lv ゲートも無視）。`R` キーでレリック選択メニュー（最大3）。的＝赤ダミー3体（`TargetDummy`：死亡で自動復活）。
- **ChampionModelSwapper の冪等化（重要）**: `Apply` は元々「一度だけ呼ぶ」前提で旧モデルを破棄せず、`Muzzle` がモデルの手にぶら下がるため連続切替で破綻。→ 新モデル生成前に `Muzzle` をプレイヤー直下へ退避してから旧 `ChampionModel` を破棄、UnityChan フォールバック経路でも motor/muzzle/death 参照を再結線。
- **HPバー埋もれ修正**: ボットの頭上 `HealthBar` ローカルY 0.65→1.3（カプセル天面 y+1.0 より上）。保存済み `AetherRift_Map.unity` へは一時エディタスクリプト（OpenScene→localY書換→SaveScene）でピンポイント反映（全再生成 Execute のchurnを避ける）。

---

## 1. デスリキャップ（task15）

- **純ロジック**: `DeathRecapModel`（`Enigma.Combat`）。`Record(sourceId, amount, now)` / `BuildRecap(now)`→被ダメ集計（既定窓12秒）。
- **結線**: `PlayerDeathRecap`（`Enigma.Combat`、Player に付与）。`HealthComponent.Damaged` を購読し、直前にセットされる `LastAttacker` を攻撃者として `Record`。`HealthModel.Died`→`BuildRecap`→`GameHudController.ShowDeathRecap`、`Revived`→`Clear`。
- **名前整形**: `DeathRecapSourceName.Clean(name)`（純関数。`(Clone)` 除去・`_`→空白）。
- **HUD**: `GameHudController.ShowDeathRecap(title, entries, holdMs)` は UXML 非依存のランタイム `VisualElement` パネルを遅延生成して数秒表示。
- **結線先**: AetherRift_Map / Sandbox 両シーンの Player。AetherRift は一時スクリプト `PatchDeathRecap`（OpenScene→AddComponent→結線→SaveScene）で追加。

---

## 2. レリック（task20）

- **純ロジック**: `RelicLoadoutModel`（`Enigma.Data`）。catalog から最大3選択、`AggregateEffects()`→`Dictionary<RelicEffect,float>`。
- **カタログ**: `RelicCatalog`（`Enigma.Data`、表示メタ付き8種）。効果＝`MaxHpBonus` / `StartShield` / `CooldownReduction` / `MoveSpeedOnKill`。
- **適用**: `RelicApplier.ApplyIds(ids, HealthModel, SkillCaster, GameObject player)`。
  - `MaxHpBonus`→`HealthModel.AddMaxHp`（生存中は CurrentHp も同量増）
  - `StartShield`→`HealthModel.AddShield(amount, 60s)`
  - `CooldownReduction`→`SkillCaster.SetCooldownReduction(frac)`（実効CD再構成、0〜0.6クランプ）
  - `MoveSpeedOnKill`→遅延効果のため `PlayerRelicEffects`（`Enigma.Data`、Player に付与）へ値を格納
- **キル時加速の発火**: `KillFeedDirector.OnChampionDied` でプレイヤーキル時に `PlayerRelicEffects.MoveSpeedOnKill` を読み、`StatusEffectController.GetOrAdd(killer).ApplyHaste(frac, 4s)`。
- **ヘイスト新設**: `StatusEffectModel`/`Controller` に `ApplyHaste(strength, duration)`。`MoveSpeedMultiplier = clamp01(1 - 最強slow) × (1 + 最強haste)`。Tick/Clear 対応。
- **永続化**: `IMatchContext.SelectedRelicIds`（`MatchContext` 実装）。`CharacterSelectController` のロックイン時に保存、`MatchBootstrap` が試合開始時に適用。
- **選択UI**: `CharacterSelect.uxml/.uss` に `cs-relics`/`cs-relic-list`/`cs-relics-title` を追加、`CharacterSelectController.BuildRelicList/ToggleRelic/SelectedRelicIdList`。
- **未対応（次スライス）**: `NeutralDamage`（与ダメ経路に対象チームを流す必要があり、カタログ未収録）。

---

## 3. 次元リフト（task18）

- **純ロジック**: `RiftEventModel`（`Enigma.GameModes`）。状態機械 Dormant→Warning→Open→Captured→Cooldown。`Tick(now, dt, presentTeam)`→`RiftStatus`。出現回ごとに `Shortcut→TeamVision→TeamHaste` 循環。
- **ディレクタ**: `RiftDirector`（`Enigma.GameModes`）。`RuntimeInitializeOnLoadMethod` で AetherRift_Map に自動生成（シーン編集不要）。
  - **占拠判定**: ゾーン（入口 `(0,1.1,26)`、半径5）内のチャンピオンを走査し、単一チームのみなら 0/1、無人/競合は -1 を `presentTeam` として渡す。
  - **効果（制圧チームへ）**:
    - `TeamHaste`: 制圧チーム全チャンピオンへ毎フレーム `ApplyHaste(0.25, 0.5)` 上書き（実質常時加速）。
    - `TeamVision`: `FogOfWarDirector.SetExternalSource(this, x, z, 22, ownerTeam)`、非制圧時は `RemoveExternalSource`。
    - `Shortcut`: 入口ゾーンの制圧チームを出口 `(0,1.1,-26)` へ転送（CharacterController を一旦無効化してテレポート、ユニット毎5秒CD）。
  - **見た目**: ポータル（円盤＋ビーム＋PointLight）を状態で色/脈動/表示変更。出現・制圧で `GameHudController.AnnounceSpecial`。
- **TeamId マッピング**: `TeamId{Blue=0,Red=1}` がモデルの presentTeam/ownerTeam と一致。

---

## 4. オーバークロック（task19）

- **純ロジック**: `OverclockModel`（`Enigma.Ability`）。`Evaluate(held, hp, maxHp, shield)`→`OverclockResult{Charge01, AmpFactor, HpCost, ShieldCost, CanCast}`。既定 maxCharge1.2s / maxAmp1.8 / maxCostFraction0.25 / minHpAfter1。
- **結線**: `SkillCaster` に組込（長押し型 Directional/GroundAoe のみ対象、オプトイン）。
  - アーム開始（ShowIndicator）時に `LeftShift` 併用ならチャージ開始時刻を記録。
  - キー離し（Cast）時に `EvaluateOverclock(slot)`→増幅率(>1)とコストを算出。
  - `TryCast(slot, amp, hpCost, shieldCost)`：CD 消費成功後に `HealthModel.TakeDamage(hpCost+shieldCost)`（シールド→HPの順に消費）、`FireSkill` の `DamageScale × amp` に増幅。
  - 非長押し / Shift未併用 / `CanCast=false`（HP不足）は通常発動（amp=1, コスト0）。

---

## テスト
- EditMode 追加: `SkillProgressionTests.GrantAllRanks`、`DeathRecapSourceNameTests`、`RelicCatalogTests`、`StatusEffectHasteTests`。
- 実機ロジック検証（play/editor execute_script）: レリック集約・適用（MaxHP/シールド/CDR）、ヘイスト乗算、オーバークロック増幅/コスト、リフト自動生成。

## 残・要プレイ検証
- 次元リフトの制圧効果（転送/視界/加速）の手触り。
- レリック `NeutralDamage`、オーバークロックの HUD チャージ表示。
- VFX item2（ヒットストップ/シェイク）。
