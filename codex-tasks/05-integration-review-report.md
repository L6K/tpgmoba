# Codex 結果報告: 結合コード静的レビュー

対象: Claude 連携用  
方針: コード修正なし。MonoBehaviour結合層のみ静的レビュー。

## 指摘

### High

1. `Assets/_Project/Scripts/Abilities/SkillCaster.cs:275` / High  
   症状: クールダウンを消費してから `CastTargeted` 側で target null、射程外、味方対象を弾いている。無効な対象指定でもCDとWindupだけ消費され、スキル効果が出ない。  
   想定される再現条件: Targeted 攻撃スキルを、現在ターゲットなし・射程外・味方ターゲットの状態で発動する。`TryCast` が `_cooldowns[slot].TryConsume` に成功した後、`CastTargeted` が `return` する。  
   修正案: Targeted は `TryConsume` 前に対象存在/射程/TeamRulesを検証する。もしくは `FireSkill` / 各Castを bool 戻り値にして、成功時だけCD消費・モーション開始する。

2. `Assets/_Project/Scripts/GameModes/CentralObjectiveDirector.cs:66` / High  
   症状: 中央ボス非表示時に root の `Collider` 1個だけを無効化している。子ColliderがあるPrefabでは、Dormant/Warning中でも当たり判定が残り、AoEの `GetComponentInParent<IDamageable>()` 経由で隠れたボスにダメージが通る可能性がある。要プレイ確認。  
   想定される再現条件: NeutralBossController配下に子Colliderを持つ構成で、ボス非表示中に `TelegraphCircle` の範囲攻撃が重なる。  
   修正案: `Collider[] _bossColliders = go.GetComponentsInChildren<Collider>(true)` をキャッシュし、Hide/Spawnで全Colliderを切り替える。もしくはボス本体を専用inactive rootで管理する。

### Medium

3. `Assets/_Project/Scripts/Combat/HealthComponent.cs:30` / Medium  
   症状: `Damaged` イベントが実HP減少量ではなく入力 `amount` を通知している。シールドが全吸収してHPが変わらない場合でも、ダメージポップアップ/被弾演出側にはフルダメージとして通知される。  
   想定される再現条件: シールド40を持つ対象に30ダメージを与える。`HealthModel` 上はHP不変だが、`Damaged(30)` が発火する。  
   修正案: `beforeHp - afterHp` を計算して、実HP減少がある時だけ `Damaged` を発火する。シールド吸収表示が必要なら別イベントを追加する。

4. `Assets/_Project/Scripts/Characters/PlayerController.cs:42` / Medium  
   症状: `RequestDash` が `StatusEffectController.CanMove` を見ていない。Root中は `CanAct=true` のためスキル発動は可能で、DashDistance付きスキルなら移動不能を無視してダッシュできる。Bot側も同様に `EnemyChampionAI.cs:513` の `RequestDash` がCCを見ない。  
   想定される再現条件: Root中のプレイヤー/ボットが DashDistance 付き Targeted スキルを使う。通常移動は止まるが、ダッシュ分は移動する。  
   修正案: Rootをダッシュ不可にするなら `RequestDash` 冒頭で `if (_statusEffects != null && !_statusEffects.CanMove) return;` を入れる。Root中ダッシュを仕様にするなら明文化する。

5. `Assets/_Project/Scripts/Characters/EnemyChampionAI.cs:453` / Medium  
   症状: 中立狩り用の `MoveDirectlyToward` が `CanMove` と `MoveSpeedMultiplier` を見ていない。通常レーン移動は `ApplyMovement` でCC/Slowを反映するが、中立接近時だけRoot/Slowを無視して移動する。  
   想定される再現条件: ジャングラーBotが中立キャンプへ接近中にRoot/Slowを受ける。Root中でも接近し、Slowでも速度が落ちない。  
   修正案: `MoveDirectlyToward` でも `ApplyMovement` と同じく `CanMove` で水平移動をゼロにし、速度に `MoveSpeedMultiplier` を掛ける。

6. `Assets/_Project/Scripts/Vision/FogOfWarDirector.cs:82` / Medium  
   症状: プレイヤーチーム未解決でも `Tick()` が続行される。`ResolvePlayerTeam` がまだ失敗するタイミングでTeamTag持ちが存在すると、味方判定が成立せず、CharacterController持ちのユニットが敵/中立対象として扱われて隠される可能性がある。  
   想定される再現条件: `FogOfWarDirector` が先に起動し、PlayerController検索が一時的に失敗するが、TeamTag持ちユニットは既に存在するフレーム。  
   修正案: `_teamResolved` が false の間は `Tick()` を実行しない。少なくともPlayerController自身は `CompareTag("Player")` 等で常時表示にする。

### Low

7. `Assets/_Project/Scripts/Combat/StatusEffectController.cs:27` / Low  
   症状: `Awake` 時点で `HealthComponent` が無い場合、後から追加されても `Revived` 購読が張られず、リスポーン時にCCが自動クリアされない。現在の主要PrefabではHealthComponent同居前提なので低リスク。  
   想定される再現条件: `StatusEffectController.GetOrAdd` がHealthComponent追加前のGameObjectに呼ばれ、その後HealthComponentが追加される。  
   修正案: `OnEnable` で `_health == null` なら再取得する。もしくは `[RequireComponent(typeof(HealthComponent))]` を付けるか、Health無し運用を明示する。

8. `Assets/_Project/Scripts/UI/GameHudController.cs:366` / Low  
   症状: シールド帯が `Shield / MaxHp` を左詰めで描画される。仕様次第だが、一般的なHPバーでは「現在HPの右側に追加HPとして乗る」表示の方が吸収量として読み取りやすい。現状はHPフィルと重なり、HPとシールドの合計耐久が視覚的に分かりづらい可能性がある。  
   想定される再現条件: CurrentHp 50%、Shield 30% の状態。白帯がバー左から30%表示され、HPの先にある追加耐久として見えない。  
   修正案: Shield幅は `Shield / MaxHp` のまま、leftを `CurrentHp / MaxHp` にするか、HP上に重ねる設計ならUSS/色で明確に区別する。

## OK

- OK: `StatusEffectController` の `Revived += Clear` は、HealthComponentがAwake時に存在する通常構成では `OnEnable` / `OnDisable` が対称。
- OK: `TelegraphCircle` は `IDamageable` 単位で重複排除しており、多Collider対象への多重ヒットを避けている。
- OK: `Projectile` / `TelegraphCircle` / `SkillCaster.CastTargeted` は TeamRules で味方ダメージを回避している。
- OK: `CentralObjectiveDirector` の boss `Died` 購読は `OnDestroy` で解除されている。
- OK: `FogOfWarDirector.Instance` と `CentralObjectiveDirector.Instance` は `OnDestroy` で自分自身の場合のみnullに戻している。
- OK: `FogOfWarDirector.CleanupDestroyed` は追跡対象外になった非表示ユニットを表示状態に戻してからキャッシュから外している。
- OK: `GameHudController.UpdateObjective` は `dir == null` / `!HasObjective` でHUDを非表示にしており、未生成時のnull参照は避けられている。

## 全体所感

純ロジック層はテストで守られており、結合層も大半はnull安全と購読解除が入っている。主なリスクは「成功判定前にコストを消費する」「非表示中のColliderが残る」「CCゲートが通常移動以外の移動経路に漏れる」の3系統。修正優先度は High 2件、次に Medium 4件。
