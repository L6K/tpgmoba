using System.Collections;
using UnityEngine;
using Enigma.Combat;

namespace Enigma.Character
{
    [RequireComponent(typeof(HealthComponent))]
    public sealed class DummyChampion : MonoBehaviour
    {
        [SerializeField] private Transform _barFill;

        private HealthComponent _health;
        private Collider _col;
        private Vector3 _spawnPos;

        private void Awake()
        {
            _health = GetComponent<HealthComponent>();
            _col = GetComponent<Collider>();
            _spawnPos = transform.position;
        }

        private void Start()
        {
            _health.Model.Changed += OnChanged;
            _health.Model.Died += OnDied;

            // 初期表示を満タンに合わせる
            if (_barFill != null)
            {
                var s = _barFill.localScale;
                s.x = 1f;
                _barFill.localScale = s;
            }
        }

        private void OnDestroy()
        {
            if (_health?.Model == null) return;
            _health.Model.Changed -= OnChanged;
            _health.Model.Died -= OnDied;
        }

        private void OnChanged(float current, float max)
        {
            if (_barFill == null || max <= 0f) return;
            var s = _barFill.localScale;
            s.x = current / max;
            _barFill.localScale = s;
        }

        private void OnDied()
        {
            // 見た目（フェード/転倒）は DeathPresenter に委譲。ここでは当たり判定だけ止める
            if (_col != null) _col.enabled = false;
            StartCoroutine(RespawnRoutine());
        }

        private IEnumerator RespawnRoutine()
        {
            yield return new WaitForSeconds(8f);

            transform.position = _spawnPos;
            // Revive 経由で HealthModel.Revived が発火し DeathPresenter が見た目を復元する
            _health.Model.Revive();

            if (_col != null) _col.enabled = true;
        }
    }
}
