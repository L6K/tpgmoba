using UnityEngine;

namespace Enigma.UI
{
    public enum FriendStatus
    {
        Offline,        // オフライン
        Online,         // オンライン（ロビー待機中）
        InGame,         // ゲーム中
        InQueue,        // マッチング中
    }

    [CreateAssetMenu(fileName = "Friend_", menuName = "Enigma/Friend Data")]
    public class FriendData : ScriptableObject
    {
        [Header("基本情報")]
        public string displayName = "プレイヤー名";
        public int    level       = 1;
        public Texture2D avatar;  // null の場合はデフォルトアイコンを使用

        [Header("ステータス")]
        public FriendStatus status = FriendStatus.Offline;

        // ステータスに応じた表示文字列
        public string StatusLabel => status switch
        {
            FriendStatus.Online  => "ロビー待機中",
            FriendStatus.InGame  => "ゲーム中",
            FriendStatus.InQueue => "マッチング中",
            _                    => "オフライン",
        };

        public bool IsOnline => status != FriendStatus.Offline;
    }
}
