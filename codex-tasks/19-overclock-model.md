# 19: OverclockModel（スキル過負荷=オーバークロックの純ロジック）

> 「オーバークロック」(キーを溜めて次の1スキルをHP/シールド消費で増幅)のロジック部分。
> **純 C#・Unity 非依存・EditMode テストのみ**。溜め時間と残リソースから「増幅率・コスト・発動可否」を返す純関数。
> 入力(溜め)・スキル増幅の実適用・VFXは Claude 側(Unity)が結線する。

## 対象ファイル（新規）
- 実装: `Assets/_Project/Scripts/Abilities/OverclockModel.cs`
- テスト: `Assets/_Project/Tests/EditMode/OverclockModelTests.cs`

## 名前空間・規約
- `namespace Enigma.Ability`（既存スキル系と同所）
- 標準 C# 規約・**UnityEngine 参照禁止**（完全 plain C#）。

## 確定仕様（曖昧さゼロ）

### struct `OverclockResult`（readonly struct）
- `float Charge01` … 溜め量 0..1（chargeHeld / maxCharge をクランプ）
- `float AmpFactor` … スキル効果(ダメージ/範囲)への乗算係数（1.0〜maxAmp）
- `float HpCost` … 発動時に支払う HP
- `float ShieldCost` … 発動時に先に支払うシールド（シールド→HP の順で消費）
- `bool CanCast` … 残リソースでコストを賄えるか（払って HP が `minHpAfter` を下回らない）

### class `OverclockModel`
コンストラクタ
`OverclockModel(float maxChargeSeconds = 1.2f, float maxAmp = 1.8f, float maxCostFraction = 0.25f, float minHpAfter = 1f)`
- 不正値(<=0)は各既定へフォールバック。`maxAmp` は 1 未満なら 1 にクランプ。

メソッド
- `OverclockResult Evaluate(float chargeHeldSeconds, float currentHp, float maxHp, float currentShield)`
  - `Charge01 = Clamp01(chargeHeldSeconds / maxChargeSeconds)`
  - `AmpFactor = 1 + (maxAmp - 1) * Charge01`
  - 総コスト = `maxHp * maxCostFraction * Charge01`（溜めるほど高い）。
    - まずシールドで支払う: `ShieldCost = Min(currentShield, 総コスト)`
    - 残りを HP で: `HpCost = 総コスト - ShieldCost`
  - `CanCast = (currentHp - HpCost) >= minHpAfter`（シールドで賄えるなら常に true）。
  - `chargeHeldSeconds <= 0` のときは Charge01=0・AmpFactor=1・コスト0・CanCast=true（=通常発動扱い）。
- `float AmpAt(float charge01)` … 0..1 の溜めに対する AmpFactor を返す純ヘルパー（テスト用）。

### 補足
- コスト/増幅は溜め(Charge01)に対し線形。最大溜めで `AmpFactor=maxAmp`・コスト=`maxHp*maxCostFraction`。
- Clamp/Clamp01 は private 自前実装（Mathf 禁止）。

## テスト要件（NUnit、許容誤差 1e-4）
1. charge=0 → AmpFactor=1・コスト0・CanCast=true。
2. 最大溜め → AmpFactor=maxAmp、総コスト=maxHp*maxCostFraction。
3. シールド優先消費: currentShield が総コスト以上なら HpCost=0・ShieldCost=総コスト・CanCast=true。
4. シールド不足: 残りが HP から引かれ、`currentHp - HpCost < minHpAfter` なら CanCast=false。
5. Charge01 クランプ: chargeHeld>maxChargeSeconds でも Charge01=1。
6. `AmpAt(0)=1`, `AmpAt(1)=maxAmp`, 中間で線形。

## 完了条件
- 2ファイル作成。`Enigma.Ability`・Unity 非依存。EditMode テスト緑（件数を report）。
- 報告 `codex-tasks/19-overclock-model-report.md`。範囲外の既存改変なし。

## 補足（Claude 側・対象外）
- 結線: スキルキー長押しで chargeHeld 計測→離した時 `Evaluate`→`CanCast` なら HP/シールド消費(既存 HealthModel のシールド消費を流用)＋ `AmpFactor` をスキルの Damage/Radius へ乗算＋過負荷VFX(発光増し/帯電)。`CanCast=false` なら通常発動。
- 課金: 従量課金・画像生成は使用禁止（純コードのみ）。
