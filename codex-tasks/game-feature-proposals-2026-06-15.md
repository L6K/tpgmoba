# Enigma 機能提案書: 実MOBA比較から見た次フェーズ候補

作成日: 2026-06-15  
対象: `D:\Document\smite\smite` / `codex-tasks` 直下の企画・タスク化メモ  
位置づけ: これは実装指示書ではなく、今後の `NN-*.md` タスクへ分解するための提案書。

## 1. 現状評価

Enigma は、既に「TPS視点のMOBA」として試合の骨格がかなり揃っている。

- 3v3、2レーン、中央ジャングル、タワー、タイタン、中央オブジェクトという基本構造がある。
- プレイヤー移動、通常攻撃、スキル発動、AoE予兆、ターゲット指定、クールダウン、スキル成長がある。
- HP、死亡、復活、CC、シールド、チームバフ、キルフィード、HUD、ミニマップ、ショップ、リザルトが実装されつつある。
- Bot、ミニオン、ジャングルモンスター、タワー攻撃、Titan勝敗判定があり、ローカルの対Bot MOBAとして遊べる土台がある。

一方で、実際のMOBAと比べると「試合中の意思決定」と「情報戦」と「長期プレイ動機」がまだ薄い。ここを足すと、単なる戦闘プロトタイプからゲームらしさが一段上がる。

## 2. 実ゲーム比較の要点

### League of Legends 系

LoL は「レーン、タワー、ジャングル、Baron/Drake、経験値、ゴールド、アイテム、ロール」を学習導線として明確に見せている。公式の入門ページでも、Nexus破壊、タワー/インヒビター、ジャングル目標、5ロール、XP/Gold/Item/Ability unlock が基礎として整理されている。

Enigma に対応させるなら、試合内で「今どこへ行くべきか」「なぜ中央オブジェクトが重要か」「今のビルドで何が強いか」をもっとUIとBotの動きで教えると良い。

### SMITE 系

SMITE はTPS MOBAとして最も近い比較対象。Conquest では3レーン、タワー、Phoenix、Titan、ミニオン、ジャングルバフ、アイテム、ワード、ロール/クラスが試合の判断を作っている。

Enigma はTPS操作とタワー/Titan/中央ボスは近いが、ジャングルバフの選択、ワード/視界、レリック/アクティブ、クラスごとの役割差はまだ薄い。3v3・2レーンに合わせて簡略化した形で入れると相性が良い。

### Dota 2 系

Dota は複雑だが、参考になるのは「情報戦」「マップ資源」「プレイヤーを助けるガイド」の部分。Fog of War、Roshan、ルーン、Neutral camp、アイテムガイド、リプレイ/戦績などが、試合ごとの読み合いと学習を支えている。

Enigma では全部を入れる必要はない。短試合向けに「中央資源」「視界」「おすすめビルド」「デス原因表示」へ圧縮すると効果が大きい。

## 3. 優先提案

### P0: 中央オブジェクトをゲームの主役にする

現状の中央オブジェクトはチームダメージバフとして機能しているが、勝敗を左右する存在としてはまだ単調。Enigma の差別化軸は中央争奪なので、ここは最優先で厚くしたい。

提案:

- 中央オブジェクトに段階演出を入れる: 出現予告、交戦中UI、討伐直前アナウンス、獲得演出。
- バフを1種類固定から、試合時間や討伐回数で変化する複数効果にする。
- 例: ダメージ強化、ミニオン強化、移動速度、タワー攻撃弱体化、シールド付与。
- 中央討伐後に短時間だけレーンを押しやすくする「攻め時」を作る。
- 劣勢側にも触れる余地を残すため、バフは強いが即勝ちではない程度にする。

タスク化候補:

- `ObjectiveBuffModel` の純ロジック化: バフ種別、持続、重ね掛け、残り時間。
- `ObjectiveSpawnTimerModel`: 出現/再出現/警告タイミング。
- HUD: 中央オブジェクト状態、残り時間、獲得チーム表示。
- Bot: 中央出現30秒前に寄る、低HPなら諦める、味方人数差で判断する。

### P0: 試合中の「次に何をすべきか」を見える化する

