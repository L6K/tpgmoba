# 18: RiftEventModel（次元リフト・イベントの純ロジック）

> 「次元リフト」(中央コアが時々開く裂け目を確保すると近道開通/全体バフ)のロジック部分。
> **純 C#・Unity 非依存・EditMode テストのみ**。出現タイミング・確保進捗・効果選択を純関数で管理する。
> ポータルの見た目/近道開通/バフ適用/在圏判定は Claude 側(Unity)が結線する。

## 対象ファイル（新規）
- 実装: `Assets/_Project/Scripts/GameModes/RiftEventModel.cs`
- テスト: `Assets/_Project/Tests/EditMode/RiftEventModelTests.cs`

## 名前空間・規約
- `namespace Enigma.GameModes`（既存 `ObjectiveSpawnTimerModel`/`ObjectiveBuffModel` と同所）
- 標準 C# 規約・**UnityEngine 参照禁止**（完全 plain C#）。時間は呼び出し側から `now`/`dt` を渡す。

## 確定仕様（曖昧さゼロ）

### enum `RiftState`
`Dormant, Warning, Open, Captured, Cooldown`

### enum `RiftEffect`
`None, Shortcut, TeamVision, TeamHaste`
（出現ごとに `Shortcut → TeamVision → TeamHaste → Shortcut …` と循環。0回目=Shortcut）

### struct `RiftStatus`（readonly struct, 各 Tick 後の状態スナップショット）
- `RiftState State`
- `float SecondsToNextChange` … 次の状態遷移までの目安秒（Warning/Open/Captured/Cooldown 各フェーズの残り。Dormant は Open までの残り）
- `int CapturingTeam` … 現在確保を進めているチーム（-1=無し/拮抗）
- `float CaptureProgress01` … 確保進捗 0..1（Open 中のみ意味を持つ）
- `RiftEffect ActiveEffect` … Captured 中に有効な効果（それ以外は None）
- `int OwnerTeam` … Captured 中の確保チーム（それ以外は -1）

### class `RiftEventModel`
コンストラクタ
`RiftEventModel(float firstOpenAt = 120f, float warningLead = 10f, float openWindow = 30f, float captureSeconds = 6f, float effectDuration = 45f, float cooldown = 90f)`
- 不正値(<=0)は各既定へフォールバック。`warningLead` は `firstOpenAt` 未満にクランプ。

メソッド
- `RiftStatus Tick(float now, float dt, int presentTeam)`
  - `presentTeam`: 今まさにリフト圏内に**単独で**居るチーム（0/1）。誰も居ない/両軍居る(拮抗)場合は -1。
  - 状態機械:
    - **Dormant**: `now >= openTime - warningLead` で Warning へ。
    - **Warning**: `now >= openTime` で Open へ（CaptureProgress=0）。
    - **Open**: `presentTeam` が 0 or 1 のとき `CaptureProgress01 += dt / captureSeconds`（CapturingTeam=presentTeam）。-1 のときは進捗据え置き(減衰なし)・CapturingTeam=-1。`CaptureProgress01 >= 1` で Captured(OwnerTeam=確保チーム, ActiveEffect=循環で選択)へ。`openWindow` を経過しても未確保なら Cooldown へ（取り逃し）。
    - **Captured**: `effectDuration` 経過で Cooldown へ。
    - **Cooldown**: `cooldown` 経過で次 openTime を設定し Dormant へ（出現回数++）。
  - 戻り値に現在の `RiftStatus` を返す。
- `int OpenCount { get; }` … これまでに Open 状態に入った回数（効果循環の確認用）。
- `void Reset()` … 初期化（試合再開用）。

### 補足
- 効果は **Captured に入った瞬間**に `OpenCount`（または確保回数）に基づき `RiftEffect`(循環)を確定。
- `SecondsToNextChange` は負にしない（0 下限）。
- 在圏の空間判定（誰が圏内か）はモデルの責務外（呼び出し側が presentTeam を渡す）。

## テスト要件（NUnit、許容誤差 1e-3）
1. Dormant→Warning→Open の遷移時刻（firstOpenAt/warningLead）。
2. Open 中、単独チーム在圏で captureSeconds で確保完了→Captured・OwnerTeam一致。
3. 拮抗(-1)で進捗据え置き（減らない・確保されない）。
4. openWindow 経過で未確保なら Cooldown（取り逃し）。
5. Captured→effectDuration→Cooldown→cooldown→次 Dormant、OpenCount が増える。
6. 効果循環: 1回目 Shortcut, 2回目 TeamVision, 3回目 TeamHaste, 4回目 Shortcut。

## 完了条件
- 2ファイル作成。`Enigma.GameModes`・Unity 非依存。EditMode テスト緑（件数を report）。
- 報告 `codex-tasks/18-rift-event-model-report.md`。範囲外の既存改変なし（所見は report へ）。

## 補足（Claude 側・対象外）
- 結線: 中央コア付近にリフトのポータル見た目（Warning=予兆/Open=渦）、在圏判定→presentTeam、Captured で Shortcut=近道コライダー開通 / TeamVision=`FogOfWarDirector.SetExternalSource` 全域 / TeamHaste=移動速度バフ。森倒壊演出の技術を流用可。
- 課金: 従量課金・画像生成は使用禁止（純コードのみ）。
