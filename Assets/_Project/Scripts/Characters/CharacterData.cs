using UnityEngine;
using UnityEngine.Serialization;

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

        [Header("所持状態")]
        // 暫定フラグ。SQLite 導入後は owned_chars テーブルへ移行すること
        [FormerlySerializedAs("ownedByDefault")]
        public bool OwnedByDefault = false;

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
