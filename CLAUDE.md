# Enigma — CLAUDE.md

## セッション運用ルール（必読）

- **従量課金サービスは絶対に使用禁止**。ユーザーの Claude Max 20 プランの利用枠のみ使用する（Anthropic API キーの直接利用、/code-review ultra、Coplay のクレジット消費系生成ツール等は禁止。課金形態が不明な外部サービスは使用前にユーザーへ確認）
- メインセッションは**設計・監査・レビューに専念**する
- 実装作業はトークン節約のためサブエージェント（Agentツール、model指定）に切り出して実行する
- 例外: **実装難易度が特に高い箇所**はメインセッションが直接実装してよい
- サブエージェントへのプロンプトには、対象ファイルパス・命名規約・設計方針を必ず含める（サブエージェントはこの会話の文脈を持たない）

## トークン節約方針

### モデルの使い分け（難易度で階層化）

| モデル | 任せる作業 |
|---|---|
| **Haiku** | 機械的な軽作業: 単一ファイルの定型修正、ボイラープレート生成、一時エディタスクリプト作成、ログ/テスト結果の要約。**完全な手順とチェックリストを渡す**（判断させない） |
| **Sonnet** | 通常の実装（複数ファイル、設計判断を含まないもの） |
| **Opus / メイン** | 高難度の実装・設計・監査のみ |

### サブエージェント運用

- 読むべきファイルを明示し、**探索させない**（探索は最もトークンを浪費する）
- 報告フォーマットを指示で固定する: 「変更ファイルと要点の箇条書きのみ。コード全文の引用・巨大な表は禁止」
- 同じサブエージェントへの追加依頼は新規起動でなく SendMessage で継続（コンテキスト再構築を避ける）

### 検証・I/O

- **スクリーンショットは高コスト**（1枚で数千トークン相当）。機能のまとまりごとに最小限の枚数とし、中間確認はコンパイルチェック・ログ・テストで代替する
- テスト結果・ログはファイルに書き出し、必要な行だけ読む
- ファイルは必要範囲のみ Read（offset/limit、Grep の head_limit）。編集直後のファイルを確認目的で再読しない

### Confluence

- 更新 API は**全文置換**のため、巨大ページは更新のたびに全文を再送することになる。**1ページを肥大化させず、セクションが育ったら子ページに分割**する
- 更新は変更があるページのみ。複数ページの一括同期はしない

## 設計書の管理（Confluence が正）

- 設計書は **Confluence の「Enigma」スペース**（https://n4t.atlassian.net/wiki/spaces/Enigma）で管理する。**Confluence 側を正とする**
- ローカル `docs/` は参照用スナップショット。設計変更時は Confluence を更新すること
- ページ構成はローカル `docs/` と同じ3階層: `01_基本設計` / `02_システム設計` / `03_詳細設計`
- Atlassian MCP（cloudId: `6d20cfec-8405-427d-8fb6-7d9e9bac6a22`、スペースID: `65843`）でページの読み書きが可能

## プロジェクト概要

**Enigma（エニグマ）** は Fantasy × SF テーマの 3D MOBA。Smite 風の TPS 視点（FF14 準拠カメラ）、Hoyoverse 風トゥーン表現。

- **Unity バージョン**: プロジェクト設定に従う
- **レンダーパイプライン**: URP (Universal Render Pipeline)。Hoyoverse 風 NPR のため HDRP から移行（2026-06-11）
- **言語**: C#
- **ターゲットプラットフォーム**: PC (Windows/Mac)

## サードパーティアセット

- **ユニティちゃん**（`Assets/UnityChan/`、UCL2.02）: プレイヤーモデル。**配布時はタイトル/クレジット画面に「© Unity Technologies Japan/UCL」表記が必須**（docs/THIRD_PARTY_NOTICES.md 参照）。License フォルダは削除禁止

## ディレクトリ構造

