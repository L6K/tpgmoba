# Task 22: Character Skill Balance / VFX / Motion Review

## Purpose

Claudeに依頼するためのレビュー結果です。対象はキャラ試用シーンで確認できる各キャラのスキルバランス、エフェクト、モーションです。

実装前に、まず以下の改善案を優先度順に確認してください。大きな仕様変更ではなく、試用シーンで「キャラごとの差」「読みやすさ」「強すぎる/弱すぎる」を判断しやすくすることを目的にしています。

## Review Context

- Character trial scene: `Assets/Scenes/Sandbox.unity`
- Sandbox builder: `Assets/Editor/BuildSandbox.cs`
- Sandbox controller: `Assets/_Project/Scripts/Sandbox/CharacterSandbox.cs`
- Character data: `Assets/_Project/Data/Characters/*.asset`
- Skill data: `Assets/_Project/Data/Skills/*.asset`
- Skill runtime: `Assets/_Project/Scripts/Abilities/SkillCaster.cs`
- Skill VFX: `Assets/_Project/Scripts/Abilities/SkillVfx.cs`
- Attack VFX profile: `Assets/_Project/Scripts/Vfx/AttackVfxProfile.cs`

Sandbox controls:

- `M`: character menu
- `L`: relic menu
- `Q/E/R`: skill test

## Highest Priority Findings

### 1. AoE preview radius is not matching skill radius

The AoE indicator appears to be a fixed-size circle. `SkillCaster.UpdateArmedIndicator` updates position, but the visual radius does not appear to follow each `SkillDefinition.Radius`.

Examples:

- Zeph E radius: `3.2`
- Zeph R radius: `4.5`
- Rin E radius: `3.0`
- Thorne E radius: `3.8`
- Garon R radius: `5.5`

Impact:

- Players cannot trust the telegraph.
- Balance review becomes unreliable, because actual hit area and preview feel different.

Request:

- Scale the AoE indicator from the armed skill's `Radius`.
- Confirm GroundAoe and SelfAoe both show their real hit size.
- Add a small visual difference between ordinary AoE and ultimate AoE.

### 2. Player and Bot skill colors are inconsistent

Player skill VFX uses champion profile colors, but Bot skill VFX appears to use fixed colors such as cyan, magenta, and gold.

Impact:

- The same character can look visually different depending on whether it is controlled by the player or Bot.
- It is harder to identify enemy skills by character.

Request:

- Make Bot skill VFX use the same champion VFX profile/color lookup as the player.
- Keep team readability if needed by adding a thin hostile outline, not by replacing the character identity color entirely.

### 3. Melee characters still look like they are firing beam projectiles

Garon, Veil, and Thorne are melee or short-range characters, but AutoAttack visuals are still projectile/beam-like.

Impact:

- Garon does not feel heavy.
- Veil does not feel like an assassin.
- Thorne does not feel like a bruiser/jungler.

Request:

- If attack range is short, use melee slash/impact visuals instead of beam projectile visuals.
- Keep hit confirmation strong: short arc, weapon trail, contact burst, and small camera/animation emphasis are enough.

## System-Level Balance Findings

### Skill slot naming is confusing

Runtime operation is effectively `Q/E/R`, but asset names use `Skill_*_Q/W/E`. It looks like asset `W` maps to in-game `E`, and asset `E` maps to in-game `R`.

Request:

- Either rename assets to match runtime keys or document the mapping clearly.
- If the game is intended to have only 3 skills, make that explicit.
- If 4 skills are planned, `Skills[3]` is currently empty for all characters and should be treated as future work.

### Only damage scales with rank

Damage receives rank scaling, but shield, heal, CC duration, range, radius, and cooldown do not appear to scale.

Impact:

- Support skills can be too strong early and not scale satisfyingly later.
- CC skills keep the same control value while damage increases.

Request:

- Add optional per-rank scaling for shield/heal/CC duration.
- At minimum, reduce early shield/heal values and allow them to scale later.

### Overclock can make some ultimates too explosive

Directional and GroundAoe skills can receive Overclock scaling. With rank scaling, high-damage ultimates can become very large spikes.

Examples:

- Zeph R: `130 * 1.5 * 1.8 = 351`
- Rin R: `135 * 1.5 * 1.8 = 364.5`

