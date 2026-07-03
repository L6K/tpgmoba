using UnityEngine;
using Enigma.Combat;

namespace Enigma.Objectives
{
    // ゲートタワー1基に1個。タワー撃破でウィング壁(タワー両脇の壁片)を開放し、
    // 生存中は味方ユニットだけが壁とタワー本体のコライダーをすり抜けられるようにする
    // (敵は塞がれたまま=タワーを守るゲートとして機能する)。
    // ビルダー(BuildAetherRiftMap)が AddComponent 後に Configure で結線する Humble Object。
    public sealed class GateWall : MonoBehaviour
    {
        [SerializeField] private HealthComponent _towerHealth;
        [SerializeField] private Collider         _towerCollider;
        [SerializeField] private GameObject       _wingLeft;
        [SerializeField] private GameObject       _wingRight;
        [SerializeField] private Collider         _wingLeftCollider;
        [SerializeField] private Collider         _wingRightCollider;

        private TeamTag _teamTag;

        /// <summary>ウィング壁が開放済み(タワー撃破済み)か。M-V の視界遮蔽が参照する。</summary>
        public bool IsOpen { get; private set; }

        /// <summary>
        /// ビルダーから結線する。タワー本体コライダー・ウィング壁2枚(GameObject+trigger Collider)を渡す。
        /// </summary>
        public void Configure(HealthComponent towerHealth, Collider towerCollider,
            GameObject wingLeft, Collider wingLeftCollider,
            GameObject wingRight, Collider wingRightCollider)
        {
            _towerHealth       = towerHealth;
            _towerCollider     = towerCollider;
            _wingLeft           = wingLeft;
            _wingLeftCollider   = wingLeftCollider;
            _wingRight          = wingRight;
            _wingRightCollider  = wingRightCollider;
        }

        private void Awake()
        {
            _teamTag = GetComponent<TeamTag>();
        }

        private void OnEnable()
        {
            if (_towerHealth?.Model != null)
                _towerHealth.Model.Died += OnTowerDied;
        }

        private void OnDisable()
        {
            if (_towerHealth?.Model != null)
                _towerHealth.Model.Died -= OnTowerDied;
        }

        // タワー撃破でゲートを開放する。ウィング壁を隠し、タワー本体コライダーも無効化して
        // 通路を完全に開通させる(TowerAttack.OnDied は自身の Collider のみ無効化するため、
        // ここでも念のため明示的に無効化しておく)。
        private void OnTowerDied()
        {
            if (IsOpen) return;
            IsOpen = true;

            if (_wingLeft  != null) _wingLeft.SetActive(false);
            if (_wingRight != null) _wingRight.SetActive(false);
            if (_towerCollider != null) _towerCollider.enabled = false;
        }

        // ウィング壁の Trigger コライダーからの侵入通知。味方ユニットなら壁本体・タワー本体との
        // 衝突を無視して素通りさせる(敵はそのまま衝突する=ゲートとして機能)。
        public void NotifyWingTriggerEnter(Collider other, bool isLeftWing)
        {
            if (IsOpen || _teamTag == null) return;

            var otherTag = other.GetComponentInParent<TeamTag>();
            if (otherTag == null || otherTag.Team != _teamTag.Team) return; // 敵はそのまま衝突させる

            var wingCollider = isLeftWing ? _wingLeftCollider : _wingRightCollider;
            if (wingCollider != null)
                Physics.IgnoreCollision(wingCollider, other, true);
            if (_towerCollider != null)
                Physics.IgnoreCollision(_towerCollider, other, true);
        }
    }
}
