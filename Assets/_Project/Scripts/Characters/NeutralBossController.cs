using System.Collections;
using UnityEngine;
using Enigma.Combat;

namespace Enigma.Objective
{
    [RequireComponent(typeof(HealthComponent))]
    public sealed class NeutralBossController : MonoBehaviour
    {
        [SerializeField] private TelegraphCircle  _telegraphPrefab;
        [SerializeField] private TelegraphSector  _sectorPrefab;
        [SerializeField] private StackMarker      _stackMarkerPrefab;

        private const float DetectRadius = 22f;

        // 通常時の詠唱間隔
        private const float CastIntervalNormal = 8f;
        // HP 40% 以下での詠唱間隔（P3 布石）
        private const float CastIntervalEnraged = 6f;

        // ChasingCircles パラメータ
        private const float ChaseRadius  = 4f;
        private const float ChaseDelay   = 1.2f;
        private const float ChaseDamage  = 30f;
        private const float ChaseInterval = 0.9f;
        private const int   ChaseCount   = 3;

        // SectorCleave パラメータ
        private const float SectorAngle  = 90f;
        private const float SectorRadius = 16f;
        private const float SectorDelay  = 1.5f;
        private const float SectorDamage = 45f;

        // StackMarker パラメータ
        private const float StackDelay       = 2.5f;
        private const float StackTotalDamage = 120f;
        private const float StackRadius      = 4f;

        private HealthComponent    _health;
        private Collider           _col;
        private float              _nextCastTime;
        private BossGimmickRotation _rotation;
        private bool               _castRunning;

        private void Awake()
        {
            _health   = GetComponent<HealthComponent>();
            _col      = GetComponent<Collider>();
            _rotation = new BossGimmickRotation();
        }

        private void Start()
        {
            _health.Model.Died += OnDied;
            _nextCastTime = Time.time + CastIntervalNormal;
        }

        private void OnDestroy()
        {
            if (_health?.Model != null)
                _health.Model.Died -= OnDied;
        }

        private void Update()
        {
            if (_health.Model.IsDead) return;
            if (_castRunning) return;
            if (Time.time < _nextCastTime) return;

            var playerObj = FindPlayerInRange();
            if (playerObj == null) return;

            var gimmick = _rotation.Next();
            StartCoroutine(CastGimmick(gimmick, playerObj));
        }

        private IEnumerator CastGimmick(BossGimmick gimmick, GameObject player)
        {
            _castRunning = true;

            switch (gimmick)
            {
                case BossGimmick.ChasingCircles:
                    yield return CastChasingCircles(player);
                    break;
                case BossGimmick.SectorCleave:
                    CastSectorCleave(player);
                    break;
                case BossGimmick.StackMarker:
                    CastStackMarker(player);
                    break;
            }

            // HP 40% 以下でエンレイジ間隔に短縮
            float interval = (_health.Model.CurrentHp / _health.Model.MaxHp <= 0.4f)
                ? CastIntervalEnraged
                : CastIntervalNormal;

            _nextCastTime = Time.time + interval;
            _castRunning  = false;
        }

        private IEnumerator CastChasingCircles(GameObject player)
        {
            if (_telegraphPrefab == null) yield break;

            for (int i = 0; i < ChaseCount; i++)
            {
                // 発動時点のプレイヤー位置へ予兆円を置く（追従ではなく現在地狙い）
                var pos = player != null ? player.transform.position : transform.position;
                var t   = Instantiate(_telegraphPrefab, pos, Quaternion.identity);
                t.Init(ChaseRadius, ChaseDelay, ChaseDamage, gameObject);
                yield return new WaitForSeconds(ChaseInterval);
            }
        }

        private void CastSectorCleave(GameObject player)
        {
            if (_sectorPrefab == null) return;

            var dir = (player.transform.position - transform.position).normalized;
            dir.y = 0f;
            if (dir == Vector3.zero) dir = transform.forward;

            var s = Instantiate(_sectorPrefab, transform.position, Quaternion.identity);
            s.Init(transform.position, dir, SectorAngle, SectorRadius, SectorDelay, SectorDamage, gameObject);
        }

        private void CastStackMarker(GameObject player)
        {
            if (_stackMarkerPrefab == null) return;

            var m = Instantiate(_stackMarkerPrefab, player.transform.position, Quaternion.identity);
            m.Init(player.transform, StackDelay, StackTotalDamage, StackRadius, gameObject);
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
