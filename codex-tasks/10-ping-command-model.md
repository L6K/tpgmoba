# Codex タスク 10: PingCommandModel（ピンのデータ層・情報戦）

> Enigma(`D:\Document\smite\smite`、ブランチ develop)。AGENTS.md 準拠。文脈なし・この仕様のみ。
> ピン(注意/集合/攻撃)の発行・スパム抑制・有効ピン管理＋ラジアル選択(角度→種別)の純ロジック。UI/入力/描画は Claude 側。

## 対象ファイル(この2つのみ・新規)
1. `D:\Document\smite\smite\Assets\_Project\Scripts\GameModes\PingCommandModel.cs`
2. `D:\Document\smite\smite\Assets\_Project\Tests\EditMode\PingCommandModelTests.cs`
- `.meta` 作らない。既存ファイル/asmdef 変更しない。**UnityEngine 不使用**(System / System.Collections.Generic のみ。`Mathf`不可・`System.Math`可)。
- 名前空間 `Enigma.GameModes`。テストは `Enigma.Tests`。private は `_camelCase`。

## API
```csharp
public enum PingType { Danger, OnMyWay, Attack } // 注意 / 集合(向かっている) / 攻撃

public readonly struct ActivePing
{
    public readonly PingType Type;
    public readonly float X;
    public readonly float Z;
    public readonly float ExpiresAt;
    public ActivePing(PingType type, float x, float z, float expiresAt) { Type=type; X=x; Z=z; ExpiresAt=expiresAt; }
}

public sealed class PingCommandModel
{
    // minIntervalSeconds: 連打抑制(この間隔未満の連続発行は弾く)。displaySeconds: ピンの表示寿命。
    public PingCommandModel(float minIntervalSeconds = 0.5f, float displaySeconds = 4f);

    // ピン発行。前回発行から minIntervalSeconds 未満なら false(抑制)。成功なら true で有効ピンに追加。
    public bool TryIssue(PingType type, float x, float z, float now);

    // 期限切れピンを除去する(now 経過を反映)。
    public void Tick(float now);

    // 現在有効なピン一覧(読み取り専用、追加順)。
    public System.Collections.Generic.IReadOnlyList<ActivePing> ActivePings { get; }

    public void Clear();

    // ラジアルメニューのカーソル方向(度)から選択するピン種別を返す。
    // 角度は「真上=0度、時計回りに増加(0..360)」。3分割: 上=Danger、右下=OnMyWay、左下=Attack。
    // 具体的には [330,360)∪[0,30)=…ではなく、3等分セクター(中心: 上=0/右下=120/左下=240)に最も近いものを返す。
    public static PingType SelectByAngle(float angleDegrees);
}
```

## 確定仕様
1. **TryIssue 抑制**: 直近の成功発行時刻を保持。`now - lastIssued < minIntervalSeconds` なら false で何もしない(時刻も更新しない)。初回(まだ発行なし)は常に許可。成功時 `lastIssued=now`、`ActivePing(type,x,z, now+displaySeconds)` を追加し true。
2. **Tick**: `ExpiresAt <= now` のピンを除去。`Tick` は ActivePings を縮める(無限増加防止)。
3. **ActivePings**: 追加順。期限切れは Tick または列挙時点で除外されていること(Tick で除去する設計で可)。
4. **SelectByAngle**: 角度を 0..360 に正規化(負や360超もmod)。3セクター中心 上=0°, 右下=120°, 左下=240° の最近傍を返す(各セクター幅120°: 境界は[300,60)=Danger, [60,180)=OnMyWay, [180,300)=Attack)。
5. コンストラクタ引数 `minIntervalSeconds`/`displaySeconds` は負値を 0 にクランプ。
6. 浮動小数比較はテストで許容誤差 1e-4f。

## テスト要件(最低13)
- 初回 TryIssue 成功、ActivePings に1件・正しい type/座標
- minInterval 未満の連続 TryIssue は false(2件目が増えない)
- minInterval 経過後は再び成功
- Tick で displaySeconds 経過後にピンが消える / 経過前は残る
- 複数ピンの個別期限切れ(古いものだけ消える)
- Clear で空
- SelectByAngle: 0°→Danger / 120°→OnMyWay / 240°→Attack
- SelectByAngle 境界: 60°→OnMyWay(境界は右側セクター) / 300°→Danger / 180°→Attack
- SelectByAngle: 負角(-30=330)→Danger / 360超(480=120)→OnMyWay
- コンストラクタ負値クランプ(minInterval=-1→0 で常に許可)

## 完了条件 / 報告
- 2ファイル作成・整合。可能なら Roslyn/dotnet コンパイル確認。EditMode 実機は Claude 側。
- 報告は箇条書き＋`codex-tasks\10-ping-command-model-report.md`。

## 連携メモ(変更対象外)
- Claude 側: Ctrl+左クリック押下でラジアルメニュー表示→押下中カーソル方向で `SelectByAngle` 判定→離して `TryIssue`(クリック地点のワールド座標 or ミニマップ座標)。`ActivePings` をワールド/ミニマップに描画、`Tick(Time.timeSinceLevelLoad)` を毎フレーム呼ぶ。
