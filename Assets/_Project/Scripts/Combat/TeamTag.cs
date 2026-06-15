using UnityEngine;

namespace Enigma.Combat
{
    public enum TeamId { Blue, Red, Neutral }

    public sealed class TeamTag : MonoBehaviour
    {
        [SerializeField] private TeamId _team = TeamId.Neutral;

        public TeamId Team => _team;

        // スポーン時にスポーナーが呼び出す
        public void SetTeam(TeamId team) => _team = team;
    }
}
