# 5.5 画像生成タスク 12: 攻撃VFX & マップネオン用テクスチャ（★1枚ずつ個別生成）

> Enigma の「派手な攻撃エフェクト＋マップのネオン化」用テクスチャ。Unity URP / トゥーン。
> 出力先: `Assets/_Project/VFX/Textures/`（Claude が import 設定・マテリアル結線・最終調整）。
>
> ★重要: **1プロンプト＝1テクスチャで個別生成**する（コンタクトシート/まとめ出力は解像度・背景が破綻するため不可）。
> 各画像は **ファイル名どおり**に保存。下の Prompt をそのまま 5.5 に渡してよい。

## 全画像共通ルール（必ず守る）
- **文字・ラベル・枠線・グリッド線を一切描かない**（フリップブック2点を除く）。被写体は1つだけ・中央配置。
- 色は **白〜グレースケール基調**（色付けは Claude がマテリアル tint で各キャラ色を乗せる）。
- **加算(Additive)合成前提**:
  - 単体エフェクト系（グロー/スパーク/ビーム/スラッシュ/コア/トレイル）→ **完全な透明背景 PNG**（白背景は厳禁。透過が無理なら**純黒背景**）。
  - マスク/ノイズ/パネル系 → **純黒背景**でよい（黒=加算で無寄与）。
- 解像度は指定の 2 のべき乗。`seamless` 指定はタイル継ぎ目が出ないように。

---

## A. 単体エフェクト（透明 or 黒背景・白発光）

### 1. `glow_dot.png` — 512×512・透明背景
Prompt: "Soft radial glow orb, pure white core fading smoothly to fully transparent edges, centered, no hard edge, no text, transparent background, additive particle texture, 512x512."

### 2. `spark_streak.png` — 512×512・透明背景
Prompt: "Explosive spark burst, thin sharp white light streaks radiating from a bright center point, energetic, no text, transparent background, additive VFX texture, 512x512."

### 3. `beam_core_gradient.png` — 256×1024（縦長）・透明背景
Prompt: "Vertical energy beam cross-section, bright white core line down the center fading to transparent on both left and right sides, smooth gradient, no text, transparent background, 256x1024."

### 4. `impact_burst_flipbook.png` — 1024×1024・4×4=16コマ・黒背景【フリップブックのみグリッド可】
Prompt: "4x4 sprite sheet flipbook, 16 frames of a white energy impact burst animation: frame 1 a tiny bright flash, expanding outward into a spiky shockwave, dissipating to faint wisps by frame 16, grayscale, pure black background, evenly spaced cells, no text labels."

### 5. `ring_shock_flipbook.png` — 1024×1024・4×4=16コマ・黒背景【グリッド可】
Prompt: "4x4 sprite sheet flipbook, 16 frames of a flat expanding shockwave ring on the ground seen from above: starts as a small bright white ring, grows larger and thinner and fades out, grayscale, pure black background, evenly spaced cells, no text."

### 6. `slash_arc.png` — 1024×512・透明背景
Prompt: "Single crescent sword slash trail, white energy arc thick in the middle and tapering to thin sharp tips, slight motion smear, anime toon style, no text, transparent background, 1024x512."

### 7. `hit_flash_radial.png` — 512×512・透明中心/白外周
Prompt: "Radial screen-damage vignette, transparent in the center and glowing white toward the outer edges, soft circular falloff, no text, transparent background, 512x512."

---

## B. キャラ個性マスク（黒 or 透明背景）

### 8. `zeph_circuit_mask.png` — 512×512・seamless・黒背景
Prompt: "Seamless tileable sci-fi circuit board pattern, thin glowing white traces and small nodes on pure black background, neon cyber tech, no text, tile-able edges, 512x512."

### 9. `veil_smoke_wisp.png` — 512×512・黒背景
Prompt: "Wispy white smoke trail, soft ink-in-water tendrils curling, grayscale, pure black background, additive VFX, no text, 512x512."

### 10. `rune_circle_arcane.png` — 1024×1024・透明背景
Prompt: "Arcane magic circle line-art, concentric white circles with geometric runes and patterns, glowing thin lines, center mostly empty, top-down, no text outside the design, transparent background, 1024x1024."

---

## C. マップ・ネオン装飾

### 11. `neon_trim_strip.png` — 1024×128（横長）・seamless・透明背景
Prompt: "Seamless horizontal neon light strip, a bright white glowing line running through the center fading vertically, tile-able on left/right edges, no text, transparent background, 1024x128."

### 12. `hex_panel_emissive.png` — 512×512・seamless・黒背景
Prompt: "Seamless tileable hexagon tech panel, thin glowing white hexagonal grid lines on pure black background, sci-fi floor emissive mask, tile-able edges, no text, 512x512."

### 13. `energy_flow_strip.png` — 256×1024（縦長）・seamless(縦)・透明背景
Prompt: "Vertical flowing energy streaks, soft white streaks of light flowing upward, tile-able on top/bottom edges for UV scrolling, grayscale, no text, transparent background, 256x1024."

### 14. `objective_core_glow.png` — 1024×1024・透明背景
Prompt: "Intense glowing energy core, brilliant white center with soft radial flare and faint lens glow, centered, no text, transparent background, 1024x1024."

### 15. `soft_noise_tile.png` — 512×512・seamless・黒背景（不透明可）
Prompt: "Seamless tileable soft grayscale perlin/cloud noise, smooth medium-contrast, tile-able edges, no text, 512x512."

---

## 完了条件 / 報告
- 各PNGを `Assets/_Project/VFX/Textures/` にファイル名厳守で保存。難しい/不要はスキップ可、報告に明記。
- 報告: 生成ファイル名一覧＋各用途1行。`codex-tasks\12-vfx-textures-5.5-report.md`。

## 連携メモ（Claude 側）
- import: 加算用は sRGB on / Alpha is Transparency on、ノイズ/マスクは Linear。フリップブックは UV アニメ or VFX Graph。
- 色付けはマテリアル tint（`AttackVfxProfile.Primary/Secondary`）。テクスチャは白基調で受け取る。
