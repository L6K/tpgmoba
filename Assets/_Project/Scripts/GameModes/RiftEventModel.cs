namespace Enigma.GameModes
{
    public enum RiftState
    {
        Dormant,
        Warning,
        Open,
        Captured,
        Cooldown
    }

    public enum RiftEffect
    {
        None,
        Shortcut,
        TeamVision,
        TeamHaste
    }

    public readonly struct RiftStatus
    {
        public readonly RiftState State;
        public readonly float SecondsToNextChange;
        public readonly int CapturingTeam;
        public readonly float CaptureProgress01;
        public readonly RiftEffect ActiveEffect;
        public readonly int OwnerTeam;

        public RiftStatus(
            RiftState state,
            float secondsToNextChange,
            int capturingTeam,
            float captureProgress01,
            RiftEffect activeEffect,
            int ownerTeam)
        {
            State = state;
            SecondsToNextChange = secondsToNextChange;
            CapturingTeam = capturingTeam;
            CaptureProgress01 = captureProgress01;
            ActiveEffect = activeEffect;
            OwnerTeam = ownerTeam;
        }
    }

    public sealed class RiftEventModel
    {
        private readonly float _firstOpenAt;
        private readonly float _warningLead;
        private readonly float _openWindow;
        private readonly float _captureSeconds;
        private readonly float _effectDuration;
        private readonly float _cooldown;

        private RiftState _state;
        private float _openTime;
        private float _phaseStartedAt;
        private float _captureProgress01;
        private int _capturingTeam = -1;
        private int _ownerTeam = -1;
        private RiftEffect _activeEffect = RiftEffect.None;

        public RiftEventModel(
            float firstOpenAt = 120f,
            float warningLead = 10f,
            float openWindow = 30f,
            float captureSeconds = 6f,
            float effectDuration = 45f,
            float cooldown = 90f)
        {
            _firstOpenAt = firstOpenAt <= 0f ? 120f : firstOpenAt;
            _warningLead = warningLead <= 0f ? 10f : warningLead;
            if (_warningLead > _firstOpenAt)
                _warningLead = _firstOpenAt;

            _openWindow = openWindow <= 0f ? 30f : openWindow;
            _captureSeconds = captureSeconds <= 0f ? 6f : captureSeconds;
            _effectDuration = effectDuration <= 0f ? 45f : effectDuration;
            _cooldown = cooldown <= 0f ? 90f : cooldown;
            Reset();
        }

        public int OpenCount { get; private set; }

        public RiftStatus Tick(float now, float dt, int presentTeam)
        {
            AdvanceTimedTransitions(now);

            if (_state == RiftState.Open)
            {
                ApplyCaptureProgress(dt, presentTeam);
                if (_captureProgress01 >= 1f)
                    EnterCaptured(now);
                else if (now - _phaseStartedAt >= _openWindow)
                    EnterCooldown(now);
            }
            else
            {
                AdvanceTimedTransitions(now);
            }

            return BuildStatus(now);
        }

        public void Reset()
        {
            _state = RiftState.Dormant;
            _openTime = _firstOpenAt;
            _phaseStartedAt = 0f;
            _captureProgress01 = 0f;
            _capturingTeam = -1;
            _ownerTeam = -1;
            _activeEffect = RiftEffect.None;
            OpenCount = 0;
        }

        private void AdvanceTimedTransitions(float now)
        {
            bool advanced;
            int guard = 0;
            do
            {
                advanced = false;
                guard++;

                if (_state == RiftState.Dormant && now >= _openTime - _warningLead)
                {
                    EnterWarning(now);
                    advanced = true;
                }

                if (_state == RiftState.Warning && now >= _openTime)
                {
                    EnterOpen(now);
                    advanced = true;
                }

                if (_state == RiftState.Captured && now - _phaseStartedAt >= _effectDuration)
                {
                    EnterCooldown(now);
                    advanced = true;
                }

                if (_state == RiftState.Cooldown && now - _phaseStartedAt >= _cooldown)
                {
                    EnterDormant(now);
                    advanced = true;
                }
            }
            while (advanced && guard < 8);
        }

        private void ApplyCaptureProgress(float dt, int presentTeam)
        {
            if (presentTeam != 0 && presentTeam != 1)
            {
                _capturingTeam = -1;
                return;
            }

            if (_capturingTeam != presentTeam)
            {
                _capturingTeam = presentTeam;
                _captureProgress01 = 0f;
            }

            if (dt > 0f)
                _captureProgress01 = Clamp01(_captureProgress01 + dt / _captureSeconds);
        }

        private void EnterWarning(float now)
        {
            _state = RiftState.Warning;
            _phaseStartedAt = now;
        }

        private void EnterOpen(float now)
        {
            _state = RiftState.Open;
            _phaseStartedAt = now;
            _captureProgress01 = 0f;
            _capturingTeam = -1;
            _ownerTeam = -1;
            _activeEffect = RiftEffect.None;
            OpenCount++;
        }

        private void EnterCaptured(float now)
        {
            _state = RiftState.Captured;
            _phaseStartedAt = now;
            _ownerTeam = _capturingTeam;
            _activeEffect = EffectForOpenCount(OpenCount);
            _capturingTeam = -1;
            _captureProgress01 = 0f;
        }

        private void EnterCooldown(float now)
        {
            _state = RiftState.Cooldown;
            _phaseStartedAt = now;
            _captureProgress01 = 0f;
            _capturingTeam = -1;
            _ownerTeam = -1;
            _activeEffect = RiftEffect.None;
        }

        private void EnterDormant(float now)
        {
            _state = RiftState.Dormant;
            _phaseStartedAt = now;
            _openTime = now + _firstOpenAt;
        }

        private RiftStatus BuildStatus(float now)
        {
            switch (_state)
            {
                case RiftState.Dormant:
                    return new RiftStatus(_state, ClampNonNegative(_openTime - now), -1, 0f, RiftEffect.None, -1);
                case RiftState.Warning:
                    return new RiftStatus(_state, ClampNonNegative(_openTime - now), -1, 0f, RiftEffect.None, -1);
                case RiftState.Open:
                    return new RiftStatus(_state, ClampNonNegative(_openWindow - (now - _phaseStartedAt)), _capturingTeam, _captureProgress01, RiftEffect.None, -1);
                case RiftState.Captured:
                    return new RiftStatus(_state, ClampNonNegative(_effectDuration - (now - _phaseStartedAt)), -1, 0f, _activeEffect, _ownerTeam);
                case RiftState.Cooldown:
                    return new RiftStatus(_state, ClampNonNegative(_cooldown - (now - _phaseStartedAt)), -1, 0f, RiftEffect.None, -1);
                default:
                    return default;
            }
        }

        private static RiftEffect EffectForOpenCount(int openCount)
        {
            int index = (openCount - 1) % 3;
            if (index == 0) return RiftEffect.Shortcut;
            if (index == 1) return RiftEffect.TeamVision;
            return RiftEffect.TeamHaste;
        }

        private static float ClampNonNegative(float value)
        {
            return value < 0f ? 0f : value;
        }

        private static float Clamp01(float value)
        {
            if (value < 0f) return 0f;
            if (value > 1f) return 1f;
            return value;
        }
    }
}
