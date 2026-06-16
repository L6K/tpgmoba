# codex-tasks — Codex 向けタスク指示書の置き場

このフォルダは **Codex に振る実装タスクの指示書**を置く所定の場所です。
各ファイルは「Codex がこの会話の文脈なしで自走できる」よう自己完結で書かれています。

## 運用ルール
- 1タスク = 1ファイル。指示書のファイル名は `NN-短い名前.md`（連番）。
- 指示書には必ず: 対象ファイルの絶対パス・命名規約・API/インターフェース仕様・確定仕様（曖昧さゼロ）・テスト要件・完了条件 を含める。
- Codex には**実機（Unity エディタ）検証ループが不要な純ロジック＋EditMode テスト**を中心に振る。シーン結線・VFX・シェーダー等は Claude 側（coplay 連携あり）が担当。
- **結果報告も同じ `codex-tasks/` に置く**。ファイル名は `NN-短い名前-report.md`（指示書とペア）。報告には: 変更/作成ファイル一覧・要点の箇条書き・テスト結果（緑/赤と件数）・残課題 を簡潔に書く（コード全文の貼り付けは不要）。
- 完了したら下の一覧の状態を更新する。

## タスク一覧
| # | ファイル | 内容 | 状態 |
|---|---|---|---|
| 01 | [01-statuseffect-model.md](01-statuseffect-model.md) | StatusEffect 基盤（スタン/ルート/スロウの plain C# モデル + EditMode テスト）。提案D（CC）の土台 | ✅ 完了・レビュー合格: [report](01-statuseffect-model-report.md) |
| 02 | [02-shield-absorption.md](02-shield-absorption.md) | シールド吸収レイヤー（HealthModel 拡張・FIFO消費・時間減衰 + テスト追記）。提案D（シールド）の土台 | ✅ 完了・レビュー合格（EditMode全331件緑）: [report](02-shield-absorption-report.md) |
| 03 | [03-vision-reveal-model.md](03-vision-reveal-model.md) | VisionRevealModel（視界源/対象から可視判定する plain C#・linger付き + テスト）。提案E（Fog of War）の核 | ✅ 完了・レビュー合格（EditMode全346件緑・FoWプレイモードで機能確認）: [report](03-vision-reveal-model-report.md) |
| 04 | [04-objective-spawn-timer.md](04-objective-spawn-timer.md) | ObjectiveSpawnTimerModel（中央オブジェクトの出現/再出現/予告タイミングの plain C# + テスト）。提案P0「中央オブジェクト主役化」の土台 | ✅ 完了・レビュー合格（EditMode全361件緑・CentralObjectiveDirectorで実機ライフサイクル確認）: [report](04-objective-spawn-timer-report.md) |
| 05 | [05-integration-review.md](05-integration-review.md) | 結合コード（D/E/P0 の MonoBehaviour層、テスト無し）の静的レビュー。バグ/リスクを report に書くだけ（修正なし） | ✅ 完了: [report](05-integration-review-report.md)（High2/Med4/Low2 検出→全件修正済 `fbb858b4`） |
| 06 | [06-fix-rereview.md](06-fix-rereview.md) | 05 指摘の修正（`fbb858b4`）の再レビュー。各指摘の解消/退行を確認 | ✅ 完了 [report](06-fix-rereview-report.md) |
| 07 | [07-objective-buff-model.md](07-objective-buff-model.md) | ObjectiveBuffModel（中央オブジェクト報酬バフの多種別・時間管理 plain C# + テスト）。次タスク①「中央オブジェクト完遂」の土台 | ✅ 完了（EditMode `dotnet test` 緑）: [report](07-objective-buff-model-report.md) |
| 08 | [08-bot-macro-decision-model.md](08-bot-macro-decision-model.md) | BotMacroDecisionModel（集合/撤退/押し/守り/ファーム判断の純関数 + テスト）。次タスク②「Botマクロ判断」の土台 | ✅ 完了（EditMode `dotnet test` 緑）: [report](08-bot-macro-decision-model-report.md) |
| 09 | [09-integration-review-2.md](09-integration-review-2.md) | 05以降の結合コード（中央オブジェクト完成/バグ修正/MatchHint/Ctrl-QER/ジャンプ/Q VFX 等）の静的レビュー。修正なし・report に所見 | ✅ 完了: [report](09-integration-review-2-report.md)（High1/Med3/Low3） |
| 10 | [10-ping-command-model.md](10-ping-command-model.md) | PingCommandModel（ピン発行/連打抑制/有効ピン管理＋ラジアル選択 角度→種別 の純ロジック + テスト）。情報戦の核 | ✅ 完了（EditMode `dotnet test` 緑）: [report](10-ping-command-model-report.md) |

備考: ①中央オブジェクト=ObjectiveBuffModel(07)+CentralObjectiveDirector で完成、②Botマクロ=BotMacroDecisionModel(08)+EnemyChampionAI で結線、MatchHint も実装済(EditMode全403件緑)。

参考: [assets-image-manifest.md](assets-image-manifest.md) — 画像アセット(効果5/スキル18/ポートレート5)の生成は **5.5** 担当・**後回し**。

## Latest task status
- 14: Completed `PlayerHitFeedbackModel` plain C# model + EditMode tests. Report: [14-hit-feedback-model-report.md](14-hit-feedback-model-report.md)
- 15: [死亡recap] `DeathRecapModel` 指示書 — 未着手: [15-death-recap-model.md](15-death-recap-model.md)
- 16: [ワード] `WardVisionModel` — ✅ **Claude が実装済(小規模のため。Codexへ投げない)**。WardController/FoW結線も完了 (commit 311055d3)
- 17: [キル演出] `MultiKillStreakModel` — ✅ **Claude が実装済(小規模のため)**。KillFeedDirector結線(マルチキル/ストリーク/シャットダウン報酬)完了
- 18: [次元リフト] `RiftEventModel` 指示書 — 未着手: [18-rift-event-model.md](18-rift-event-model.md)
- 19: [オーバークロック] `OverclockModel` 指示書 — 未着手: [19-overclock-model.md](19-overclock-model.md)
- 20: [レリック] `RelicLoadoutModel` 指示書 — 未着手: [20-relic-loadout-model.md](20-relic-loadout-model.md)
- 21: [環境ギミック] `GimmickPhysicsModel` 指示書(ジャンプパッド弾道/重力井戸/ゲート減速) — 未着手: [21-gimmick-physics-model.md](21-gimmick-physics-model.md)
