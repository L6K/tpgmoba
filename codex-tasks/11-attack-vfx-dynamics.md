# Codex タスク 11: 攻撃VFXの演出ロジック層（派手さ・キャラ個性）

> Enigma(`D:\Document\smite\smite`、ブランチ develop)。AGENTS.md 準拠。文脈なし・この仕様のみ。
> 「攻撃エフェクトをド派手に」＋「キャラ個性に合わせる」を支える**純ロジック層**。実際のシェーダー/
> パーティクル/VFX Graph・マテリアル結線・見た目検証は Claude 側（Unityエディタ作業）。本タスクは
> エディタ非依存の決定論ロジック＋EditModeテストのみ。

## 対象ファイル(4つ・新規)
1. `D:\Document\smite\smite\Assets\_Project\Scripts\Vfx\AttackVfxProfile.cs`
2. `D:\Document\smite\smite\Assets\_Project\Scripts\Vfx\VfxDynamicsModels.cs`
3. `D:\Document\smite\smite\Assets\_Project\Tests\EditMode\AttackVfxProfileTests.cs`
4. `D:\Document\smite\smite\Assets\_Project\Tests\EditMode\VfxDynamicsModelsTests.cs`

- `.meta` 作らない。既存ファイル/asmdef 変更しない。**UnityEngine 不使用**（`System` / `System.Collections.Generic` のみ。`Mathf`不可・`System.Math`可）。
- 名前空間 `Enigma.Vfx`。テストは `Enigma.Tests`。private は `_camelCase`、public は PascalCase。
- 全て決定論（時間/乱数の内部依存なし。`now`/`dt` は引数で受ける）。浮動小数比較は許容誤差 `1e-4f`。

---

## ファイル1: AttackVfxProfile.cs

```csharp
public readonly struct VfxColor
{
    public readonly float R, G, B; // 0..1（範囲外はコンストラクタで Clamp01）
    public VfxColor(float r, float g, float b);
    public static VfxColor Lerp(VfxColor a, VfxColor b, float t); // t は 0..1 にクランプ
}

public enum ChampionVfx { Zeph, Garon, Veil, Rin, Nova, Thorne }

// 1キャラの攻撃見た目パラメータ（Claude 側が VFX/シェーダーに流し込む素材）。
public readonly struct AttackVfxProfile
{
    public readonly ChampionVfx Id;
    public readonly VfxColor Primary;        // 芯/主色
    public readonly VfxColor Secondary;      // グロー/縁/トレイル副色
    public readonly float BeamWidthStart;    // ビーム発生時の太さ(m)
    public readonly float BeamWidthEnd;      // 着弾直前の太さ(m)
    public readonly float TrailLingerSeconds;// 残像の残り時間
    public readonly float ImpactScale;       // 着弾バーストの基準スケール
    public readonly float EmissionIntensity; // 発光強度(HDR係数)
    public AttackVfxProfile(ChampionVfx id, VfxColor primary, VfxColor secondary,
        float beamWidthStart, float beamWidthEnd, float trailLingerSeconds,
        float impactScale, float emissionIntensity);
}

public static class AttackVfxProfiles
{
    // 全6キャラの確定プロファイルを返す（下表の値）。未知 Id は Zeph を返す。
    public static AttackVfxProfile For(ChampionVfx id);
    // 文字列キー("zeph"等、大小無視)→ ChampionVfx。未知は Zeph。
    public static ChampionVfx Parse(string key);
}
```

### 確定プロファイル値（キャラ個性）
| Id | Primary(RGB) | Secondary(RGB) | WidthStart | WidthEnd | Linger | Impact | Emission |
|---|---|---|---|---|---|---|---|
| Zeph (SFネオン) | 0.10,0.90,1.00 | 0.90,0.20,1.00 | 0.25 | 0.60 | 0.35 | 1.0 | 3.5 |
| Garon (鋼鉄+橙) | 0.70,0.75,0.85 | 1.00,0.50,0.10 | 0.40 | 0.90 | 0.20 | 1.4 | 2.0 |
| Veil (影/紫) | 0.55,0.20,0.95 | 0.30,0.00,0.40 | 0.18 | 0.45 | 0.45 | 3.0 | 3.0 |
| Rin (金/シアン) | 1.00,0.85,0.30 | 0.40,0.90,1.00 | 0.12 | 0.30 | 0.25 | 0.8 | 2.6 |
| Nova (電撃) | 0.50,0.95,1.00 | 0.95,0.98,1.00 | 0.30 | 0.55 | 0.30 | 1.1 | 4.0 |
| Thorne (紅蓮/爪)| 1.00,0.15,0.10 | 1.00,0.55,0.20 | 0.35 | 0.80 | 0.22 | 1.3 | 3.2 |

---

## ファイル2: VfxDynamicsModels.cs

### VfxEscalationModel（コンボで派手さが段階上昇）
```csharp
public sealed class VfxEscalationModel
{
    // maxTier: 最大段階。comboWindowSeconds: この間隔内の連続ヒットでコンボ継続。hitsPerTier: 何ヒットで1段上がるか。
    public VfxEscalationModel(int maxTier = 3, float comboWindowSeconds = 2.0f, int hitsPerTier = 2);
    public int   ComboCount { get; }   // 現在の連続ヒット数
    public int   CurrentTier { get; }  // 0..maxTier
    // ヒット登録。now-lastHit <= window ならコンボ継続(ComboCount++)、超過ならリセット(=1)。
    // tier = Clamp((ComboCount-1)/hitsPerTier, 0, maxTier)。CurrentTier を返す。
    public int   RegisterHit(float now);
    // 段階に応じたスケール/強度倍率。1 + 0.25*tier（tier は 0..maxTier）。
    public float Multiplier(int tier);
    public void  Reset(); // ComboCount=0, CurrentTier=0, lastHit 無効化
}
```
- ctor: `maxTier` は 1 未満を 1 に、`comboWindowSeconds` 負を 0 に、`hitsPerTier` は 1 未満を 1 にクランプ。
- 初回 `RegisterHit` は ComboCount=1, tier=0。

