using UnityEngine;
using UnityEngine.Serialization;
using Enigma.Ability;

namespace Enigma.Character
{
    public enum CharacterRole
    {
        Tank,
        Fighter,
        Mage,
        Marksman,
        Support,
    }

    [CreateAssetMenu(fileName = "Char_", menuName = "Enigma/Character Data")]
    public class CharacterData : ScriptableObject
    {
        [Header("識別情報")]
        // 将来 SQLite owned_chars.char_id と対応するキー
        [FormerlySerializedAs("charId")]
        public string CharId = "";

        [FormerlySerializedAs("displayName")]
        public string DisplayName = "キャラクター名";

        [Header("ロール・説明")]
        [FormerlySerializedAs("role")]
        public CharacterRole Role = CharacterRole.Fighter;

        [TextArea]
        [FormerlySerializedAs("description")]
        public string Description = "";

        [Header("見た目")]
        [FormerlySerializedAs("themeColor")]
        public Color ThemeColor = new Color(46f / 255f, 107f / 255f, 242f / 255f);

        // null の場合は ThemeColor 背景 + 頭文字で代替表示
        [FormerlySerializedAs("icon")]
        public Texture2D Icon;

        [Header("スキル（Q/W/E/R）")]
        public SkillDefinition[] Skills = new SkillDefinition[4];

        [Header("所持状態")]
        // 暫定フラグ。SQLite 導入後は owned_chars テーブルへ移行すること
        [FormerlySerializedAs("ownedByDefault")]
        public bool OwnedByDefault = false;

        // characters.json を正としてインポータが書き込むステータス（既存フィールドは不変、ここから追加）
        [Header("ステータス（characters.json 由来）")]
        // CharacterRole enum に対応しない自由記述ロール（"ジャングラー/ブルーザー" 等）も保持するための原文
        public string RoleLabelRaw = "";
        [TextArea]
        public string Theme = "";
        public float BaseHp = 200f;
        public float HpPerLevel = 0f;
        public float MoveSpeed = 5.5f;
        public float AttackDamage = 15f;
        public float AttackRange = 12f;
        public float AttackCooldown = 1.5f;
        public Color TintColor = Color.white;
        // モデル名（プレハブ解決のキー）。Texture2D Icon とは別物
        public string ModelName = "";

        [Header("試合用 3D モデル（インポータが結線）")]
        // ModelName が "UnityChan"/空 のキャラは null（既存 UnityChanModel を維持）。
        // それ以外は Champ_{ModelName}.fbx のプレハブをエディタインポータが結線する。
        public GameObject ModelPrefab;
        // FBX サブアセットの AnimationClip。ランタイムでは FBX サブアセットへアクセスできないため事前結線が必要
        public AnimationClip IdleClip;
        public AnimationClip WalkClip;
        // Run 系クリップ。Move 状態で WalkClip より優先使用される（null なら Walk へフォールバック）
        public AnimationClip RunClip;
        // 攻撃ワンショット用クリップ。AA/スキル発射時に LocomotionClipSwitcher.PlayAttack で再生する
        public AnimationClip AttackClip;
        // AA コンボ用の複数攻撃クリップ（順繰り再生）。空なら AttackClip 単発へフォールバック
        public AnimationClip[] AttackClips;
        // 棒立ち回避用のアイドルバリアント（ベース Idle 以外）。null/空可
        public AnimationClip[] IdleVariantClips;
        // FBX リネームで内部のテクスチャ参照が切れるため、ボディテクスチャも明示結線する
        public Texture2D BodyTexture;

        /// <summary>ロールの日本語表示名</summary>
        public string RoleLabel => Role switch
        {
            CharacterRole.Tank      => "タンク",
            CharacterRole.Fighter   => "ファイター",
            CharacterRole.Mage      => "メイジ",
            CharacterRole.Marksman  => "マークスマン",
            CharacterRole.Support   => "サポート",
            _                       => Role.ToString(),
        };
    }
}
