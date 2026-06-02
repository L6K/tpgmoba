using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Enigma.UI
{
    [CreateAssetMenu(fileName = "FriendDatabase", menuName = "Enigma/Friend Database")]
    public class FriendDatabase : ScriptableObject
    {
        [SerializeField] List<FriendData> friends = new();

        /// <summary>オンライン優先でソートしたフレンド一覧</summary>
        public IReadOnlyList<FriendData> GetSorted() =>
            friends.OrderByDescending(f => f.IsOnline)
                   .ThenBy(f => f.displayName)
                   .ToList();

        public int TotalCount  => friends.Count;
        public int OnlineCount => friends.Count(f => f.IsOnline);
    }
}
