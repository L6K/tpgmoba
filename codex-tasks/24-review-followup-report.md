# Task 24: Review Follow-up Report

対象: `b313b501` 以降のレビュー対応コミット（`78a928fe`、`cd902fdd`、`6e46e90e`、`d9b89dbd`）

## 確認済み

- Unity Editor はコンパイルエラーなし。
- AoE 指定時の床インジケーターは `SkillDef.Radius` に合わせて拡縮される。
- Self AoE は発動時の範囲リングを持つ。
- Bot のスキル色は Champion VFX profile に統一され、プレイヤー側と同じ規則になった。
- 短射程キャラクターの AA は飛翔弾ではなく即時の斬撃 VFX / ダメージに分岐した。
- 魔法陣のランタイム生成 Material は `OnDestroy` で破棄される。

## Findings

### P1: 近接 AA が Projectile prefab 未設定時に一切発動しない

`AutoAttack.Update` は近接 / 遠隔の分岐より前に `_projectilePrefab == null` を検査して return する。
近接分岐の `StrikeMelee` は Projectile を使用しないため、近接専用プレハブやテスト用構成で Projectile prefab を設定しないと AA 自体が止まる。

- 対象: `Assets/_Project/Scripts/Characters/AutoAttack.cs:85`
- 再現: `AttackRange <= 7` のキャラクターで `_muzzle` だけを設定し、`_projectilePrefab` を未設定にする。
- 修正: 共通事前条件を `target` / `_muzzle` に限定し、`_projectilePrefab` の null 検査は `FireProjectile` のみで行う。

### P1: ボットのリスポーン / 回復地点が基地前方のまま

前回のマップレビュー後、この領域に差分はない。Bot は Red / Blue とも `x = +/-44` に復帰し、同じ位置が個別 `FountainRegen` の中心になっている。Titan 前方へ直ちに復帰するため、基地奥のリスポーン広場を使う設計にならない。

- 対象: `Assets/Editor/BuildAetherRiftMap.cs:835`, `Assets/Editor/BuildAetherRiftMap.cs:2366`, `Assets/Editor/BuildAetherRiftMap.cs:2411`
- 修正: Bot の spawn / fountain / respawn をプレイヤーと同じ基地奥寄り（Blue `-64`、Red `64` 近傍）へ移す。

### P1: AI とミニオンが基地広場を経由・活用しない

Bot route はリスポーン位置の次にレーン開口へ向かい、基地内の集合・撤退・防衛地点を持たない。Minion の最終地点も敵側 `x = +/-50, z = +/-8` で、Titan へ向かう段階がない。広い基地を作っても、実際の試合でそこで押し引きが発生しにくい。

- 対象: `Assets/Editor/BuildAetherRiftMap.cs:2326`, `Assets/Editor/BuildAetherRiftMap.cs:2447`, `Assets/Editor/BuildAetherRiftMap.cs:2700`
- 修正: Champion AI に `fountain back -> Titan front -> lane split -> lane entrance` の中間経路を加える。Titan 解放後の minion route / target を Titan まで延長する。

### P2: 基地正面の入口拡幅は未対応

正面の Jungle/Lane wall arc は前回と同一で、広い基地前ファイトを受け止める扇形の無障害スペースがない。

- 対象: `Assets/Editor/BuildAetherRiftMap.cs:2972`
- 修正: Blue 側は 180 度、Red 側は 0 度付近の wall arc を広く切り、Titan 前から中央へ向けた扇形スペースを確保する。

## テストギャップ

- 近接専用構成（Projectile prefab なし）の AA 発動テスト。
- マップ再生成後の Bot の死亡復帰からレーン復帰。
- 基地内での複数 Champion の交戦時に Titan / 壁へ詰まらないこと。
- Minion が Titan を攻撃できる段階まで進むこと。
