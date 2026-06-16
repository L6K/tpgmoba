using System.Collections.Generic;

namespace Enigma.Combat
{
    public enum MultiKill { None, Double, Triple, Quadra, Penta }

    public enum Streak { None, Spree, Rampage, Unstoppable, Dominating, Godlike }

    public readonly struct KillResult
    {
        public readonly MultiKill MultiKill;
        public readonly int       MultiKillCount;
        public readonly Streak    Streak;
        public readonly int       StreakCount;
        public readonly bool      IsShutdown;
        public readonly Streak    VictimStreakEnded;

        public KillResult(MultiKill multiKill, int multiKillCount, Streak streak, int streakCount,
                          bool isShutdown, Streak victimStreakEnded)
        {
            MultiKill = multiKill;
            MultiKillCount = multiKillCount;
            Streak = streak;
            StreakCount = streakCount;
            IsShutdown = isShutdown;
            VictimStreakEnded = victimStreakEnded;
        }
    }

    /// <summary>
    /// マルチキル(短時間連続)・キルストリーク(死なずに連続)・シャットダウン判定の純ロジック。
    /// アナウンス/音/ゴールド付与は呼び出し側(Unity)が KillResult を見て行う。
    /// </summary>
    public sealed class MultiKillStreakModel
    {
        private sealed class State
        {
            public float LastKillTime;
            public int   MultiKillCount;
            public int   StreakCount;
        }

        private readonly float _window;
        private readonly Dictionary<string, State> _states = new Dictionary<string, State>();

        public MultiKillStreakModel(float multiKillWindowSeconds = 10f)
        {
            _window = multiKillWindowSeconds <= 0f ? 10f : multiKillWindowSeconds;
        }

        public KillResult RegisterKill(string killerId, string victimId, float now)
        {
            string killer = Norm(killerId);
            string victim = Norm(victimId);
            if (killer == victim) return default;

            var ks = Get(killer);
            // マルチキル: 直前キルから window 以内なら継続、超過/初回は 1。
            if (ks.MultiKillCount > 0 && now - ks.LastKillTime <= _window) ks.MultiKillCount++;
            else ks.MultiKillCount = 1;
            ks.LastKillTime = now;

            // ストリーク: 死ぬまで累積。
            ks.StreakCount++;

            // シャットダウン: 被害者が Spree 以上のストリーク中だったか。
            var vs = Get(victim);
            Streak victimTier = StreakTier(vs.StreakCount);
            bool shutdown = victimTier >= Streak.Spree;
            // 被害者は死亡 → ストリークもマルチキル窓もリセット。
            vs.StreakCount = 0;
            vs.MultiKillCount = 0;

            return new KillResult(
                MultiKillTier(ks.MultiKillCount), ks.MultiKillCount,
                StreakTier(ks.StreakCount), ks.StreakCount,
                shutdown, shutdown ? victimTier : Streak.None);
        }

        /// <summary>被killでない死(環境/オーバータイム減衰等)。マルチキル窓とストリークを 0 に。</summary>
        public void RegisterDeath(string playerId, float now)
        {
            var s = Get(Norm(playerId));
            s.StreakCount = 0;
            s.MultiKillCount = 0;
        }

        public int StreakCountOf(string playerId)
        {
            return _states.TryGetValue(Norm(playerId), out var s) ? s.StreakCount : 0;
        }

        public void Clear() => _states.Clear();

        private State Get(string id)
        {
            if (!_states.TryGetValue(id, out var s)) { s = new State(); _states[id] = s; }
            return s;
        }

        private static string Norm(string id) => string.IsNullOrEmpty(id) ? "Unknown" : id;

        private static MultiKill MultiKillTier(int count)
        {
            switch (count)
            {
                case 1:  return MultiKill.None;
                case 2:  return MultiKill.Double;
                case 3:  return MultiKill.Triple;
                case 4:  return MultiKill.Quadra;
                default: return count >= 5 ? MultiKill.Penta : MultiKill.None;
            }
        }

        private static Streak StreakTier(int count)
        {
            if (count >= 11) return Streak.Godlike;
            if (count >= 9)  return Streak.Dominating;
            if (count >= 7)  return Streak.Unstoppable;
            if (count >= 5)  return Streak.Rampage;
            if (count >= 3)  return Streak.Spree;
            return Streak.None;
        }
    }
}
