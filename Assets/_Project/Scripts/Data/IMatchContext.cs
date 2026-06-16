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

        // 試合開始前に選択したレリックの ID 群（未選択は null/空）。
        System.Collections.Generic.IReadOnlyList<string> SelectedRelicIds { get; set; }
    }
}
