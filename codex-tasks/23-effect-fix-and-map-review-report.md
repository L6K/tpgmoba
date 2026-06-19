# Task 23: Effect Fix Confirmation / Map Review Report

## Summary

エフェクト修正は、前回の主要指摘に対してかなり改善されています。

特に以下はコード上で修正確認済みです。

- AoEインジケーターが `SkillDefinition.Radius` に追従するようになった。
- 近接AAが projectile/beam 固定ではなく、短射程キャラでは即時斬撃VFXへ分岐するようになった。
- Botのスキル色が固定色ではなく、Playerと同じ champion profile 由来のスロット色を使うようになった。
- Ultimate時に `RotatingMagicCircleEffect` と `NeonImpactEffect` を重ねる演出が入っている。

Unity Editor状態:

- `hasCompilationErrors: false`
- Active scene: `Assets/Scenes/Sandbox.unity`

## Effect Fix Review

### Good

#### AoE indicator radius

`SkillCaster.UpdateArmedIndicator` で GroundAoe のインジケーターが `def.Radius * 2f` にスケールされるようになっています。

確認箇所:

- `Assets/_Project/Scripts/Abilities/SkillCaster.cs`

評価:

- 前回の「予告円と実際の範囲が合わない」問題は解消方向です。
- `Cylinder` の直径基準をコメントで明示しているため、後続も理解しやすいです。

追加確認したい点:

- `SelfAoe` は現在アーム表示対象ではなさそうなので、Garon Rのような自己中心AoEを押した瞬間だけでなく、発動前にも範囲確認したいなら別途SelfAoe用の予告が必要です。

#### Melee auto attack

`AutoAttack` に `MeleeRangeThreshold = 7f` が追加され、短射程キャラは `StrikeMelee()` に分岐します。

確認箇所:

- `Assets/_Project/Scripts/Characters/AutoAttack.cs`

評価:

- Garon / Veil / Thorne が短距離ビームに見える問題は改善されています。
- 斬撃線、接触バースト、操作プレイヤー限定の `NeonImpactEffect` が入っていて、手応えも出やすいです。

注意点:

- 閾値 `7f` はやや広めです。将来、射程6前後の短射程キャスターや槍キャラを作ると近接扱いになります。意図通りなら問題ありませんが、ロール別に `IsMeleeAttack` をCharacterDataへ持たせる方が安全です。

#### Bot skill color

Bot側の `SkillSlotColor()` が champion profile を参照するようになっています。

確認箇所:

- `Assets/_Project/Scripts/Characters/EnemyChampionAI.cs`

評価:

- Player/Botの同キャラ色不一致は改善されています。
- 敵味方識別は足元リングやチーム色で補い、スキル本体はキャラ色に寄せる方針で良いです。

#### Ultimate visual layer

`SkillVfx.PlayUltimate()` で回転魔法陣、ネオン着弾、光柱、衝撃リングを重ねています。

確認箇所:

- `Assets/_Project/Scripts/Abilities/SkillVfx.cs`
- `Assets/_Project/Scripts/Vfx/RotatingMagicCircleEffect.cs`
- `Assets/_Project/Scripts/Vfx/NeonImpactEffect.cs`

評価:

- 「赤く光る魔法陣」「ド派手な必殺演出」の方向性は出ています。
- Sandbox上のプレビューでは床面の赤い円と縦方向の火花が確認できました。

改善余地:

- TPSカメラが低め/遠めだと、床面の魔法陣が薄く潰れて見えやすいです。
- 低い床円だけでなく、膝から腰くらいの高さに薄い回転リングや縦ルーンを足すと、視認性と派手さが上がります。
- キャラ別Ultimate差分はまだ共通演出寄りなので、次の段階でキャラ固有化すると良いです。

## Map Review: Respawn/Base Space

ユーザー要望:

> LoLのようにリスポーン地点付近には大きなスペースを取って円滑にファイトができるようにしたい

結論:

物理的なベース広場はかなり改善されています。ただし、AI/ミニオン経路がその広場を十分に使っていないため、実戦では「広いベース内で押し引きする」より「ベース口付近に詰まる」展開になりやすいです。

### Good

#### Base platform is wider

ベース中心は `±56`、床スケールは `34` で、半径約17の広場になっています。

確認箇所:

- `Assets/Editor/BuildAetherRiftMap.cs`

評価:

- プレイヤースポーン `(-64, 1.1, 0)` と Blue Titan `(-48, 7, 0)` が同じ広場内に収まります。
- 「泉奥 -> ネクサス/タイタン -> レーン口」というLoL風の並びを作ろうとしている意図は正しいです。

#### Base pocket boundary is expanded

境界ポケットも `TubePocketInnerR = 17.4` / `TubePocketOuterR = 18.4` に広げられています。

