---
layout: post
title: "プロジェクト概要 — Enigma とこれまでの歩み"
tags: [概要, アーキテクチャ, 設計]
---

**Enigma(エニグマ)** は Fantasy×SF テーマの 3v3 3D MOBA。LoL のレーン戦と FF14 のレイドギミックを掛け合わせた対戦ゲームを目指して、Unity 6 (URP) で開発している。この記事では現時点の全体像と、設計で拘っているポイントをまとめる。

## ゲームの骨格

- **3v3 / 2レーン+ジャングル**: TOP・BOT・JUNGLER の3ロール。円形マップの中央を川が貫き、左右に青/赤のベースが向かい合う
- **FF14 準拠の操作系**: WASD 移動+ドラッグでカメラ回転。スキルは AoE / 対象指定 / 方向指定 の3種で、LoL 式のクイックキャスト3モードを設定で切替可能
- **中立ボス「エニグマ・コア」**: 森の中央に鎮座し、FF14 の絶コンテンツ風に予兆円→扇範囲→頭割りのローテーションを回す。討伐するとチームバフ
- **ゲームループ完成済み**: ホーム → マッチング → Valorant 風キャラピック → 試合(ミニオン・タワー・ジャングル・アイテムショップ・XP/レベル) → タイタン撃破 → リザルト

## 設計で拘っているポイント

### 1. ロジックは plain C#、MonoBehaviour は Humble Object

Unity のテストしづらさは、ロジックを MonoBehaviour に書くことから始まる。Enigma ではロジックを**依存ゼロの plain C# クラス**に切り出し、MonoBehaviour は「Unity API とのグルー層」に徹する方針を最初に固めた。

例えば攻撃の3段階モーション(準備→攻撃→後隙)は `AttackMotion` という純粋なステートマシンで、Unity に一切依存しない:

```csharp
public bool TryBegin(float windupSeconds, float recoverySeconds, Action onStrike)
{
    if (_phase == AttackPhase.Windup) return false; // 準備中はキャンセル不可

    _phase           = AttackPhase.Windup;
    _timer           = windupSeconds;
    _recoverySeconds = recoverySeconds;
    _onStrike        = onStrike;
    return true;
}
```

「後隙だけ移動でキャンセルできる」という格ゲー的な仕様も `CancelRecovery()` 一つで表現でき、EditMode テストでフレーム単位の挙動を検証できる。

### 2. サービスはインターフェース+コンストラクタ注入

`PlayerPrefs` や `Screen` のような Unity API は直接呼ばず、`ISaveStore` / `ISystemSettingsApplier` といった抽象でラップする。composition root(`GameServices`)で本物を組み立て、テストではフェイクを注入する。設定・ガチャ・マッチング・試合状態・チームバフまで全サービスがこの形で、**EditMode テストは現在 157 件**。

### 3. マップはコードで再生成可能

マップ全体(地形・レーン・タワー・ジャングル・プレハブ群)はエディタスクリプト `BuildAetherRiftMap.Execute()` が**毎回ゼロから決定論的に生成**する。手作業のシーン編集を残さないことで、「壊れたら作り直す」が常に効く。プレハブも毎回 `DeleteAsset` してから再生成する(これは後述の苦い教訓から)。

## ビジュアルの方向性

Hoyoverse 作品のようなトゥーン表現を目標に、HDRP から URP へ移行して自作の `Enigma/Toon` シェーダー(セルランプ+リム+輪郭線)で統一。プレイヤーモデルにはユニティちゃん(UCL2.02)を採用している。マップ素材は Kenney の CC0 アセット。

<figure>
  <img src="{{ '/assets/img/MinimapBg.png' | relative_url }}" alt="円形マップの真上からのベイク画像">
  <figcaption>円形マップ「Aether Rift」を真上から見た図。中央の川・環状レーン・左右のベースが見える</figcaption>
</figure>

## これまでの主なマイルストーン

| 時期 | 内容 |
|---|---|
| 序盤 | ホーム画面(LoL風ナビ+Valorant風設定)、ガチャ、プロフィール |
| 中盤 | ゲーム画面(FF14操作系・スキル3種・キャスト3モード)、HDRP→URP移行、ユニティちゃん導入 |
| 中盤 | マッチング→キャラピック→試合→リザルトのループ完成、タワー防衛、ボスギミック |
| 直近 | 円形マップ化、ジャングルキャンプとXP、ゴールド+アイテムショップ、ミニマップ、攻撃モーション |

## 現在の課題

- 敵チームのレーナー AI が未実装(ミニオンとタワーのみが抵抗勢力)
- サウンドが一切ない
- ボスギミックのフェーズ2以降が未設計
- ユニティちゃんの攻撃アニメーションクリップが未統合(現状はプロシージャルなランジ演出)

このあたりを少しずつ潰していく予定。次の記事では、UI/UX をプロダクト品質へ引き上げた最新ラウンドの裏側を書く。
