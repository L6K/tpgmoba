using System.Collections.Generic;
using Enigma.Data;

namespace Enigma.UI
{
    /// <summary>
    /// キャラピック画面のピック/オートロックロジック。
    /// MonoBehaviour に依存しない plain C# クラスとして切り出し、EditMode テスト対象にする。
    /// </summary>
    public static class CharSelectLogic
    {
        /// <summary>
        /// AI が選ぶべきキャラクターのインデックスを返す。
        /// owned かつ taken でないインデックスの中から random.Next で1つ選ぶ。
        /// 候補なしは -1。
        /// </summary>
        public static int ChooseAiPick(
            IReadOnlyList<bool> taken,
            IReadOnlyList<bool> owned,
            IRandomSource random)
        {
            // 候補リストを先に集めることで、random.Next(候補数) の結果を
            // インデックス変換なしに使える
            var candidates = new List<int>();
            int count = owned.Count < taken.Count ? owned.Count : taken.Count;
            for (int i = 0; i < count; i++)
            {
                if (owned[i] && !taken[i])
                    candidates.Add(i);
            }
            if (candidates.Count == 0) return -1;
            return candidates[random.Next(candidates.Count)];
        }

        /// <summary>
        /// タイマー満了時の自動ロック対象インデックスを返す。
        /// currentSelection が有効（0以上かつ owned）ならそれをそのまま採用し、
        /// 無効なら最初の owned を返す。所持キャラが1つもなければ -1。
        /// </summary>
        public static int ResolveAutoLock(int currentSelection, IReadOnlyList<bool> owned)
        {
            // 現在の選択が有効であれば優先する
            if (currentSelection >= 0 && currentSelection < owned.Count && owned[currentSelection])
                return currentSelection;

            // フォールバック: 最初の所持キャラを選ぶ
            for (int i = 0; i < owned.Count; i++)
            {
                if (owned[i]) return i;
            }
            return -1;
        }
    }
}
