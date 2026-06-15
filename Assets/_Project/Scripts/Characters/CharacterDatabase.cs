using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Serialization;
using Enigma.Data;

namespace Enigma.Character
{
    [CreateAssetMenu(fileName = "CharacterDatabase", menuName = "Enigma/Character Database")]
    public class CharacterDatabase : ScriptableObject
    {
        [FormerlySerializedAs("characters")]
        public List<CharacterData> Characters = new();

        public int TotalCount => Characters.Count(c => c != null);

        /// <summary>
        /// 所持→未所持の順、同区分内はロール順（enum 順）→DisplayName 順でソートした一覧を返す。
        /// ICharacterOwnership を引数で受け取ることで static 依存を排除する。
        /// </summary>
        public List<CharacterData> GetSorted(ICharacterOwnership ownership) =>
            Characters
                .Where(c => c != null)
                .OrderByDescending(c => ownership.IsOwned(c))
                .ThenBy(c => (int)c.Role)
                .ThenBy(c => c.DisplayName)
                .ToList();

        /// <summary>
        /// ownership が所持済みと判定するキャラクターの数を返す。
        /// </summary>
        public int CountOwned(ICharacterOwnership ownership) =>
            Characters.Count(c => c != null && ownership.IsOwned(c));
    }
}
