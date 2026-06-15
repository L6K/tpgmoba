# Codex タスク 03: VisionRevealModel（視界判定の純ロジック）

> このタスクは **Enigma**（Unity 6.3 URP の 3D MOBA、リポジトリ `D:\Document\smite\smite`、ブランチ `develop`）の一部です。
> あなたはこの会話の文脈を持ちません。**この仕様だけ**を根拠に実装してください。AGENTS.md のルール（従量課金禁止・命名規約）に従うこと。

## 背景・ゴール

提案E「視界（Fog of War）」の核となる **plain C# モデル `VisionRevealModel`** と **EditMode テスト**を作成する。
味方の視界源（位置＋視界半径）と対象ユニット（敵/中立の位置）から「どの対象が可視か」を計算するだけの純ロジック。
**MonoBehaviour・Unity シーン・描画・既存ファイル改変は一切しない**（レンダリングのトグルは別担当=Claude 側が後で結線する）。

## 厳守事項（スコープ）

- 新規作成するファイルは **2つだけ**:
  1. `D:\Document\smite\smite\Assets\_Project\Scripts\Vision\VisionRevealModel.cs`
  2. `D:\Document\smite\smite\Assets\_Project\Tests\EditMode\VisionRevealModelTests.cs`
- `Vision` フォルダは新規作成してよい（`.meta` は作らない＝Unity が自動生成）。
- 既存ファイル・asmdef は**変更しない**。
- **Unity API（UnityEngine 名前空間）を使わない**。純 C# のみ（`System` / `System.Collections.Generic` は可）。距離計算は自前（`Mathf` 不可、`System.Math` 可）。これでテストが Unity 非依存に走る。

## 命名・規約

- 名前空間: `Enigma.Vision`
- プライベートフィールドは `_camelCase`、public API はプロパティ/メソッド PascalCase。
- テストの名前空間: `Enigma.Tests`、`using NUnit.Framework;`、`using Enigma.Vision;`、クラス `public sealed class VisionRevealModelTests`。
- コメントは WHY が非自明な箇所のみ（日本語可）。

## API 仕様

### 入力用の軽量 struct（同ファイル内に定義）
```csharp
// 視界源（味方ユニット等）。XZ 平面の位置と視界半径。
public readonly struct VisionSource
{
    public readonly float X;
    public readonly float Z;
    public readonly float Radius;
    public VisionSource(float x, float z, float radius) { X = x; Z = z; Radius = radius; }
}

// 可視判定の対象（敵/中立ユニット）。安定した整数 Id で識別する。
public readonly struct VisionTarget
{
    public readonly int Id;
    public readonly float X;
    public readonly float Z;
    public VisionTarget(int id, float x, float z) { Id = id; X = x; Z = z; }
}
```

### モデル本体
```csharp
public sealed class VisionRevealModel
{
    // linger: 視界から外れた後、可視を維持する猶予秒（0=即時に隠す）。負値は 0 にクランプ。
    public VisionRevealModel(float lingerSeconds = 0f);

    // sources のいずれかの半径内に入っている target を「直接可視」とする。
    // 直接可視なら可視＆linger をリセット。直接可視でなければ linger を deltaTime で減衰し、
    // 残っている間は可視を維持。targets に現れない Id の状態は破棄する。
    // 戻り値: 今回可視な target Id の集合（読み取り専用）。
    public IReadOnlyCollection<int> Update(
        IReadOnlyList<VisionSource> sources,
        IReadOnlyList<VisionTarget> targets,
        float deltaTime);

    // 直近の Update 結果に基づき、指定 Id が可視かを返す。
    public bool IsVisible(int targetId);

    // 全状態をクリア（試合リセット等）。
    public void Clear();
}
```

## 確定仕様（曖昧さを残さない）

1. **直接可視判定**: target が「ある source の半径内」にあるか。2D（XZ）ユークリッド距離で `dist <= source.Radius` なら可視。**境界上（dist == Radius）は可視に含める**。計算は2乗比較（`dx*dx+dz*dz <= r*r`）でよい（平方根不要）。
2. 複数 source のうち1つでも半径内なら直接可視。
3. **linger（猶予）**: 直接可視になった target は「可視」かつ残り猶予 = `lingerSeconds` にセット。直接可視でない target は残り猶予を `deltaTime` 分減らし、`> 0` の間は可視を維持、`<= 0` で不可視。`lingerSeconds == 0` なら直接可視でなくなった瞬間に不可視。
4. `deltaTime < 0` は 0 として扱う。
5. **状態の掃除**: `Update` に渡された `targets` に含まれない Id の内部状態（猶予）は破棄する（メモリリーク防止）。
6. コンストラクタ引数 `lingerSeconds` が負なら 0 にクランプ。
7. `Radius <= 0` の source は誰も可視にしない（実質無効）。
8. `IsVisible` は最後の `Update` の結果に基づく（`Update` 前は全て false）。
9. 戻り値の集合は呼び出しごとに新規でも内部参照でもよいが、呼び出し側が列挙する前提（読み取り専用型で返す）。
10. 浮動小数比較はテスト側で許容誤差を使う（`Assert.AreEqual(expected, actual, 1e-4f)` 等）。距離の境界テストはちょうどの値で可視判定を確認。

## テスト要件（`VisionRevealModelTests`、最低14ケース）

- source 半径内の target は可視 / 半径外は不可視
- 境界上（dist == Radius）は可視
- 複数 source: いずれか1つの半径内なら可視
- 半径外の複数 source では不可視
- `Radius <= 0` の source は可視にしない
- linger=0: 視界から外れた次フレームで即不可視
- linger>0: 視界から外れても猶予中は可視、`deltaTime` 累積で猶予超過後に不可視
- linger 中に再び直接可視になったら猶予がリセットされる
- `targets` から消えた Id の状態が破棄される（再登場時に猶予が残っていない＝即判定）
- `deltaTime < 0` は 0 扱い（猶予が減らない）
- `Clear` 後は全て不可視
- `IsVisible`: `Update` 前は false、`Update` 後は結果通り
- 空の sources/targets でも例外を投げない
- コンストラクタ負 linger は 0 にクランプ（視界外で即不可視）

## 完了条件

1. 上記2ファイルが作成され、論理的に整合している。
2. 可能なら Roslyn / `dotnet` でコンパイル確認（Unity 非依存）。Unity EditMode 実機テストは Claude 側が統合時に一括実行する（必須ではない）。
3. 報告は **変更ファイルと要点の箇条書きのみ**。コード全文の貼り付け不要。
4. **結果報告を `D:\Document\smite\smite\codex-tasks\03-vision-reveal-model-report.md` に書き出す**（作成ファイル一覧・要点・テスト結果の緑/赤と件数・残課題）。

## 連携メモ（変更対象外・参考）

- 後で `FogOfWarDirector`(MonoBehaviour) が一定間隔で味方視界源（味方チャンピオン/ミニオン/タワーの位置＋視界半径）と敵動的ユニットを集め、`Update` を呼ぶ。戻り値の可視 Id 集合に基づき、敵の Renderer・頭上UI・ミニマップ表示をトグルする。
- 対象の Id は Claude 側で `GameObject.GetInstanceID()` を割り当てる想定（このモデルは Id を不透明な int として扱うだけでよい）。
- 地形遮蔽（壁/茂みで視線が遮られる）は将来の拡張。このモデルは半径ベースのみ扱う。
- だからこのモデルは Unity の Time/座標型に依存せず、渡された値だけで完結させること。
