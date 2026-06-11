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
        private Renderer[] _renderers;
        private Collider _col;
        private Vector3 _spawnPos;

        private void Awake()
        {
            _health = GetComponent<HealthComponent>();
            _renderers = GetComponentsInChildren<Renderer>();
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
            SetVisible(false);
            if (_col != null) _col.enabled = false;
            StartCoroutine(RespawnRoutine());
        }

        private IEnumerator RespawnRoutine()
        {
            yield return new WaitForSeconds(8f);

            transform.position = _spawnPos;
            _health.Model.Revive();

            if (_col != null) _col.enabled = true;
            SetVisible(true);
        }

        private void SetVisible(bool visible)
        {
            foreach (var r in _renderers)
                r.enabled = visible;
        }
    }
}
