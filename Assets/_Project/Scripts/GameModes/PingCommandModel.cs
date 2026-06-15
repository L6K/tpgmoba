using System;
using System.Collections.Generic;

namespace Enigma.GameModes
{
    public enum PingType
    {
        Danger,
        OnMyWay,
        Attack
    }

    public readonly struct ActivePing
    {
        public readonly PingType Type;
        public readonly float X;
        public readonly float Z;
        public readonly float ExpiresAt;

        public ActivePing(PingType type, float x, float z, float expiresAt)
        {
            Type = type;
            X = x;
            Z = z;
            ExpiresAt = expiresAt;
        }
    }

    public sealed class PingCommandModel
    {
        private const float FullCircleDegrees = 360f;
        private const float OnMyWayBoundaryDegrees = 60f;
        private const float AttackBoundaryDegrees = 180f;
        private const float DangerBoundaryDegrees = 300f;

        private readonly float _minIntervalSeconds;
        private readonly float _displaySeconds;
        private readonly List<ActivePing> _activePings = new List<ActivePing>();
        private bool _hasIssued;
        private float _lastIssuedAt;

        public PingCommandModel(float minIntervalSeconds = 0.5f, float displaySeconds = 4f)
        {
            _minIntervalSeconds = Math.Max(0f, minIntervalSeconds);
            _displaySeconds = Math.Max(0f, displaySeconds);
        }

        public IReadOnlyList<ActivePing> ActivePings => _activePings;

        public bool TryIssue(PingType type, float x, float z, float now)
        {
            if (_hasIssued && now - _lastIssuedAt < _minIntervalSeconds)
                return false;

            _hasIssued = true;
            _lastIssuedAt = now;
            _activePings.Add(new ActivePing(type, x, z, now + _displaySeconds));
            return true;
        }

        public void Tick(float now)
        {
            for (int i = _activePings.Count - 1; i >= 0; i--)
            {
                if (_activePings[i].ExpiresAt <= now)
                    _activePings.RemoveAt(i);
            }
        }

        public void Clear()
        {
            _activePings.Clear();
        }

        public static PingType SelectByAngle(float angleDegrees)
        {
            float normalized = angleDegrees % FullCircleDegrees;
            if (normalized < 0f)
                normalized += FullCircleDegrees;

            if (normalized >= DangerBoundaryDegrees || normalized < OnMyWayBoundaryDegrees)
                return PingType.Danger;

            if (normalized < AttackBoundaryDegrees)
                return PingType.OnMyWay;

            return PingType.Attack;
        }
    }
}
