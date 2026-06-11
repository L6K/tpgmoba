using System;

namespace Enigma.Abilities
{
    public sealed class SkillProgression
    {
        private readonly int[] _ranks = new int[3];
        private int _unspentPoints;

        public event Action Changed;

        public int UnspentPoints => _unspentPoints;

        public SkillProgression()
        {
            // LoL rule: champion starts with 1 point at level 1
            _unspentPoints = 1;
        }

        public int GetRank(int slot)
        {
            ValidateSlot(slot);
            return _ranks[slot];
        }

        public bool CanLevelUp(int slot, int championLevel)
        {
            ValidateSlot(slot);

            if (_unspentPoints <= 0)
                return false;

            int currentRank = _ranks[slot];

            if (slot == 2)
            {
                // R (ultimate): max rank 3, gated at Lv 6/8/10
                if (currentRank >= 3)
                    return false;
                int requiredLevel = 4 + (currentRank + 1) * 2; // rank1→6, rank2→8, rank3→10
                return championLevel >= requiredLevel;
            }
            else
            {
                // Q/E: max rank 5; rank n requires championLevel >= 2n-1
                if (currentRank >= 5)
                    return false;
                int nextRank = currentRank + 1;
                // rank1 requires Lv1 (2*1-1=1), rank2→Lv3, rank3→Lv5, rank4→Lv7, rank5→Lv9
                int requiredLevel = 2 * nextRank - 1;
                return championLevel >= requiredLevel;
            }
        }

        public bool TryLevelUp(int slot, int championLevel)
        {
            ValidateSlot(slot);

            if (!CanLevelUp(slot, championLevel))
                return false;

            _ranks[slot]++;
            _unspentPoints--;
            Changed?.Invoke();
            return true;
        }

        public void OnChampionLevelUp()
        {
            _unspentPoints++;
            Changed?.Invoke();
        }

        public static float DamageMultiplier(int rank)
        {
            if (rank <= 0)
                return 0f;
            return 1f + 0.25f * (rank - 1);
        }

        private static void ValidateSlot(int slot)
        {
            if (slot < 0 || slot > 2)
                throw new ArgumentOutOfRangeException(nameof(slot), slot, "Slot must be 0 (Q), 1 (E), or 2 (R).");
        }
    }
}
