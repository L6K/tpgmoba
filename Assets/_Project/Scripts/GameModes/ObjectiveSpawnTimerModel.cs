using System;

namespace Enigma.GameModes
{
    public enum ObjectiveState
    {
        Dormant,
        Warning,
        Active
    }

    public sealed class ObjectiveSpawnTimerModel
    {
        private readonly float _firstSpawnDelay;
        private readonly float _respawnInterval;
        private readonly float _warningLeadSeconds;
        private float _nextSpawnAt;

        public ObjectiveSpawnTimerModel(float firstSpawnDelay, float respawnInterval, float warningLeadSeconds)
        {
            _firstSpawnDelay = Math.Max(0f, firstSpawnDelay);
            _respawnInterval = Math.Max(0f, respawnInterval);
            _warningLeadSeconds = Math.Max(0f, warningLeadSeconds);
            _nextSpawnAt = _firstSpawnDelay;
        }

        public ObjectiveState GetState(float now)
        {
            if (now >= _nextSpawnAt)
                return ObjectiveState.Active;

            float secondsUntilSpawn = _nextSpawnAt - now;
            if (secondsUntilSpawn <= _warningLeadSeconds)
                return ObjectiveState.Warning;

            return ObjectiveState.Dormant;
        }

        public bool IsActive(float now)
        {
            return GetState(now) == ObjectiveState.Active;
        }

        public bool IsWarning(float now)
        {
            return GetState(now) == ObjectiveState.Warning;
        }

        public float SecondsUntilSpawn(float now)
        {
            if (IsActive(now))
                return 0f;

            return Math.Max(0f, _nextSpawnAt - now);
        }

        public void NotifyKilled(float now)
        {
            if (!IsActive(now))
                return;

            _nextSpawnAt = now + _respawnInterval;
        }

        public void Reset()
        {
            _nextSpawnAt = _firstSpawnDelay;
        }
    }
}
