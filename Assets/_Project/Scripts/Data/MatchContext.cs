using Enigma.Character;

namespace Enigma.Data
{
    public sealed class MatchContext : IMatchContext
    {
        public CharacterData PickedCharacter { get; set; }
    }
}