MOBA初心者が迷う最大の理由は、操作よりも「次に何をすればいいか」が分からないこと。LoLの公式入門がロール、レーン、XP/Gold、オブジェクトを明確に説明しているように、Enigma も試合中UIで次の行動を示すと定着しやすい。

提案:

- 画面上部に「次の推奨行動」を短く出す。
- 例: 「Topの味方ミニオンを待ってタワーを攻めよう」「中央ボス出現まで30秒」「所持金が貯まったので帰還して装備更新」。
- ミニマップに次目標の強調リングを出す。
- 初回プレイ時だけ、過剰にならないチュートリアルヒントを出す。

タスク化候補:

- `MatchHintModel`: 試合時間、HP、Gold、Objective timer、タワー状況からヒントを1つ選ぶ純ロジック。
- `HintPriorityRulesTests`: 同時条件で最も重要なヒントを選ぶテスト。
- HUD 結線: ヒント文言とミニマップ強調。

### P1: 3v3用ロールとキャラ個性をはっきりさせる

実MOBAはキャラの役割が明確。SMITE なら Assassin / Guardian / Hunter / Mage / Warrior、LoL ならレーン/ロールが学習軸になる。Enigma は3v3なので、5ロールをそのまま入れるより、少数ロールへ圧縮した方が分かりやすい。

提案:

- 初期ロールを3系統にする。
- Vanguard: 前に出てCC/シールドで味方を守る。
- Striker: 通常攻撃/方向指定スキルで火力を出す。
- Controller: AoE予兆、Root/Slow、中央争奪のゾーニングが得意。
- 各キャラに固有パッシブを1つ持たせる。
- 初期は3〜5体に絞り、全員が「やることが違う」状態を作る。

タスク化候補:

- `CharacterRole` enum と `CharacterData` へのロール/難易度/推奨レーン追加。
- `PassiveEffectModel`: 純ロジックで扱える簡単なパッシブ候補から開始。
- キャラ選択UI: ロールフィルタ、初心者おすすめ、チーム構成警告。

### P1: アイテムとレリックを「選択が楽しい」層にする

現状のショップ/アイテム枠はあるが、ビルド選択の意味をもう少し強めたい。SMITE のアイテム/ワード/レリック、LoL のアイテム/サモナースペル、Dota のアイテムガイドを参考に、短試合用に簡略化する。

提案:

- 6アイテムとは別に、1つだけ「レリック」枠を作る。
- レリック例: 短距離ブリンク、浄化、瞬間シールド、加速、ワード設置。
- アイテムは最初から大量に作らず、攻撃/防御/機動/クールダウン/対CCの5系統にする。
- ショップに「おすすめ購入」を出す。

タスク化候補:

- `RelicCooldownModel`: 使用可否、クールダウン、チャージ数。
- `RecommendedItemService`: 現在Gold、ロール、所持アイテムから次候補を返す。
- HUD: レリックスロット、クールダウン表示。
- Bot: ロール別に最低限の購入ルール。

### P1: 視界・ピン・情報戦を足す

MOBAらしさは「敵が見えない」「見えた情報を共有する」ことで強くなる。Dota/LoL/SMITE ほど複雑なFog of Warを最初から入れなくても、3v3用の軽い情報戦は実装価値が高い。

提案:

- ミニマップ上の敵表示を、距離や視界条件で制限する。
- 置き型ワードまたは短時間スキャンを追加する。
- ピンを3種類に絞る: 注意、集合、攻撃。
- 中央オブジェクト周辺に視界争いを作る。

タスク化候補:

- `VisionRevealModel`: 味方/敵/ワード/半径から可視判定する純ロジック。
- `PingCommandModel`: ピン種類、位置、クールダウン、連打抑制。
- ミニマップ結線: 見えている敵だけ表示、ピン表示。

### P1: Botのマクロ判断を強くする

現状のBotはレーン戦・中立狩り・スキル使用の骨格がある。次は「MOBAらしい判断」を増やすと、ソロ開発中の検証品質も上がる。

提案:

- 中央オブジェクト集合: 出現前、人数差、HP、レーン状況で集合判断。
- 低HP撤退: HPだけでなく敵人数、タワー距離、シールド有無を見る。
- ウェーブ待ち: タワー下に味方ミニオンがいない時は無理に入らない。
- 押し引き: バフ中は攻める、人数不利なら引く。
- Bot用ピン: 集合/撤退/攻撃をHUDやログに出す。

タスク化候補:

- `BotMacroDecisionModel`: Objective、HP、人数差、タワー、ミニオン有無から行動を返す。
- `BotMacroDecisionTests`: オブジェクト集合、撤退、タワー攻め、ジャングル優先のケース。
- `EnemyChampionAI` への薄い結線。

### P2: デス原因とリザルトを学習ツールにする

実MOBAは負けた理由が分からないと離脱されやすい。Enigma は短試合なので、デス時と試合後に「次はどうすればよいか」を返すとプレイ継続につながる。

提案:

- デス recap: 最後に受けた主なダメージ、CC、攻撃者、タワー被弾の有無を表示。
- リザルトに、K/Dだけでなく与ダメ、被ダメ、シールド吸収、回復、中央参加回数を出す。
- MVPを単純キル数ではなく、オブジェクト貢献・ダメージ・耐久も含めて出す。
- 初心者向けに「次回の改善ヒント」を1つだけ表示する。

タスク化候補:

- `DamageEventLog`: 直近数秒の被ダメ/加害者/種別を保持。
- `MvpScoreModel`: KDA、Objective、Damage、Shield、Heal からスコア化。
- `MatchStatsContext` 拡張: 統計項目追加。

### P2: オンライン縦切り版を早めに作る

設計書では Netcode for GameObjects 前提だが、現状の実装はローカル/対Bot中心。オンライン前提の設計差分は後から入れるほど重くなる。全機能オンライン化ではなく、最小縦切りを早めに作るのがよい。

提案:

- まずは2人だけの小部屋で、位置、HP、通常攻撃、1スキル、死亡/復活だけ同期する。
- サーバー権威にする対象を最初から決める。
- Bot、ミニオン、タワーはホスト側で処理し、結果を同期する。
- 通信が未完成でも、同期対象リストと権威の境界をコード上に作る。

タスク化候補:

- `NetworkCombatBridge` の薄い試作。
- 位置/HP/スキル発動の同期だけに絞った検証シーン。
- 切断/再接続は後回し。まず同期境界を決める。

### P2: 短試合向けの逆転・降参・試合テンポ調整

15〜20分MOBAでは、序盤の失敗で負け確定に見えると離脱されやすい。逆に逆転要素が強すぎると勝っている側が不快。ここは数式モデルで慎重に扱うとよい。

提案:

- 劣勢チームが中央オブジェクトを取った時だけ少し強いバフにする。
- 連続デスしたプレイヤーの報酬価値を下げる。
- 長引きすぎた時のOvertimeは既にあるので、HUDで明確に見せる。
- 降参投票は設計にあるため、UIと最低限のローカル実装を足す。

タスク化候補:

- `ComebackValueModel`: チーム差、時間、Objective取得で補正値を返す。
- `SurrenderVoteModel`: 投票数、時間制限、成立条件。
- Overtime HUD: 建造物減衰の開始警告。

### P3: Enigma独自のマップギミックを育てる

差別化の種として、倒木/地形変化、Fantasy × SF、中央オブジェクトがある。ここはLoL/SMITE/Dotaの模倣ではなく、Enigmaの顔にできる。

提案:

- 中央ボス討伐で一定時間だけマップの通路が変わる。
- 倒木やエネルギー壁で射線/移動経路が変わる。
- 試合時間で中央エリアが段階変化する。
- AoE予兆と地形変化を連動させ、FF14的な「見て避ける」強みを出す。

タスク化候補:

- `MapPhaseModel`: 時間/Objective回数からフェーズを返す。
- `TemporaryLaneModifier`: 一時的な通行可否/危険エリアの管理。
- Claude側タスク: シーン/VFX/ナビメッシュ結線。

## 4. Codexに切りやすい純ロジックタスク案

次の順で `codex-tasks/NN-*.md` に分解すると進めやすい。