Request:

- Consider a lower Overclock multiplier for ultimates.
- Alternatively exclude ultimates from Overclock until the rest of the kit balance is stable.

### Bot and player validation can diverge

Bots appear to use base skill values without player-side rank/overclock behavior.

Request:

- Decide whether Bot tests should represent base skill behavior or real match behavior.
- If Sandbox is used for balance review, expose current rank/overclock state in the HUD or test controls.

## Character Balance Review

### Zeph

Role read: long-range burst mage / AoE control.

Current strengths:

- Safe poke from long range.
- E and R both provide AoE slow.
- R has strong area denial and high damage ceiling with Overclock.

Concerns:

- R may be too decisive when Overclocked, especially with radius `4.5` and slow.
- E/R overlap in function: both are AoE damage plus slow.

Suggested tuning:

- Reduce R Overclock value or make ultimates use a separate lower multiplier.
- Consider reducing R radius from `4.5` to around `4.0`, or increasing cooldown from `48` to `55`.
- Give E and R clearer identity: E as smaller control field, R as slower but more dramatic meteor.

VFX/motion request:

- Zeph should lean into arcane circles, violet/cyan runes, delayed meteor impact, and strong ground warning.
- Make the R telegraph very readable before the impact.

### Garon

Role read: tank / frontline initiator.

Current strengths:

- Highest HP.
- Q shield.
- E AoE stun.
- R large SelfAoe stun plus large shield.

Concerns:

- Too much durability and crowd control are stacked together.
- With cooldown reduction, E can become a frequent large-radius stun.
- Current visuals do not sell tank weight because the model is height-normalized and AA still looks beam-like.

Suggested tuning:

- E stun: reduce from `1.0s` to `0.7-0.8s`, or reduce radius.
- R: reduce one of radius, stun duration, or shield. Do not keep all three at high values.
- Q shield: consider `45-50` instead of `60`, or shorter duration.

VFX/motion request:

- Replace beam-like melee AA with sword/weapon slash.
- R should feel like a judgment slam: golden shield flare, ground cracks, dust ring, heavy recovery.
- Preserve heavy silhouette; consider per-character visual scale instead of normalizing everyone to the same height.

### Veil

Role read: assassin / mobile finisher.

Current strengths:

- Highest movement speed.
- Low-CD Q.
- Targeted dash damage on E.
- Targeted high-damage R.

Concerns:

- Targeted burst gives low counterplay.
- R can reach `240` damage at max rank without needing aim.
- Current targeted VFX fires a beam before dash, which weakens the fantasy of "dash assassination."

Suggested tuning:

- Add a mark/condition before R can deal full damage.
- Make R an execute-style skill or reduce base damage to around `130-140`.
- Increase E cooldown to around `8-9s` or reduce E damage from `55` to around `40-45`.

VFX/motion request:

- E/R should visually dash first or during impact, not look like a beam followed by movement.
- Add afterimages, short smoke trail, dark slash arc, and a clear target mark.
- Keep counterplay readable with a brief lock-on tell before the burst.

### Rin

Role read: marksman / long-range precision damage.

Current strengths:

- Longest AA range.
- Strong AA uptime.
- Q and R have very long range.
- R can become extremely high damage with rank plus Overclock.

Concerns:

- Q, R, and AA may all read as similar beam shots.
- R damage ceiling can feel like a long-range one-shot.

Suggested tuning:

- Reduce R Overclock interaction, or add a charge commitment such as slower movement during windup.
- Consider slightly lowering AA range or AA damage if Rin dominates neutral poke.
- Make Q a quick piercing shot and R a clearly different railgun with charge-up, recoil, and louder telegraph.

VFX/motion request:

- Q: thin fast tracer.
- E: scattered bomblets or shrapnel sparks.
- R: thick rail line, charge particles, muzzle bloom, recoil pose, delayed thunder-like hit.

### Nova

Role read: support / battle mage.

Current strengths:

- Good poke range.
- E shield `110/4s` on `10s` cooldown.
- R gives team heal `160` plus shield `110/4s` on `45s` cooldown.

Concerns:

- R appears to affect all allies without range limit. In a 5-player team, the total value is extremely high.
- E shield is very large compared with early HP values.
- Support values do not scale by rank, so early value is overloaded.

Suggested tuning:

