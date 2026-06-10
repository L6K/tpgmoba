using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Enigma.Data;

namespace Enigma.Character
{
    [CreateAssetMenu(fileName = "CharacterDatabase", menuName = "Enigma/Character Database")]
    public class CharacterDatabase : ScriptableObject
    {
        public List<CharacterData> characters = new();

        public int TotalCount  => characters.Count(c => c != null);
        public int OwnedCount  => characters.Count(c => c != null && CharacterOwnership.IsOwned(c));

        /// <summary>所持→未所持の順、同区分内はロール順（enum順）→displayName 順でソートした一覧を返す</summary>
        public List<CharacterData> GetSorted() =>
            characters
                .Where(c => c != null)
                .OrderByDescending(c => CharacterOwnership.IsOwned(c))
                .ThenBy(c => (int)c.role)
                .ThenBy(c => c.displayName)
                .ToList();
    }
}
