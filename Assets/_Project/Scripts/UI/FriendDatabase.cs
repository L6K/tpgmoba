using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Serialization;

namespace Enigma.UI
{
    [CreateAssetMenu(fileName = "FriendDatabase", menuName = "Enigma/Friend Database")]
    public class FriendDatabase : ScriptableObject
    {
        [FormerlySerializedAs("friends")]
        [SerializeField] private List<FriendData> _friends = new();

        /// <summary>オンライン優先でソートしたフレンド一覧</summary>
        public IReadOnlyList<FriendData> GetSorted() =>
            _friends.OrderByDescending(f => f.IsOnline)
                    .ThenBy(f => f.DisplayName)
                    .ToList();

        public int TotalCount  => _friends.Count;
        public int OnlineCount => _friends.Count(f => f.IsOnline);
    }
}
