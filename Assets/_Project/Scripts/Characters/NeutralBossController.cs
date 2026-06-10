using System.Collections;
using UnityEngine;
using Enigma.Combat;
using Enigma.Ability;

namespace Enigma.Objective
{
    [RequireComponent(typeof(HealthComponent))]
    public sealed class NeutralBossController : MonoBehaviour
    {
        [SerializeField] private TelegraphCircle _telegraphPrefab;

        private const float DetectRadius    = 20f;
        private const float CastInterval    = 6f;
        private const float TelegraphRadius = 5f;
        private const float TelegraphDelay  = 1.5f;
        private const float TelegraphDamage = 50f;

        private HealthComponent _health;
        private Collider        _col;
        private float           _nextCastTime;

        private void Awake()
        {
            _health = GetComponent<HealthComponent>();
            _col    = GetComponent<Collider>();
        }

        private void Start()
        {
            _health.Model.Died += OnDied;
            _nextCastTime = Time.time + CastInterval;
        }

        private void OnDestroy()
        {
            if (_health?.Model != null)
                _health.Model.Died -= OnDied;
        }

        private void Update()
        {
            if (_health.Model.IsDead) return;
            if (Time.time < _nextCastTime) return;

            _nextCastTime = Time.time + CastInterval;

            // 索敵半径内のプレイヤーを探してターゲット
            var playerObj = FindPlayerInRange();
            if (playerObj == null) return;

            if (_telegraphPrefab == null) return;

            var t = Instantiate(_telegraphPrefab, playerObj.transform.position, Quaternion.identity);
            t.Init(TelegraphRadius, TelegraphDelay, TelegraphDamage, gameObject);
        }

        private GameObject FindPlayerInRange()
        {
            var players = GameObject.FindGameObjectsWithTag("Player");
            foreach (var p in players)
            {
                if (Vector3.Distance(transform.position, p.transform.position) <= DetectRadius)
                    return p;
            }
            return null;
        }

        private void OnDied()
        {
            // 倒れる演出
            transform.rotation = Quaternion.Euler(0f, 0f, 90f);
            if (_col != null) _col.enabled = false;
        }
    }
}
