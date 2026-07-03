using UnityEngine;

namespace Enigma.Objectives
{
    // ウィング壁の Trigger Collider に付ける薄いブリッジ。OnTriggerEnter を
    // 親の GateWall へ中継するだけ(ロジックは GateWall.NotifyWingTriggerEnter に集約)。
    public sealed class GateWingTrigger : MonoBehaviour
    {
        [SerializeField] private GateWall _gateWall;
        [SerializeField] private bool     _isLeftWing;

        public void Configure(GateWall gateWall, bool isLeftWing)
        {
            _gateWall   = gateWall;
            _isLeftWing = isLeftWing;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_gateWall != null)
                _gateWall.NotifyWingTriggerEnter(other, _isLeftWing);
        }
    }
}
