using UnityEngine;
using UnityEngine.Serialization;

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
        [FormerlySerializedAs("displayName")]
        public string DisplayName = "プレイヤー名";

        [FormerlySerializedAs("level")]
        public int Level = 1;

        // null の場合はデフォルトアイコンを使用
        [FormerlySerializedAs("avatar")]
        public Texture2D Avatar;

        [Header("ステータス")]
        [FormerlySerializedAs("status")]
        public FriendStatus Status = FriendStatus.Offline;

        // ステータスに応じた表示文字列
        public string StatusLabel => Status switch
        {
            FriendStatus.Online  => "ロビー待機中",
            FriendStatus.InGame  => "ゲーム中",
            FriendStatus.InQueue => "マッチング中",
            _                    => "オフライン",
        };

        public bool IsOnline => Status != FriendStatus.Offline;
    }
}
