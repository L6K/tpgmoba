using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Enigma.Ability;
using Enigma.Audio;
using Enigma.Character;
using Enigma.Combat;
using Enigma.Core;
using Enigma.GameModes;
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
        [SerializeField] private Transform  _crystal; // チャージ予兆を出す頂部クリスタル（ビルダーで結線）

        // 隣接タワー間隔(ジャングル口の対=20°間隔・弦長≈15.6m)より攻囲位置(タワー際2〜3m)が
        // 圏外になるよう 11 に抑える。14 だと外側タワーを殴るミニオンが奥のタワーに撃たれる。
        private const float Range         = 11f;
        private const float AttackInterval = 1.2f;
        private const float Damage        = 20f;
        private const float ProjectileSpeed = 25f;
        private const float ScanInterval  = 0.4f;
        private const float ChargeLead    = 0.45f; // 発射の何秒前からチャージ予兆を出すか

        private AttackCooldown _attackCd;
        private float          _scanTimer;
        private HealthComponent _health;
        private TeamTag         _teamTag;
        private bool            _dead;
        private bool            _charging;       // チャージ中は新規発射を受け付けない
        private Vector3         _crystalBaseScale = Vector3.one;

        private void Awake()
        {
            _health    = GetComponent<HealthComponent>();
            _teamTag   = GetComponent<TeamTag>();
            _attackCd  = new AttackCooldown(AttackInterval);

            // クリスタル未結線なら名前で探索フォールバック（"TowerCrystalLarge" メッシュ or "Crystal" GO）
            if (_crystal == null)
                _crystal = FindCrystal();
            if (_crystal != null)
                _crystalBaseScale = _crystal.localScale;

            // 撃破されたら倒れる演出と攻撃停止
            _health.Model.Died += OnDied;
        }

        // 子階層から発光クリスタルを名前ベースで探す。ビルダー結線が無い場合のフォールバック。
        private Transform FindCrystal()
        {
            foreach (var t in GetComponentsInChildren<Transform>(includeInactive: true))
            {
                if (t == transform) continue;
                if (t.name.Contains("Crystal")) return t;
            }
            return null;
        }

        // チーム色（青/赤）をチャージ発光色に使う
        private Color ChargeColor()
        {
            var team = _teamTag != null ? _teamTag.Team : TeamId.Neutral;
            return team == TeamId.Red
                ? new Color(1f, 0.4f, 0.35f, 1f)
                : new Color(0.35f, 0.65f, 1f, 1f);
        }

        private void OnDestroy()
        {
            // Died の購読解除（シーンアンロード時の二重発火防止）
            if (_health != null)
                _health.Model.Died -= OnDied;
        }

        private void Update()
        {
            // プレイ開始直後の約1秒、初期化前 Update が走る環境要因の NRE バーストが
            // 観測されたため防御(根本原因は初期化順の調査タスクへ切り出し済み)
            if (_attackCd == null) return;
            if (_dead || _charging) return;

            _scanTimer += Time.deltaTime;
            if (_scanTimer < ScanInterval) return;
            _scanTimer = 0f;

            if (!_attackCd.IsReady(Time.time)) return;

            int idx = FindTarget();
            if (idx < 0) return;

            // CD をここで消費して発射時刻の基準を確定し、チャージ予兆を前倒しで再生する。
            // チャージ分だけ発射が遅れるが、CD 消費が一定間隔なのでファイアレートは変わらない。
            if (!_attackCd.TryConsume(Time.time)) return;
            StartCoroutine(ChargeThenFire(_cachedPositions[idx]));
        }

        // ChargeLead 秒かけてクリスタルを発光+スケールパルスさせ、その後に発射する。
        private IEnumerator ChargeThenFire(Vector3 targetPos)
        {
            _charging = true;

            var color = ChargeColor();
            // チャージ中に小バーストを2回（前半と中盤）
            if (_crystal != null)
                SkillVfx.SpawnBurst(_crystal.position, color, 0.2f, 0.9f, 0.25f);

            GameSfx.Play("tower_charge", _crystal != null ? _crystal.position : transform.position, 0.8f);

            float elapsed = 0f;
            bool secondBurstDone = false;
            while (elapsed < ChargeLead)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / ChargeLead);

                // localScale を 1→1.15→1 へパルス（中央でピーク）
                if (_crystal != null)
                {
                    float pulse = 1f + 0.15f * Mathf.Sin(t * Mathf.PI);
                    _crystal.localScale = _crystalBaseScale * pulse;
                }

                if (!secondBurstDone && t >= 0.5f && _crystal != null)
                {
                    SkillVfx.SpawnBurst(_crystal.position, color, 0.25f, 1.1f, 0.25f);
                    secondBurstDone = true;
                }

                yield return null;
            }

            if (_crystal != null)
                _crystal.localScale = _crystalBaseScale;

            // チャージ中に撃破されたら発射しない
            if (!_dead)
                FireAt(targetPos);
            _charging = false;
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
            // CD は ChargeThenFire 開始時に消費済み（ここでは消費しない）

            // 銃口が高所(クリスタル)にあるため、対象の胴体高さを狙う3D方向で撃つ
            var dir = (targetPos + Vector3.up * 0.9f - _muzzle.position);
            if (dir.sqrMagnitude < 0.001f) return;
            dir.Normalize();

            float lifetime = ProjectileSpeed > 0f ? Range / ProjectileSpeed : 2f;
            var proj = Instantiate(_projectilePrefab, _muzzle.position, Quaternion.identity);

            // 敵チームの TowerWeaken でタワーの与ダメを減衰させる
            TeamId myTeam = _teamTag != null ? _teamTag.Team : TeamId.Neutral;
            TeamId opp = myTeam == TeamId.Blue ? TeamId.Red : (myTeam == TeamId.Red ? TeamId.Blue : TeamId.Neutral);
            float weaken = (GameServices.ObjectiveBuffs != null && opp != TeamId.Neutral)
                ? GameServices.ObjectiveBuffs.GetMagnitude(opp, ObjectiveBuffType.TowerWeaken, Time.time) : 0f;
            float dmg = Damage * Mathf.Clamp01(1f - weaken);
            proj.Init(dir, ProjectileSpeed, dmg, gameObject, lifetime);

            // ボルト演出: チーム色トレイル + 細長い発光コア + 発射バースト
            var color = ChargeColor();
            SkillVfx.AddTrail(proj.gameObject, color, 0.18f, 0.3f);
            SkillVfx.AttachGlowCore(proj.gameObject, dir, color, 0.3f, 1.1f);
            SkillVfx.SpawnBurst(_muzzle.position, color, 0.25f, 1.0f, 0.2f);
            GameSfx.Play("tower_fire", _muzzle.position, 0.9f);
        }

        private void OnDied()
        {
            _dead = true;

            // 倒壊演出は DeathPresenter(Sink) が担うため、ここでは傾けない（二重演出回避）。
            GameSfx.Play("tower_destroyed", transform.position, 1f);

            // コライダーを無効化して通行可能にする
            foreach (var col in GetComponents<Collider>())
                col.enabled = false;
        }
    }
}