1. `ObjectiveSpawnTimerModel`
   - 出現、再出現、警告、残り時間表示の純ロジック。
2. `ObjectiveBuffModel`
   - バフ種別、持続、チーム別状態、重ね掛けルール。
3. `MatchHintModel`
   - 現在状況から初心者向けヒントを1つ選ぶ。
4. `BotMacroDecisionModel`
   - Botの集合/撤退/攻撃/ファーム判断。
5. `RelicCooldownModel`
   - レリック使用、クールダウン、チャージ。
6. `RecommendedItemService`
   - ロール/所持Gold/所持アイテムから次の購入候補。
7. `VisionRevealModel`
   - 味方/敵/ワード/距離による可視判定。
8. `PingCommandModel`
   - ピン種類、連打抑制、位置データ。
9. `DamageEventLog`
   - デスrecap用の直近被ダメ記録。
10. `SurrenderVoteModel`
   - 投票成立条件と制限時間。

## 5. Claude側に向いている結線タスク案

Unity Editor、シーン、UI Toolkit、VFX、Coplay確認が必要なものは Claude 側が向いている。

- 中央オブジェクトの出現演出、獲得演出、HUD結線。
- ミニマップ上のObjective/Ping/Vision表示。
- レリックスロットと入力結線。
- Tutorial/PracticeモードのUIと導線。
- BotのNavMesh/ウェイポイント調整。
- ネットワーク検証シーンとPrefab結線。
- マップギミック、倒木、中央エリア変化のVFX/Collider/NavMesh調整。

## 6. 推奨ロードマップ

### Phase 1: いま遊んで分かる改善

- 中央オブジェクトUI/タイマー/演出。
- Botの中央集合と撤退判断。
- MatchHint表示。
- リザルト統計の拡張。

目的: ローカルBot戦だけでも「MOBAの試合をしている」感を強くする。

### Phase 2: 戦略層の追加

- ロール/キャラ個性。
- レリック。
- おすすめアイテム。
- 視界/ピン。

目的: 毎試合の選択とチーム判断を増やす。

### Phase 3: オンライン縦切り

- 2人同期の最小検証。
- HP/通常攻撃/1スキル/死亡復活の同期。
- Bot/ミニオン/タワーのホスト権威処理。

目的: 後から壊れにくい同期境界を早めに確定する。

### Phase 4: Enigma独自要素

- 中央ボスによるマップ変化。
- AoE予兆と地形ギミックの連動。
- Fantasy × SF らしい視覚演出。

目的: 既存MOBAの模倣から、Enigmaの顔を作る。

## 7. 参考にしたローカル根拠

- `D:\Document\smite\smite\AGENTS.md`
- `D:\Document\smite\smite\docs\01_基本設計\01_ゲーム概要.md`
- `D:\Document\smite\smite\docs\01_基本設計\03_UI設計.md`
- `D:\Document\smite\smite\docs\02_システム設計\01_アーキテクチャ.md`
- `D:\Document\smite\smite\docs\02_システム設計\03_ネットワーク設計.md`
- `D:\Document\smite\smite\Assets\_Project\Scripts\Abilities\SkillDefinition.cs`
- `D:\Document\smite\smite\Assets\_Project\Scripts\UI\GameHudController.cs`
- `D:\Document\smite\smite\Assets\_Project\Scripts\Characters\EnemyChampionAI.cs`
- `D:\Document\smite\smite\Assets\_Project\Scripts\Core\MatchFlowController.cs`
- `D:\Document\smite\smite\Assets\_Project\Scripts\Data\TeamBuffService.cs`
- `D:\Document\smite\smite\codex-tasks\01-statuseffect-model-report.md`
- `D:\Document\smite\smite\codex-tasks\02-shield-absorption-report.md`

## 8. 外部比較メモ

- League of Legends official "How to Play": https://www.leagueoflegends.com/en-us/how-to-play/
- SMITE gameplay overview: https://en.wikipedia.org/wiki/Smite_(video_game)
- Dota 2 gameplay overview: https://en.wikipedia.org/wiki/Dota_2

外部情報は、ゲーム構造の比較軸を作るために参照した。Enigma の仕様判断はローカル設計書と実装状況を優先する。
