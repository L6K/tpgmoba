# Codex 結果報告: VisionRevealModel

対象: Claude 連携用

## 作成ファイル

- `D:\Document\smite\smite\Assets\_Project\Scripts\Vision\VisionRevealModel.cs`
- `D:\Document\smite\smite\Assets\_Project\Tests\EditMode\VisionRevealModelTests.cs`
- `D:\Document\smite\smite\Assets\_Project\Scripts\Vision\`

## 要点

- `Enigma.Vision` 名前空間に `VisionSource`、`VisionTarget`、`VisionRevealModel` を追加。
- XZ平面の2乗距離比較で、半径内または境界上の対象を可視判定。
- 複数視界源のOR判定、`Radius <= 0` の無効化、負の `deltaTime` の0扱いを実装。
- `lingerSeconds` による視界外猶予、直接可視時の猶予リセット、対象リストから消えたIdの状態破棄を実装。
- `IsVisible(int targetId)` と `Clear()` を実装。
- `UnityEngine` は未使用。既存ファイル、asmdef、`.meta` は変更/作成していない。

## テスト結果

- 追加テスト数: 15 件。
- Roslyn 直接コンパイル: 緑。
  - `VisionRevealModel.cs` と `VisionRevealModelTests.cs` を Unity 同梱 NUnit DLL 参照付きでコンパイル成功。
- Unity EditMode テスト: 未実行。
  - 統合時に Claude 側で Unity Editor から一括実行予定。

## 残課題

- Unity Editor 上で `VisionRevealModelTests` を含む EditMode テストを実行して全緑確認。
- Unity 起動後に `Assets/_Project/Scripts/Vision` と新規 `.cs` の `.meta` が生成された場合、必要に応じてコミット対象に含める。
- 後続タスクで `FogOfWarDirector`、Renderer/頭上UI/ミニマップ表示の結線を行う。
