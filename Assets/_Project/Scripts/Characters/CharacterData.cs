using UnityEngine;

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
        public string charId = "";
        public string displayName = "キャラクター名";

        [Header("ロール・説明")]
        public CharacterRole role = CharacterRole.Fighter;

        [TextArea]
        public string description = "";

        [Header("見た目")]
        public Color themeColor = new Color(46f / 255f, 107f / 255f, 242f / 255f);
        // null の場合は themeColor 背景 + 頭文字で代替表示
        public Texture2D icon;

        [Header("所持状態")]
        // 暫定フラグ。SQLite 導入後は owned_chars テーブルへ移行すること
        public bool ownedByDefault = false;

        /// <summary>ロールの日本語表示名</summary>
        public string RoleLabel => role switch
        {
            CharacterRole.Tank      => "タンク",
            CharacterRole.Fighter   => "ファイター",
            CharacterRole.Mage      => "メイジ",
            CharacterRole.Marksman  => "マークスマン",
            CharacterRole.Support   => "サポート",
            _                       => role.ToString(),
        };
    }
}
