# 21: GimmickPhysicsModel（環境ギミックの物理ロジック）

> 「環境ギミック」(ジャンプパッド/重力井戸/レーザーゲート)の**運動の数式**を純関数で持つ。
> **純 C#・Unity 非依存・EditMode テストのみ**。実際の当たり判定・移動適用・VFX は Claude 側が結線する。
> ジャンプパッド=弾道で目標地点へ打ち上げ、重力井戸=中心への引力加速度、ゲート=減速倍率(単純)。

## 対象ファイル（新規）
- 実装: `Assets/_Project/Scripts/Map/GimmickPhysicsModel.cs`
- テスト: `Assets/_Project/Tests/EditMode/GimmickPhysicsModelTests.cs`

## 名前空間・規約
- `namespace Enigma.Map`
- 標準 C# 規約・**UnityEngine 参照禁止**（完全 plain C#。Vector 型も使わず個別 float で受け渡す）。

## 確定仕様（曖昧さゼロ）

### struct `LaunchVelocity`（readonly struct, 打ち上げ初速）
- `float Vx`, `float Vy`, `float Vz`
- `float TravelSeconds` … 着地までの総時間

### static class `GimmickPhysicsModel`

#### `static LaunchVelocity LaunchToTarget(float fromX, float fromY, float fromZ, float toX, float toY, float toZ, float gravity, float arcHeight)`
- `from`(発射点) から `to`(着地点) へ、頂点が `max(fromY, toY) + arcHeight` になる放物線で打ち上げる初速を求める。
- `gravity`(>0、下向き加速度の大きさ) で:
  - `peakY = max(fromY, toY) + arcHeight`
  - 上昇初速 `Vy = sqrt(2 * gravity * (peakY - fromY))`
  - 上昇時間 `tUp = Vy / gravity`、下降時間 `tDown = sqrt(2 * (peakY - toY) / gravity)`、`TravelSeconds = tUp + tDown`
  - 水平初速 `Vx = (toX - fromX) / TravelSeconds`、`Vz = (toZ - fromZ) / TravelSeconds`
- 不正(`gravity <= 0` または `arcHeight <= 0`)時は `arcHeight` を 1、`gravity` を 9.8 にフォールバック。
- `TravelSeconds` が 0 になり得ない前提（arcHeight>0 なので tUp>0）。

#### `static void GravityWellAccel(float unitX, float unitZ, float centerX, float centerZ, float radius, float strength, out float ax, out float az)`
- 中心 `(centerX,centerZ)` への水平引力加速度を返す（XZ平面）。
- `d = sqrt((centerX-unitX)^2 + (centerZ-unitZ)^2)`。`d >= radius` または `d < 1e-4` のとき `ax=az=0`。
- それ以外: 方向 `(centerX-unitX, centerZ-unitZ)/d` に `magnitude = strength * (1 - d/radius)`（中心ほど強い、縁で0）を掛けた `(ax, az)`。

#### `static float GateSlowMultiplier(bool inside, float slowStrength)`
- `inside` のとき `Clamp01(1 - slowStrength)`（移動速度倍率）、そうでなければ 1。`slowStrength` は 0..1 にクランプ。

### 補足
- sqrt は `System.Math.Sqrt`（double）で計算し float へキャストしてよい。Clamp/Clamp01 は自前実装。
- すべて決定的（乱数なし）。

## テスト要件（NUnit、許容誤差 1e-3）
1. `LaunchToTarget`: 水平のみ(from=(0,0,0)→to=(10,0,0), g=20, arc=5)で `Vy=sqrt(2*20*5)=√200`、`Vx=10/TravelSeconds`、`Vz=0`。打ち上げを `TravelSeconds` 後に積分すると到達点が `to` に一致(誤差内)。
2. 高低差あり(toY>fromY)でも頂点が `max+arcHeight`、着地で `to.y` に戻る。
3. 不正入力(g<=0 / arc<=0)でフォールバック値が使われる。
4. `GravityWellAccel`: 半径外で0、中心方向を向く、縁(d≈radius)で magnitude≈0、中心寄り(d=radius/2)で magnitude=strength*0.5。d<1e-4 で0。
5. `GateSlowMultiplier`: inside=true で 1-slow(クランプ)、false で 1、slow>1 は 0 にクランプ。

## 完了条件
- 2ファイル作成。`Enigma.Map`・Unity 非依存。EditMode テスト緑（件数を report）。
- 報告 `codex-tasks/21-gimmick-physics-model-report.md`。範囲外の既存改変なし（所見は report へ）。

## 補足（Claude 側・対象外）
- 結線: ジャンプパッド=トリガー進入で `LaunchToTarget`→ CharacterController に初速付与し放物線移動(着地まで操作制限)。重力井戸=毎フレーム圏内ユニットに `GravityWellAccel` を加算。レーザーゲート=圏内で `GateSlowMultiplier` を移動速度へ。配置・見た目(渦/パッド/レーザー)はシーン側。
- 課金: 従量課金・画像生成は使用禁止（純コードのみ）。
