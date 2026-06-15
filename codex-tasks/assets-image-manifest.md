# 画像アセット マニフェスト（生成は 5.5、Codex不可）

> 画像生成は **5.5（画像生成可）** が担当。Codex（コード専用）は不可。coplay の生成系（クレジット消費）は使用禁止。
> 生成物は **`codex-tasks/assets-out/` に出力**（ステージング）。その後 Claude が `Assets/...` へ取り込み＋UI結線＋実機確認する。
> 詳細プロンプトは別紙 `D:\Document\smite\image-prompts-icons.md`（共通スタイル＋効果5＋スキル18）も参照。

## 共通仕様
- 透過PNG、正方形、**512×512**（縮小利用）。Hoyoverse風セミフラット・トゥーン、太く読めるシルエット、ダークUI(#0A0C16)で映える発光。枠/文字なし。
- 出力先（ステージング）: `D:\Document\smite\smite\codex-tasks\assets-out\`
- ファイル名は下表の「ファイル名」厳守（Claude 側の取り込み規約に使う）。

## A. ステータス効果アイコン 5枚 ★最優先（D のCC/バフバー）
| ファイル名 | 用途 | 取り込み先 | 色味 |
|---|---|---|---|
| `fx_stun.png` | スタン | Assets/_Project/UI/Icons/ | 黄/ゴールド（星＋渦） |
| `fx_root.png` | ルート | 同上 | 緑（絡みつく根/蔓） |
| `fx_slow.png` | スロウ | 同上 | ティール（カタツムリ＋下向き矢印） |
| `fx_shield.png` | シールド | 同上 | エネルギーブルー（六角バリア） |
| `fx_heal.png` | 回復 | 同上 | 緑→白（十字＋粒子） |

## B. キャラ別スキルアイコン 18枚 ★中（HUDのQ/E/Rスロット）
6キャラ×Q/E/R。キャラごとにテーマ色で統一。プロンプトは別紙B章参照。ファイル名規約:
`skill_{charId}_{Q|E|R}.png`（例: `skill_zeph_Q.png`）。取り込み先 `Assets/_Project/UI/Icons/`。
- zeph(紫): Q=アーケインボルト / E=プラズマフィールド / R=オーバードライブ
- garon(金): Q=大剣薙ぎ / E=大地の咆哮 / R=断罪の一閃
- veil(藍): Q=影手裏剣 / E=影爆 / R=暗殺刻印
- rin(橙): Q=ピアスショット / E=スキャッターボム / R=レールガン
- nova(空): Q=スターレイ / E=重力井戸 / R=超新星(味方回復ult・回復モチーフ)
- thorne(緑): Q=チェーンフック / E=震撃波 / R=捕食プロトコル

## C. キャラポートレート 5枚 ★中（キャラ選択/HUD。zephは既存）
| ファイル名 | キャラ | 取り込み先 | 既存 |
|---|---|---|---|
| `PortraitGaron.png` | ガロン(重装騎士/金) | Assets/_Project/UI/Textures/ | — |
| `PortraitVeil.png` | ヴェイル(アサシン/藍) | 同上 | — |
| `PortraitRin.png` | リン(マークスマン/橙) | 同上 | — |
| `PortraitNova.png` | ノヴァ(サポ/空) | 同上 | — |
| `PortraitThorne.png` | ソーン(JG機械化/緑) | 同上 | — |
| (PortraitZeph.png) | ゼフ | （既存・統一の参考に） | あり |
- 仕様: バストアップ、キャラテーマ色、`PortraitZeph.png` と画角/タッチを揃える。正方形512〜1024、透過 or 単色背景。characters.json の theme 記述を反映。

## D. 将来分（次の大きなタスク確定後に追加）
- アイテムアイコン（攻撃/防御/機動/CD/対CC ＋ 既存6種）
- レリックアイコン（ブリンク/浄化/シールド/加速/ワード）※候補③採用時
- ピンアイコン（注意/集合/攻撃）※候補④採用時
- これらは該当タスク着手時にこのマニフェストへ追記する。

## 取り込み・結線（Claude 側・参考）
- 効果アイコン → バフ/デバフ表示UI（D の HUD バフバー、未実装なら併せて作成）。
- スキルアイコン → `GameHudController.UpdateSkillIcon` を `SkillDefinition.Icon`（新規 SerializeField）参照に変更し per-skill 表示。インポータで `skill_{id}_{slot}.png` を自動結線。
- ポートレート → キャラ選択 / HUD ポートレート枠。
