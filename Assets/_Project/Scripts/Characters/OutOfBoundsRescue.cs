using UnityEngine;

namespace Enigma.Character
{
    /// <summary>
    /// 場外に出たプレイヤーを定期的に検知し、最寄りのレーン地点へ瞬間移動させる Humble Object。
    /// 判定ロジックは OutOfBoundsLogic（純粋関数）へ委譲し、本体は時間管理とワープのみ担う。
    /// </summary>
    public sealed class OutOfBoundsRescue : MonoBehaviour
    {
        [SerializeField] private float _checkInterval = 2f;
        [SerializeField] private float _rescueY       = 1.3f;

        private CharacterController _cc;
        private float _timer;

        private void Awake()
        {
            _cc = GetComponent<CharacterController>();
        }

        private void Update()
        {
            _timer += Time.deltaTime;
            if (_timer < _checkInterval) return;
            _timer = 0f;

            var pos = transform.position;
            if (!OutOfBoundsLogic.IsOutOfBounds(pos.x, pos.z)) return;

            var (rx, rz) = OutOfBoundsLogic.NearestLanePoint(pos.x, pos.z);

            // CharacterController 有効時はコライダー解決がワープを阻害するため一時無効化
            bool wasEnabled = _cc != null && _cc.enabled;
            if (wasEnabled) _cc.enabled = false;

            transform.position = new Vector3(rx, _rescueY, rz);

            if (wasEnabled) _cc.enabled = true;
        }
    }
}
