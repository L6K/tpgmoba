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
| 04 | [04-objective-spawn-timer.md](04-objective-spawn-timer.md) | ObjectiveSpawnTimerModel（中央オブジェクトの出現/再出現/予告タイミングの plain C# + テスト）。提案P0「中央オブジェクト主役化」の土台 | 未着手 |
