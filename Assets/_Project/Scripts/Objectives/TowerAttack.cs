using System.Collections.Generic;
using UnityEngine;
using Enigma.Character;
using Enigma.Combat;
using Enigma.Minion;

namespace Enigma.Objective
{
    // タワーが射程内の敵を自動攻撃する。
    // RequireComponent で HealthComponent を強制し、自身の死亡で停止する。
    [RequireComponent(typeof(HealthComponent))]
    public sealed class TowerAttack : MonoBehaviour
    {
        [SerializeField] private Projectile _projectilePrefab;
        [SerializeField] private Transform  _muzzle;

        private const float Range         = 14f;
        private const float AttackInterval = 1.2f;
        private const float Damage        = 20f;
        private const float ProjectileSpeed = 25f;
        private const float ScanInterval  = 0.4f;

        private AttackCooldown _attackCd;
        private float          _scanTimer;
        private HealthComponent _health;
        private TeamTag         _teamTag;
        private bool            _dead;

        private void Awake()
        {
            _health    = GetComponent<HealthComponent>();
            _teamTag   = GetComponent<TeamTag>();
            _attackCd  = new AttackCooldown(AttackInterval);

            // 撃破されたら倒れる演出と攻撃停止
            _health.Model.Died += OnDied;
        }

        private void OnDestroy()
        {
            // Died の購読解除（シーンアンロード時の二重発火防止）
            if (_health != null)
                _health.Model.Died -= OnDied;
        }

        private void Update()
        {
            if (_dead) return;

            _scanTimer += Time.deltaTime;
            if (_scanTimer < ScanInterval) return;
            _scanTimer = 0f;

            if (!_attackCd.IsReady(Time.time)) return;

            int idx = FindTarget();
            if (idx < 0) return;

            FireAt(_cachedPositions[idx]);
        }

        // OverlapSphere で候補を収集し MinionLogic.ChooseTarget で最近敵を決定する
        private readonly List<TargetCandidate> _candidates = new();
        private readonly List<Vector3>         _cachedPositions = new();

        private int FindTarget()
        {
            _candidates.Clear();
            _cachedPositions.Clear();

            var selfTeam = _teamTag != null ? _teamTag.Team : TeamId.Neutral;
            var hits     = Physics.OverlapSphere(transform.position, Range);

            foreach (var col in hits)
            {
                if (col.isTrigger) continue;
                var hc = col.GetComponent<HealthComponent>();
                var tt = col.GetComponent<TeamTag>();
                if (hc == null || tt == null) continue;
                if (hc.Model.IsDead) continue;

                _candidates.Add(new TargetCandidate(col.transform.position, tt.Team));
                _cachedPositions.Add(col.transform.position);
            }

            return MinionLogic.ChooseTarget(transform.position, selfTeam, _candidates, Range);
        }

        private void FireAt(Vector3 targetPos)
        {
            if (_projectilePrefab == null || _muzzle == null) return;
            if (!_attackCd.TryConsume(Time.time)) return;

            // 銃口が高所(クリスタル)にあるため、対象の胴体高さを狙う3D方向で撃つ
            var dir = (targetPos + Vector3.up * 0.9f - _muzzle.position);
            if (dir.sqrMagnitude < 0.001f) return;
            dir.Normalize();

            float lifetime = ProjectileSpeed > 0f ? Range / ProjectileSpeed : 2f;
            var proj = Instantiate(_projectilePrefab, _muzzle.position, Quaternion.identity);
            proj.Init(dir, ProjectileSpeed, Damage, gameObject, lifetime);
        }

        private void OnDied()
        {
            _dead = true;

            // 倒壊演出は DeathPresenter(Sink) が担うため、ここでは傾けない（二重演出回避）。

            // コライダーを無効化して通行可能にする
            foreach (var col in GetComponents<Collider>())
                col.enabled = false;
        }
    }
}
