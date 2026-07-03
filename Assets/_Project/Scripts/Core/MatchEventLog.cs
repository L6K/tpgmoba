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

        public MatchEvent(float time, MatchEventType type, int team, string actorName)
        {
            Time = time;
            Type = type;
            Team = team;
            ActorName = actorName;
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
