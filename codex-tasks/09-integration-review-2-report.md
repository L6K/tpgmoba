# Codex Task 09 静的レビュー報告

対象: 05以降の結合コード（中央オブジェクト / Botマクロ / FoW / HUD / スキル / ジャンプ / VFX / マップ生成）

## 結論

- コード修正は行っていません。
- High 1 / Medium 3 / Low 3 を検出しました。
- 特に FoW の Collider 無効化は、コメント上の前提と Unity の型階層が食い違っており、優先修正対象です。

## Findings

### High: FoW が CharacterController まで無効化し、隠れた敵の移動を止める可能性

- ファイル: `Assets/_Project/Scripts/Vision/FogOfWarDirector.cs:150`, `Assets/_Project/Scripts/Vision/FogOfWarDirector.cs:179`
- 症状: 不可視化対象の `Colliders` に `CharacterController` が混入し、`SetVisible(false)` で移動用 CharacterController まで無効化される可能性があります。
- 再現条件: CharacterController を持つ敵チャンピオン/ミニオンが FoW で不可視になる。`GetComponentsInChildren<Collider>(true)` は `CharacterController` も拾い、`fog.Colliders[i].enabled = false` が走ります。
- 修正案: キャッシュ時に `CharacterController` を除外してください。例: `GetComponentsInChildren<Collider>(true).Where(c => c is not CharacterController)` 相当。ターゲット用 CapsuleCollider だけを切る設計に分けるのが安全です。

### Medium: QuickWithIndicator のキーリリース経路が CanAct を再確認しない

- ファイル: `Assets/_Project/Scripts/Abilities/SkillCaster.cs:210`
- 症状: スキルをアームした後に Stun などで `CanAct == false` になっても、キーを離すと `TryCast` が呼ばれます。
- 再現条件: QuickWithIndicator で Q/E/R を押しっぱなしにしてインジケーター表示中、敵のCCで行動不能になった後にキーを離す。
- 修正案: `wasReleasedThisFrame` の発動経路にも `canAct` を入れるか、`TryCast` 冒頭で `_statusEffects.CanAct` を確認してください。発動不可時はアーム解除とインジケーター非表示も合わせると入力状態が残りません。

### Medium: Y-up の木FBXで幹 Collider が横倒しになる可能性

- ファイル: `Assets/Editor/BuildAetherRiftMap.cs:3296`, `Assets/Editor/BuildAetherRiftMap.cs:3331`
- 症状: 既にY軸が縦の木アセットでも、幹用 `CapsuleCollider` は常に `direction = 2` でZ軸方向に作られます。Y-upアセットではZ軸が水平なので、透明な横壁が再発します。
- 再現条件: `rawBounds` の最長軸がYで、直立補正が入らない木FBXが `PlaceOneTree` に渡される。
- 修正案: 直立補正の有無または補正後の縦軸を保持し、補正済みZ-upモデルだけ `direction = 2` を使う。補正なしY-upでは `direction = 1` とY方向centerにしてください。

### Medium / 要仕様判断: 進行中ダッシュは Root/Stun で止まらない

- ファイル: `Assets/_Project/Scripts/Characters/PlayerController.cs:109`, `Assets/_Project/Scripts/Characters/EnemyChampionAI.cs:207`
- 症状: ダッシュ開始時の `CanMove` は見ていますが、ダッシュ中は通常移動ゲートを通らず、CCを受けても水平ダッシュが完了します。
- 再現条件: ダッシュスキル発動直後、Projectile / TelegraphCircle の Root/Stun を受ける。
- 修正案: ダッシュ中ブロックでも `CanMove` を確認し、falseなら水平移動を止めるか `_dashTimeRemaining = 0` にしてください。Root/Stunが「開始禁止のみ」でよい設計なら仕様として明文化で十分です。

### Low: 3回目以降の中央報酬 Shield が ObjectiveBuffModel に記録されない

