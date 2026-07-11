using System.Collections;
using UnityEngine;
using Enigma.Combat;
using Enigma.GameModes;

namespace Enigma.Character
{
    [RequireComponent(typeof(HealthComponent))]
    public sealed class PlayerRespawn : MonoBehaviour
    {
        [SerializeField] private Transform _spawnPoint;

        private HealthComponent   _health;
        private CharacterController _cc;

        private void Awake()
        {
            _health = GetComponent<HealthComponent>();
            _cc     = GetComponent<CharacterController>();
        }

        private void Start()
        {
            _health.Model.Died += OnDied;
        }

        private void OnDestroy()
        {
            if (_health?.Model != null)
                _health.Model.Died -= OnDied;
        }

        private void OnDied()
        {
            StartCoroutine(RespawnRoutine());
        }

        private IEnumerator RespawnRoutine()
        {
            // 膠着防止: 試合経過に応じてデスタイマーを伸ばす（固定5秒から逓増へ）。
            yield return new WaitForSeconds(RespawnTimerLogic.Delay(Time.timeSinceLevelLoad));

            // CharacterController を無効化してからワープ（有効時はコライダー解決が阻害するため）
            if (_cc != null) _cc.enabled = false;

            if (_spawnPoint != null)
                transform.position = _spawnPoint.position;

            if (_cc != null) _cc.enabled = true;

            _health.Model.Revive();
        }
    }
}
