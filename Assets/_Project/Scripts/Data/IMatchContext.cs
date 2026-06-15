using Enigma.Character;

namespace Enigma.Data
{
    public enum MatchResult { None, Victory, Defeat }

    public interface IMatchContext
    {
        CharacterData PickedCharacter { get; set; }
        MatchResult Result { get; set; }
        float MatchDurationSeconds { get; set; }
        int Kills  { get; set; }
        int Deaths { get; set; }
    }
}