```
Assets/
├── _Project/               # プロジェクト固有アセット（先頭 _ で整理）
│   ├── Scripts/
│   │   ├── Characters/     # キャラクター基底クラス・コンポーネント
│   │   ├── Abilities/      # スキルシステム
│   │   ├── Combat/         # 戦闘ロジック（ダメージ、CC、バフ）
│   │   ├── UI/             # HUD、スコアボード、スキルUI
│   │   ├── Networking/     # Netcode for GameObjects
│   │   ├── GameModes/      # MOBA ゲームモードロジック
│   │   ├── Map/            # ジャングル、レーン、オブジェクティブ
│   │   └── Core/           # GameManager、EventBus、定数
│   ├── Prefabs/
│   ├── Materials/
│   ├── Shaders/
│   ├── Animations/
│   ├── Audio/
│   └── VFX/
├── Scenes/
│   ├── MainMenu.unity
│   ├── AetherRift_Map.unity  # メインMAPシーン
│   └── CharacterSelect.unity
└── TutorialInfo/           # Unity デフォルト（触らない）
```

## コーディング規約（標準 C# 規約準拠）

- **名前空間**: `Enigma.{サブシステム名}` (例: `Enigma.Abilities`)
- **クラス/メソッド/プロパティ/定数**: PascalCase（`KEY_BGM` のような SCREAMING_SNAKE は禁止）
- **プライベートフィールド**: `_camelCase`（`[SerializeField]` 付きも同様）
- **パブリック API**: フィールドではなくプロパティを公開する。ScriptableObject のデータ定義のみ public PascalCase フィールド可
- **インターフェース**: `I` プレフィックス (例: `IDamageable`)
- **シリアライズ済みフィールドのリネーム**: 必ず `[FormerlySerializedAs]` を付けてアセット/シーン参照を保護する
- **ScriptableObject**: データ定義に積極的に使用
- コメントは WHY が非自明な場合のみ記述

## 設計方針（疎結合・テスタビリティ）

- **static クラスへの直接依存は禁止**。サービスはインターフェース（`ISaveStore`, `IGachaService` 等）+ コンストラクタ注入の plain C# クラスで実装する
- **Unity API（PlayerPrefs / Screen / QualitySettings / Random 等）は直接呼ばず抽象でラップ**し、テストでフェイクに差し替え可能にする
- **MonoBehaviour は Humble Object**: ロジックを持たず、サービスの組み立ては composition root（`GameServices`）経由で取得する
- **ユニットテスト**: ロジックは EditMode テスト（`Assets/_Project/Tests/EditMode`）で fake を注入して検証する。新しいサービスを追加したらテストも書く
- アセンブリ: ゲームコードは `Enigma.asmdef`、テストは `Enigma.Tests.EditMode.asmdef`

## 主要システム

| システム | 概要 |
|---|---|
| AbilitySystem | スキル定義 (ScriptableObject) + 実行 (MonoBehaviour) |
| CharacterController | TPS移動、回転、アニメーション連携 |
| CombatSystem | ダメージ計算、CC管理、デス/リスポーン |
| ObjectiveSystem | タワー、フェニックス、タイタン HP管理 |
| NetworkManager | Netcode for GameObjects ベース |
| MinimapSystem | ワールド座標 → ミニマップ変換 |

## アーキテクチャ方針

- `GameManager` はシングルトンだが `static` 乱用禁止、EventBus 経由で疎結合
- スキルは `AbilityBase` (ScriptableObject) を継承して定義し、`AbilityExecutor` (MonoBehaviour) が実行
- ダメージは必ず `IDamageable.TakeDamage()` を通す
- 不透明マテリアルは原則 `Enigma/Toon`（セルルック統一）、エフェクト系半透明は `URP/Unlit`。旧来の Standard Shader 禁止

## 依存パッケージ（予定）

- `com.unity.render-pipelines.universal`
- `com.unity.netcode.gameobjects`
- `com.unity.inputsystem`
- `com.unity.cinemachine`
- `com.unity.mathematics`
- `com.unity.collections`

## よく使うパス

- メインシーン: `Assets/Scenes/AetherRift_Map.unity`
- キャラクタースクリプト: `Assets/_Project/Scripts/Characters/`
- スキル定義 SO: `Assets/_Project/Scripts/Abilities/`