評価:

- 床半径17をほぼ内包しており、場外判定も `PocketInnerRadius = 17.4` に追従しています。
- 外周壁と場外判定の整合は良いです。

#### Shop radius covers much of the base

ショップ中心は `(-56, 0, 0)`、半径14です。

評価:

- Blue側では広場中心にいれば購入できます。
- スポーン地点 `(-64,0)` からもショップ範囲内です。

### Problems

#### 1. Bot spawn points are too far forward

Botのスポーン/リスポーン位置が `±44` 付近です。

例:

- Red top: `(44, 1.1, 9)`
- Red bot: `(44, 1.1, -9)`
- Red jungle: `(44, 1.1, 0)`
- Blue top: `(-44, 1.1, 9)`
- Blue bot: `(-44, 1.1, -9)`

問題:

- Titanは `±48` なので、BotがTitanの手前/横あたりに出ます。
- LoL的な「泉奥から出て、ネクサス前を通って、広場からレーンへ出る」動きになりません。
- 広いリスポーン広場があるのに、AI戦ではその広場を使いにくいです。

提案:

- Bot respawnを泉奥へ寄せる。
- 例:
  - Blue team bot respawn: `(-64, 1.1, 0)` 付近
  - Red team bot respawn: `(64, 1.1, 0)` 付近
- その上で、AI waypointの最初に以下を入れる。
  - 泉奥
  - Titan前
  - Top/Bot/Jungle分岐点
  - レーン開口

#### 2. Minion path ends near the base mouth, not inside the base

ミニオン経路は `±45.5, ±8` を終端にしています。

問題:

- ミニオン圧がベース内部まで入らないため、ベース内ファイトが起きにくいです。
- 戦闘がベース口に偏ります。

提案:

- ミニオン終端は今のままでも、チャンピオンAIだけはベース内部に入る防衛/攻撃ウェイポイントを持たせる。
- もし「本陣攻め」をもっとLoLに寄せるなら、終盤またはTitan露出後だけミニオンがTitan前まで進む経路に切り替える。

#### 3. Base entrance can still funnel combat

`JungleLaneWalls` が内周 `38.5-39.8` にあり、ベース正面周辺の移動をやや絞っています。

問題:

- ベース外から内側へ入る導線が扇形に広がりにくいです。
- 複数キャラが集まると、広場の入口付近で詰まりやすい可能性があります。

提案:

- ベース正面 `0°/180°` 付近の `JungleLaneWalls` をさらに切り欠く。
- ベース口からレーンリングへ向かう扇形エリアを明示的に無障害にする。
- 目安として、入口幅は最低でもキャラ4-5体が横並びできる幅を確保したいです。

#### 4. Fountain radius is smaller than the expanded base

`FountainRegen` は半径10です。ベース床は半径17です。

問題:

- 広場全体が泉ではないため、ベース内で戦える余地はあります。
- これは良い方向でもありますが、プレイヤーが「どこまで泉回復か」を見た目で理解しづらい可能性があります。

提案:

- 泉回復範囲を床の内側に別リングで表示する。
- リスポーン保護/ショップ範囲/泉回復範囲を別々の視覚リングにすると、LoLの泉・ショップ・ネクサス前広場の役割が伝わりやすいです。

## Recommended Map Changes

優先度順です。

1. Bot champion respawnを `±64` の泉奥に寄せる。
2. Bot waypointに「泉奥 -> Titan前 -> レーン分岐 -> レーン口」を追加する。
3. ベース正面の `JungleLaneWalls` を広めに切り欠き、入口ファネルを緩和する。
4. 泉回復範囲を視覚リングで表示する。
5. Titan露出後、ミニオンまたはAIがTitan前まで押し込む経路を追加する。
6. Red側ショップ/泉の考え方もBlue側と対称に整理する。

## Suggested Claude Task

Claudeには以下の順で依頼すると安全です。

1. `BuildAetherRiftMap.cs` のBot respawn位置を泉奥へ移す。
2. `BuildTopLaneWaypoints` / `BuildBotLaneWaypoints` またはBot専用waypointにベース内中継点を追加する。
3. `JungleLaneWalls` のベース正面切り欠きを広げる。
4. ベース内に泉範囲リングを追加する。
5. マップ再生成後、BotがTitanや壁に詰まらずレーンへ出るか確認する。

## Acceptance Checklist

- PlayerとBotがどちらも泉奥から自然に出撃する。
- ベース内で3v3程度が横に広がって戦える。
- Titan前に十分な回り込みスペースがある。
- ベース入口でBotやミニオンが壁に引っかからない。
- 泉回復範囲、ショップ範囲、Titan位置が見た目で区別できる。
- Ultimate/VFXをベース内で撃っても視界が破綻せず、AoE範囲が読み取れる。