- ファイル: `Assets/_Project/Scripts/GameModes/CentralObjectiveDirector.cs:193`, `Assets/_Project/Scripts/UI/GameHudController.cs:701`
- 症状: 実シールドは `GrantTeamShield` で付与されますが、`ObjectiveBuffType.Shield` は `ObjectiveBuffModel` に `Grant` されません。HUDの `UpdateBuff` は `GetActiveTypes` ベースのため、「シールド」強化が表示されません。
- 再現条件: 同じチームが中央オブジェクトを3回以上撃破する。
- 修正案: シールドをHUD表示したいなら `buffs.Grant(team, ObjectiveBuffType.Shield, amount, dur, now)` を追加してください。即時付与で表示不要なら、`ObjectiveBuffType.Shield` とHUDラベルの期待値を整理してください。

### Low: HUD再有効化時に過去の中央制圧アナウンスが再生される可能性

- ファイル: `Assets/_Project/Scripts/UI/GameHudController.cs:42`, `Assets/_Project/Scripts/UI/GameHudController.cs:343`
- 症状: `_lastCaptureCount` の初期値が0のため、HUDが途中から有効化/再生成されると既存の `CaptureCount` を新規増分として扱い、過去の制圧アナウンスを表示する可能性があります。
- 再現条件: 中央制圧後にHUDを再Enable、またはHUDを再生成する。
- 修正案: `OnEnable` 時点で `CentralObjectiveDirector.Instance?.CaptureCount` を初期同期し、以後の増分だけ通知してください。

### Low / 要UI仕様判断: HP満タン時のシールド帯が見えない

- ファイル: `Assets/_Project/Scripts/UI/GameHudController.cs:455`, `Assets/_Project/Scripts/UI/GameHudController.cs:457`
- 症状: `hpFrac == 1` のとき `shieldW = Mathf.Min(shieldFrac, 1f - hpFrac)` が0になり、満HPで受けたシールドがHPバー上では見えません。
- 再現条件: 満HPのプレイヤーに中央報酬シールドまたはスキルシールドを付与する。
- 修正案: 満HP時はHP上に半透明オーバーレイで表示する、または別アイコン/バフ表示でシールドを可視化してください。バー幅を100%に制限する現在の設計自体は破綻していません。

## OK

- `CentralObjectiveDirector` の boss `Died` 購読解除は `OnDestroy` で対称です。`Assets/_Project/Scripts/GameModes/CentralObjectiveDirector.cs:78`
- 中央ボスの子Collider/Renderer切替は全子要素を対象にしており、非表示中のAoE命中対策として妥当です。`Assets/_Project/Scripts/GameModes/CentralObjectiveDirector.cs:91`, `:148`
- `DamageUtility` は Damage バフ参照を一点化し、null/Neutral時は安全に素通しします。`Assets/_Project/Scripts/Combat/DamageUtility.cs:12`
- `MinionAI` の MinionPower と `TowerAttack` の TowerWeaken はチーム基準が妥当です。TowerWeaken は `Mathf.Clamp01` で過剰値も安全側です。`Assets/_Project/Scripts/Minions/MinionAI.cs:212`, `Assets/_Project/Scripts/Objectives/TowerAttack.cs:187`
- Projectile / TelegraphCircle は味方判定をダメージ・CC適用前に行っており、味方へCCを入れません。`Assets/_Project/Scripts/Characters/Projectile.cs:61`, `Assets/_Project/Scripts/Combat/TelegraphCircle.cs:176`
- VFX生成プリミティブはColliderを削除しており、演出が物理・戦闘へ干渉しにくい作りです。`Assets/_Project/Scripts/Abilities/SkillVfx.cs:27`
- Player初期スポーンは `(-52, 1.1, 0)` に更新されています。`Assets/Editor/BuildAetherRiftMap.cs:598`

## 検証メモ

- 静的レビューのみ。コード変更なし。
- EditMode全403件緑、プレイモード確認済みという前提は尊重し、実機挙動と矛盾する可能性があるものは「要仕様判断」「可能性」として記載しました。
