using System.Collections;
using UnityEngine;
using Enigma.Combat;

namespace Enigma.Character
{
    [RequireComponent(typeof(HealthComponent))]
    public sealed class TargetDummy : MonoBehaviour
    {
        [SerializeField] private Transform _barFill;

        private HealthComponent _health;
        private Collider _col;
        private bool _isDead;

        private void Awake()
        {
            _health = GetComponent<HealthComponent>();
            _col = GetComponent<Collider>();
        }

        private void Start()
        {
            _health.Model.Changed += OnChanged;
            _health.Model.Died += OnDied;

            // 満タン表示にリセット
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
            var scale = _barFill.localScale;
            scale.x = current / max;
            _barFill.localScale = scale;
        }

        private void OnDied()
        {
            _isDead = true;
            // 90度倒れる
            transform.rotation = Quaternion.Euler(0f, 0f, 90f);
            if (_col != null) _col.enabled = false;
            StartCoroutine(ReviveRoutine());
        }

        private IEnumerator ReviveRoutine()
        {
            yield return new WaitForSeconds(3f);
            transform.rotation = Quaternion.identity;
            if (_col != null) _col.enabled = true;
            _health.Model.Revive();
            _isDead = false;
        }
    }
}
