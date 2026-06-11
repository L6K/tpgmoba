using Enigma.Character;

namespace Enigma.Data
{
    public sealed class MatchContext : IMatchContext
    {
        public CharacterData PickedCharacter { get; set; }
        public MatchResult Result { get; set; }
        public float MatchDurationSeconds { get; set; }
    }
}
