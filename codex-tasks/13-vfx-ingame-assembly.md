# 13: 攻撃VFX 実機組み立て手順（Claude / Unity を開いた状態で実施）

> 自走で「テクスチャ(手続き仮)・URP加算マテリアル・ヒットストップ/シェイク基盤」までを実装・検証・コミット済み
> (commit 6f174d79)。ここから先は**見た目の確認が必須**なので Unity 起動＋coplay でビジュアル検証しながら行う。
> 5.5 の個別生成テクスチャが届いたら同名で `Assets/_Project/VFX/Textures/` に上書き → 再import で差し替え完了。

## 既に用意済み（コードで利用可能）
- テクスチャ15枚: `Assets/_Project/VFX/Textures/*.png`
- 加算マテリアル: `Assets/_Project/Materials/VFX/Vfx_{Glow,Spark,Beam,Impact,Slash,Core,NeonTrim,Hex}.mat`
- ロジック(Codex, テスト済): `Enigma.Vfx.AttackVfxProfile`(6キャラ色) / `VfxEscalationModel` / `HitStopModel` / `ScreenShakeTraumaModel` / `BeamEnvelope`
- ジュース: `HitStopController`(常駐) / `AttackJuice.PlayerLandedHit`(Projectile に結線済み・操作プレイヤー限定)

## やること（優先順）
1. **ゼフのAAビームをネオン化**（最優先・ユニティちゃん=SFネオン）
   - `AaBeam.prefab` の MeshRenderer/TrailRenderer に `Vfx_Beam` を割り当て、TrailRenderer の color グラデを白→透明に。
   - 発射時に `AttackVfxProfiles.Parse(championName).Primary/Secondary` でビーム色を per-instance 着色（MaterialPropertyBlock）。
     - champion 名の取得元: 実行時のピック適用(BootstrapでCharacterDataを当てている箇所)を辿り、AutoAttack 経由で profile を解決。
   - マズル: `AutoAttack._muzzle` に `Vfx_Glow` の小Quad/パーティクルを一瞬出す。
   - 着弾: 既存 `SkillVfx.SpawnBurst` を `Vfx_Impact`(4x4フリップブック) ベースの一発に差し替え or 併用。色は profile。
   - ビーム生存中の太さ/フェードに `BeamEnvelope.WidthAt/AlphaAt` を使用（LineRenderer or trail width）。
2. **ヒットストップ/シェイクの実機チューニング**
   - `AttackJuice` の定数(HitStopMinFraction=0.12 / ShakePerDamage=0.004 / Min/Max)と `HitStopController`(FreezeScale=0.05 / Max0.12s)を実プレイで調整。
   - 重すぎたら `AttackJuice.Enabled=false` で即無効化できる。
3. **コンボ段階(派手さ)**: `VfxEscalationModel.RegisterHit` を AA 命中で回し、`Multiplier(tier)` をビーム太さ/着弾スケール/エミッション強度に乗算。
4. **画面被弾フラッシュ**: `hit_flash_radial` を HUD かフルスクリーンに重ね、`PlayerHitFeedback` 被弾時に明滅。
5. **マップ ネオン化**: 基地床/レーン縁/中央コアに `Vfx_NeonTrim`(UVスクロール)・`Vfx_Hex`(emissive)・`objective_core_glow` を適用。`energy_flow_strip` を川/ビームに。
6. **キャラ横展開**: ガロン(鋼鉄+橙)/ヴェイル(影紫)/リン(金シアン)/ノヴァ(電撃)/ソーン(紅蓮)を `AttackVfxProfile` で色替え＋固有マスク(`zeph_circuit_mask`/`veil_smoke_wisp`/`rune_circle_arcane`)を流用。

## 注意
- マテリアルは URP/Unlit 加算(One/One・ZWrite off)で生成済み。実機で発光が強すぎ/弱すぎる場合は `_BaseColor` を HDR で増減（profile.EmissionIntensity を参考）。
- 加算なのでテクスチャは白基調・背景透過/黒が前提（5.5差し替え時もこの規約を守る）。
- 従量課金ツール(Coplayの生成系/API)は使わない。
