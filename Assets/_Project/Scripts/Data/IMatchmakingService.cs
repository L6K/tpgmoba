namespace Enigma.Data
{
    public enum MatchmakingState { Idle, Searching, Found }

    public interface IMatchmakingService
    {
        MatchmakingState State        { get; }
        float            ElapsedSeconds { get; }

        void StartQueue();
        void Cancel();
        void Tick(float deltaSeconds);

        event System.Action MatchFound;
    }
}
