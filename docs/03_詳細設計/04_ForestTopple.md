# 倒木演出(エニグマ・コア討伐)

> **状態**: 実装済み(2026-06-12)。**Confluence 未同期** — 次回 Atlassian MCP 接続時に 03_詳細設計 配下へ追記すること。

## 概要

中立ボス「エニグマ・コア」を討伐すると、ジャングルの木(`Tree_Q*`、104本)がボスを震源として外側へ波及しながら倒れる演出。倒れた木は少し見せた後に地面へ沈み、コライダーごと消える。ゲームプレイ上の効果はなし(演出のみ)だが、討伐後はジャングルの見通しが開ける。

## トリガー

`NeutralBoss` の `HealthComponent.Model.Died` イベント。既存の `NeutralBossController`(倒れ演出・チームバフ付与)には手を入れず、独立コンポーネントで購読する。

## 構成クラス(いずれも `Enigma.Map`、Scripts/Map/)

| クラス | 種別 | 責務 |
|---|---|---|
| `ToppleWavePlanner` | plain C#(`IRandomSource` 注入) | 震源からの XZ 距離 ÷ 波速 + ジッタで各木の倒れ開始遅延を計算。外向きに倒すための回転軸 `ToppleAxis`(`Cross(up, d)`)も提供 |
| `TreeTopplePresenter` | MonoBehaviour(各木に付与) | 転倒(1.2s、`DeathAnimationCurve.ToppleAngle`)→静止(0.8s)→沈下(1.5s、`SinkDepth`)→`SetActive(false)`。回転はワールド軸の前掛けでランダム yaw に依存しない |
| `ForestToppleDirector` | MonoBehaviour(ボスに付与) | `Died` 購読 →全 `TreeTopplePresenter` を収集し、Planner の遅延・軸で `Fall` を一斉指示。一度きりガード付き |

パラメータ(`ForestToppleDirector` のシリアライズ値): 波速 `_waveSpeed = 14 m/s`、ジッタ `_maxJitterSeconds = 0.4s`。最遠 ~38m で開始遅延は最大約3秒。

## 設計判断

- **沈下で消す(フェードでなく)**: `DeathPresenter` の Topple はマテリアル複製によるアルファフェードを伴うが、104本分のマテリアル生成はコスト・リーク管理の面で不利。`DeathAnimationCurve` の転倒/沈下カーブのみ流用し、マテリアルには触れない。
- **コライダーは木ごと回し、沈下完了で GameObject ごと無効化**: 「見えない壁」を残さないことを優先。転倒中もコライダーがルートと一緒に回るため見た目と判定が一致する。
- **`DeathPresenter` を木に再利用しない**: 同コンポーネントは `HealthComponent` 必須で、木に HP を持たせるのは不自然なため専用 Presenter とした。
- **静的フラグの変更(重要)**: 木は従来 `BatchingStatic` 付きで生成されており、静的バッチ済みメッシュは実行時の Transform 変更に追従しない。`BuildAetherRiftMap.PlaceOneTree` を `ContributeGI` のみ(子も再帰)に変更した。**マップ再生成(`BuildAetherRiftMap.Execute`)が必要**(実施済み)。

## 変更ファイル

- 新規: `Assets/_Project/Scripts/Map/ToppleWavePlanner.cs` / `TreeTopplePresenter.cs` / `ForestToppleDirector.cs`
- 新規: `Assets/_Project/Tests/EditMode/ToppleWavePlannerTests.cs`(6テスト)
- 変更: `Assets/Editor/BuildAetherRiftMap.cs`(`SetStaticContributeGiOnly` 追加、`PlaceOneTree` で Presenter 付与、`CreateBoss` で Director 付与)

## 検証(2026-06-12)

- EditMode テスト 6/6 成功(距離比例・Y無視・ジッタ上限・速度0耐性・外向き軸・縮退ケース)
- Play モード実測: 討伐 5.3 秒後に近距離13本沈下完了/21〜38m の89本転倒中/最遠2本未着手 → 距離順の波及を確認。最終的に 104/104 本が非アクティブ。エラーログなし