### HitStopModel（被弾の手応え＝一瞬の硬直、純静的）
```csharp
public static class HitStopModel
{
    // ダメージ割合(damage/maxHp)で硬直フレーム(@60fps)を決める。
    // baseFrames=2 + 12*Clamp01(damage/maxHp)、クリットは *1.5、最終を [0,8] フレームにクランプ。
    // maxHp<=0 は割合0扱い。
    public static float FramesAt60(float damage, float maxHp, bool isCrit);
    // 上記フレームを秒へ（frames/60）。
    public static float Seconds(float damage, float maxHp, bool isCrit);
}
```

### ScreenShakeTraumaModel（trauma^2 方式の画面シェイク）
```csharp
public sealed class ScreenShakeTraumaModel
{
    public ScreenShakeTraumaModel(float maxAmplitude, float decayPerSecond);
    public float Trauma { get; }              // 0..1
    public void  AddTrauma(float amount);      // Trauma = Clamp01(Trauma + amount)
    public void  Tick(float dt);               // Trauma = Max(0, Trauma - decayPerSecond*dt)
    public float Amplitude => maxAmplitude * Trauma * Trauma; // 体感が自然な二乗カーブ
}
```
- ctor: `maxAmplitude`/`decayPerSecond` 負を 0 にクランプ。`AddTrauma` 負入力は加算前に 0 扱い（Trauma は減らない）。`Tick` 負 dt は無視。

### BeamEnvelope（ビーム生存時間 t∈[0,1] の太さ/不透明度の純関数）
```csharp
public static class BeamEnvelope
{
    // 太さ: t で widthStart→widthEnd へ smoothstep 補間（t は 0..1 にクランプ）。
    public static float WidthAt(float t, float widthStart, float widthEnd);
    // 不透明度: [0,0.1) 立ち上がり 0→1、[0.1,0.7) 維持 1、[0.7,1] フェード 1→0。返り値 0..1。
    public static float AlphaAt(float t);
}
```
- smoothstep: `s=Clamp01(t); s*s*(3-2*s)` を補間係数に使う。

---

## テスト要件（最低18）
### AttackVfxProfileTests
1. `VfxColor` 範囲外入力が Clamp01 される（-0.5→0, 1.5→1）
2. `VfxColor.Lerp` 中点・端（t=0/1/0.5、t範囲外クランプ）
3. `AttackVfxProfiles.For(Zeph)` が表の Primary/Secondary/各値と一致
4. `For` を全6キャラで呼び、Id が一致し WidthEnd>WidthStart（全キャラ）
5. `For` 未知（キャストした範囲外）→ Zeph
6. `Parse("GARON")`/`"garon"`→Garon、`"zzz"`→Zeph

### VfxDynamicsModelsTests
7. 初回 RegisterHit → ComboCount=1, tier=0
8. window 内連続で ComboCount 増加、hitsPerTier ごとに tier 上昇
9. window 超過で ComboCount=1 にリセット
10. tier は maxTier で頭打ち
11. `Multiplier(0)=1`, `Multiplier(2)=1.5`
12. ctor クランプ（maxTier=0→1, window=-1→0, hitsPerTier=0→1）
13. `HitStopModel.FramesAt60`: damage=0→2、damage=maxHp→14 を上限8でクランプ→8
14. クリット倍率（同条件で非クリットより大、ただし8上限）
15. `Seconds` = FramesAt60/60
16. ScreenShake: AddTrauma→Amplitude=max*tr^2、Tick で減衰、0未満にならない、Trauma>1にならない
17. ScreenShake: AddTrauma 負入力で Trauma 不変、Tick 負dt無視
18. BeamEnvelope: WidthAt(0)=start, WidthAt(1)=end, 単調増加(start<end時)。AlphaAt(0)=0, AlphaAt(0.05)≈0.5, AlphaAt(0.4)=1, AlphaAt(1)=0

## 完了条件 / 報告
- 4ファイル作成・整合。可能なら Roslyn/dotnet でコンパイル確認（UnityEngine 非参照なので単体で通るはず）。EditMode 実機は Claude 側。
- 報告は箇条書き＋`codex-tasks\11-attack-vfx-dynamics-report.md`（変更ファイルと要点のみ。コード全文引用・巨大表は禁止）。

## 連携メモ（変更対象外・Claude 側）
- `MatchBootstrap`/`BotChampionBootstrap` がピックキャラ名を持つ → `AttackVfxProfiles.Parse(charName)` で `AttackVfxProfile` を取得し、AAビーム/スキルVFXのマテリアル色・幅・発光に流し込む。
- `VfxEscalationModel.RegisterHit` を AA 命中時に呼び、`Multiplier(CurrentTier)` をビーム太さ/着弾スケールに乗算。
- `HitStopModel.Seconds` を被弾時の一瞬の `Time.timeScale` ディップに、`ScreenShakeTraumaModel` を `OrbitCamera` の微振動に使用。
- `BeamEnvelope` は AA ビームの LineRenderer 幅カーブ/フェードに使用。
