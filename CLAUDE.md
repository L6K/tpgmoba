# Enigma — CLAUDE.md

## セッション運用ルール（必読）

- メインセッションは**設計・監査・レビューに専念**する
- 実装作業はトークン節約のため **Opus / Sonnet のサブエージェント**（Agentツール、model指定）に切り出して実行する
- 例外: **実装難易度が特に高い箇所**はメインセッションが直接実装してよい
- サブエージェントへのプロンプトには、対象ファイルパス・命名規約・設計方針を必ず含める（サブエージェントはこの会話の文脈を持たない）

## プロジェクト概要

**Enigma（エニグマ）** は Fantasy × SF テーマの 3D MOBA。Smite 風の TPS 視点（肩越しカメラ）、Unity HDRP レンダリング。

- **Unity バージョン**: プロジェクト設定に従う
- **レンダーパイプライン**: HDRP (High Definition Render Pipeline)
- **言語**: C#
- **ターゲットプラットフォーム**: PC (Windows/Mac)

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

## コーディング規約

- **名前空間**: `Enigma.{サブシステム名}` (例: `Enigma.Abilities`)
- **クラス/メソッド**: PascalCase
- **フィールド**: camelCase、SerializeField は `_` プレフィックスなし
- **インターフェース**: `I` プレフィックス (例: `IDamageable`)
- **ScriptableObject**: データ定義に積極的に使用
- コメントは WHY が非自明な場合のみ記述

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
- HDRP マテリアルは `Lit` または独自サブグラフを使用、旧来の Standard Shader 禁止

## 依存パッケージ（予定）

- `com.unity.render-pipelines.high-definition`
- `com.unity.netcode.gameobjects`
- `com.unity.inputsystem`
- `com.unity.cinemachine`
- `com.unity.mathematics`
- `com.unity.collections`

## よく使うパス

- メインシーン: `Assets/Scenes/AetherRift_Map.unity`
- キャラクタースクリプト: `Assets/_Project/Scripts/Characters/`
- スキル定義 SO: `Assets/_Project/Scripts/Abilities/`
