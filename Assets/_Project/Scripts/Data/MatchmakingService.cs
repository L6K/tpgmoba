namespace Enigma.Data
{
    public sealed class MatchmakingService : IMatchmakingService
    {
        private readonly IRandomSource _random;

        private float _targetSeconds;
        private bool  _eventFired;

        public MatchmakingState State         { get; private set; } = MatchmakingState.Idle;
        public float            ElapsedSeconds { get; private set; }

        public event System.Action MatchFound;

        public MatchmakingService(IRandomSource random)
        {
            _random = random;
        }

        public void StartQueue()
        {
            ElapsedSeconds = 0f;
            _eventFired    = false;
            // 成立目標 = 2 + [0,4) → 2〜5 秒
            _targetSeconds = 2f + _random.Next(4);
            State          = MatchmakingState.Searching;
        }

        public void Cancel()
        {
            State          = MatchmakingState.Idle;
            ElapsedSeconds = 0f;
            _eventFired    = false;
        }

        public void Tick(float deltaSeconds)
        {
            if (State != MatchmakingState.Searching) return;

            ElapsedSeconds += deltaSeconds;

            if (ElapsedSeconds >= _targetSeconds && !_eventFired)
            {
                _eventFired = true;
                State       = MatchmakingState.Found;
                MatchFound?.Invoke();
            }
        }
    }
}