- E shield: reduce to around `70-85`, or increase cooldown to `12-14s`.
- R: add radius, ally count limit, or line-of-sight requirement.
- R heal: reduce to around `110-130`; R shield around `70-90`.
- Add rank scaling for shield/heal so early game is not dominated by fixed values.

VFX/motion request:

- Support effects should use Nova's cyan/white identity, not generic green.
- E: star shield wrap around ally.
- R: team pulse, starfield ring, soft vertical light, clear ally confirmation.

### Thorne

Role read: bruiser / jungler / pick engager.

Current strengths:

- High HP.
- High movement speed.
- Strong AA damage.
- Long-range root on Q.
- Targeted dash ultimate with self-heal.

Concerns:

- Q root at range `14` plus high movement speed gives strong pick power.
- R combines engage, damage, and sustain.
- Current targeted VFX does not communicate hook/predator identity.

Suggested tuning:

- Q root: reduce from `1.0s` to `0.7-0.8s`, or reduce range to around `12`.
- Consider slightly lowering movement speed or AA damage.
- R heal should scale by rank or missing HP rather than being a flat early-game spike.

VFX/motion request:

- Q should read as chain/vine/hook, not a generic projectile.
- R should show leap trail, claw/chain impact, and green absorption/heal feedback.
- E should be an earth shockwave with radial dust and ground fracture.

## Motion Review

### Attack animation masking

UnityChan appears to have an upper-body attack mask, but imported character models may use `attackMask: null`, causing full-body attack override.

Impact:

- Moving attacks may cause foot sliding or sudden full-body snapping.

Request:

- Add upper-body masks or separate locomotion/attack blending for Knight, Rogue, Ranger, Cleric, and Barbarian.
- In Sandbox, test attacking while moving for each character.

### Character silhouette is too normalized

`ChampionModelSwapper` normalizes models to roughly the same height. This makes roles less readable.

Request:

- Add per-character visual scale and offset fields.
- Suggested direction:
  - Garon: taller/wider/heavier.
  - Veil: slimmer/lower stance.
  - Rin: readable ranged posture.
  - Nova: light caster silhouette.
  - Thorne: broad bruiser shape.

### Skill casts need character-specific pose language

Current skill execution appears more system-driven than character-driven.

Request:

- Directional skills: rotate character toward aim before firing.
- Ground AoE: hand/staff/weapon raise during windup.
- Targeted dash: anticipation crouch, dash, contact frame, recovery.
- Self AoE: planted stance and strong ground impact.

## VFX Style Targets

Use these as implementation direction, not strict final design.

- Zeph: violet/cyan arcane, rune circles, delayed meteor, clean magical geometry.
- Garon: gold/white, shield flare, ground cracks, heavy dust, sword arcs.
- Veil: black/purple, afterimage, smoke, target mark, slash impact.
- Rin: orange/blue, tracer rounds, railgun line, muzzle flash, recoil.
- Nova: cyan/white, stars, soft shields, team pulse, heal confirmation.
- Thorne: green, chains/vines, shockwaves, absorption, predatory leap trail.

## Suggested Implementation Order

1. Fix AoE indicator radius.
2. Unify Player/Bot champion VFX color/profile lookup.
3. Add melee AA visual branch for short-range characters.
4. Add support-value tuning for Nova.
5. Reduce targeted burst counterplay issues for Veil.
6. Reduce CC density for Garon and Thorne.
7. Differentiate ultimate VFX per character.
8. Add animation masks or blend fixes for imported models.
9. Add per-character visual scale/offset.
10. Clean up skill asset naming (`Q/E/R` vs `Q/W/E`).

## Acceptance Checklist

- Sandbox AoE preview matches actual radius for every GroundAoe and SelfAoe skill.
- Player and Bot versions of the same character use the same core skill color identity.
- Garon, Veil, and Thorne no longer look like ranged beam attackers during normal attacks.
- Nova support values do not dominate level-1 HP pools.
- Veil targeted burst has visible counterplay or a condition before full damage.
- Garon and Thorne CC windows feel strong but not chain-lock oppressive.
- Rin Q/R/AA are visually distinguishable at a glance.
- Each ultimate has a unique silhouette, not just a color-shifted shared effect.
- Moving while attacking does not visibly break imported character animation.

