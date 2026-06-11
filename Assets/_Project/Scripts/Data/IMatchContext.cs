using Enigma.Character;

namespace Enigma.Data
{
    public interface IMatchContext
    {
        CharacterData PickedCharacter { get; set; }
    }
}
