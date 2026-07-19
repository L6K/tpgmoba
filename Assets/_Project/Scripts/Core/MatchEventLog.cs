using System;
using System.Collections.Generic;

namespace Enigma.Core
{
    public enum MatchEventType { ChampionKill, ChampionDeath, MinionKill, TowerDestroyed, CoreCaptured, TitanDestroyed, MatchEnd }

    public readonly struct MatchEvent
    {
        public readonly float Time;
        public readonly MatchEventType Type;
        public readonly int Team;
        public readonly string ActorName;
        // 発生位置(倒された側の transform.position の X/Z)。バランス計測でマップ上の偏りを見るために使う。
        // 既存の発行箇所を壊さないよう既定値 0 とする(未設定=原点扱い)。
        public readonly float X;
        public readonly float Z;

        public MatchEvent(float time, MatchEventType type, int team, string actorName, float x = 0f, float z = 0f)
        {
            Time = time;
            Type = type;
            Team = team;
            ActorName = actorName;
            X = x;
            Z = z;
        }
    }

    public interface IMatchEventLog
    {
        IReadOnlyList<MatchEvent> Events { get; }
        event Action<MatchEvent> EventLogged;
        void Log(in MatchEvent e);
        void Clear();
    }

    public sealed class MatchEventLog : IMatchEventLog
    {
        private readonly List<MatchEvent> _events = new();

        public IReadOnlyList<MatchEvent> Events => _events;
        public event Action<MatchEvent> EventLogged;

        public void Log(in MatchEvent e)
        {
            _events.Add(e);
            EventLogged?.Invoke(e);
        }

        public void Clear()
        {
            _events.Clear();
        }
    }
}
