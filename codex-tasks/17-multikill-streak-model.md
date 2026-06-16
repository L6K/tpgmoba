# 17: MultiKillStreakModel（マルチキル/連続キルの純ロジック）

> 「キル演出の格上げ」のロジック部分。**純 C#・Unity 非依存・EditMode テストのみ**。
> ダブルキル〜ペンタキル(短時間連続)と、キルストリーク(死なずに連続キル)・シャットダウン報酬を
> 純関数モデルで判定する。アナウンス表示/音/ゴールド付与は Claude 側が結線する（Unity 非依存）。

## 対象ファイル（新規）
- 実装: `Assets/_Project/Scripts/Combat/MultiKillStreakModel.cs`
- テスト: `Assets/_Project/Tests/EditMode/MultiKillStreakModelTests.cs`

## 名前空間・規約
- `namespace Enigma.Combat`
- 標準 C# 規約・**UnityEngine 参照禁止**（完全 plain C#）。時間は呼び出し側から `now` を渡す。

## 確定仕様（曖昧さゼロ）

### enum `MultiKill`
`None, Double, Triple, Quadra, Penta`

### enum `Streak`
`None, Spree, Rampage, Unstoppable, Dominating, Godlike`

### struct `KillResult`（readonly struct, RegisterKill の戻り値）
- `MultiKill MultiKill` … 今回の連続キル段階（時間窓内の累積で決まる）
- `int MultiKillCount` … 時間窓内の連続キル数（1,2,3,...）
- `Streak Streak` … この killer の現在の連続キル段階（死ぬまでの累積）
- `int StreakCount` … 連続キル数（死なずに）
- `bool IsShutdown` … この**被害者**が連続キル中(Spree以上)を止めた場合 true
- `Streak VictimStreakEnded` … 被害者が中断された連続キル段階（IsShutdown=false なら None）

### class `MultiKillStreakModel`
コンストラクタ `MultiKillStreakModel(float multiKillWindowSeconds = 10f)`（`<=0` は 10）。

- `KillResult RegisterKill(string killerId, string victimId, float now)`
  - killer の **マルチキル**: 直前キルから `multiKillWindowSeconds` 以内なら窓内カウント++、超過/初回なら 1 にリセット。カウント→段階: 1=None,2=Double,3=Triple,4=Quadra,5以上=Penta(5でクランプ表示)。
  - killer の **ストリーク**: 連続キル数++（死ぬまで累積）。数→段階: 0-2=None, 3-4=Spree, 5-6=Rampage, 7-8=Unstoppable, 9-10=Dominating, 11以上=Godlike。
  - **シャットダウン**: victim の現在ストリークが Spree 以上なら `IsShutdown=true`、`VictimStreakEnded=その段階`。その後 victim のストリークは 0 にリセット。
  - 文字列IDは null/空なら "Unknown" 扱い。自分自身のキル(killer==victim)は無視して `KillResult` 既定(None)を返す。
- `void RegisterDeath(string playerId, float now)` … その playerId のマルチキル窓とストリークを 0 リセット（被killでなく環境死など、シャットダウン無しで状態を消すため）。
- `int StreakCountOf(string playerId)` … 現在ストリーク数（テスト/賞金計算用）。
- `void Clear()` … 全リセット。

### 補足
- `RegisterKill` 内でシャットダウンを処理した後に victim のストリークをリセットすること（`RegisterDeath` は呼ばない＝二重リセットしない設計でよいが、結果が同じなら可）。
- マルチキルとストリークは別カウンタ（マルチキルは時間窓、ストリークは死ぬまで）。

## テスト要件（NUnit、許容誤差不要=整数/enum）
1. 窓内に2,3,4,5連続 → Double/Triple/Quadra/Penta。6連続でも Penta(クランプ)。
2. 窓を超えた次キルはマルチキルが None(=1) に戻る。
3. ストリーク段階: 3→Spree, 5→Rampage, 7→Unstoppable, 9→Dominating, 11→Godlike。
4. シャットダウン: Spree中の相手を倒すと IsShutdown=true・VictimStreakEnded=Spree、以後その相手の StreakCountOf=0。
5. `RegisterDeath` でマルチキル窓・ストリークがリセット。
6. killer==victim は無視。null/空ID は "Unknown"。

## 完了条件
- 上記2ファイル作成。`Enigma.Combat`・Unity 非依存。EditMode テスト緑（件数を report に記載）。
- 結果報告 `codex-tasks/17-multikill-streak-model-report.md`。
- 範囲外の既存ファイル改変はしない（所見は report へ）。

## 補足（Claude 側・対象外）
- 結線: `HealthComponent` の死亡/帰属イベント → `RegisterKill`。`MultiKill`/`Streak` をセンターアナウンス＋ボイス＋キルフィードに、`IsShutdown` を被害者の賞金(シャットダウンゴールド)に反映。`RegisterDeath` は環境死/オーバータイム減衰死などに。
- 課金: 従量課金サービス・画像生成は使用禁止（純コードのみ）。
