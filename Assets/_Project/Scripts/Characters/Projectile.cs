using UnityEngine;
using Enigma.Ability;
using Enigma.Audio;
using Enigma.Combat;

namespace Enigma.Character
{
    // コライダーは isTrigger 前提（BuildAetherRiftMap エディタスクリプト側で設定）
    public sealed class Projectile : MonoBehaviour
    {
        private Vector3 _direction;
        private float _speed;
        private float _damage;
        private GameObject _owner;
        private float _lifeTimer;
        private float _stun, _root, _slowStrength, _slowDuration;
        private float _pullDistance;

        // プル時のめり込み防止の最小離隔(m)。発射者の目の前まで詰めさせない。
        private const float MinPullSeparation = 2f;

        // 着弾時のネオン演出色（キャラ別）。SetImpactColors で結線したときのみ発動する。
        private bool _impactColorsSet;
        private Color _impactPrimary, _impactSecondary;

        private const float DefaultLifetime = 1.5f;

        public void Init(Vector3 direction, float speed, float damage, GameObject owner,
                         float lifetime = DefaultLifetime)
        {
            _direction = direction.normalized;
            _speed     = speed;
            _damage    = damage;
            _owner     = owner;
            _lifeTimer = 0f;
            _lifetime  = lifetime > 0f ? lifetime : DefaultLifetime;
        }

        // 対象追尾(ホーミング)弾。AA/タワー弾は「対象指定=ロックした相手に必ず当たる」仕様
        // (2026-07-05 ユーザー指定)のため、飛行中も対象へ向きを更新し続ける。対象が飛行中に
        // 死亡/消滅した場合は最後の方向へ直進に切り替える。スキルの方向指定弾は従来どおり
        // Init を使い、この経路には乗らない。
        public void InitHoming(HealthComponent target, float speed, float damage, GameObject owner,
                               float lifetime = DefaultLifetime)
        {
            Init((AimPoint(target.transform) - transform.position).normalized,
                speed, damage, owner, lifetime);
            _homingTarget = target;
        }

        private HealthComponent _homingTarget;

        private static Vector3 AimPoint(Transform t) => t.position + Vector3.up * 1.0f;

        private float _lifetime = DefaultLifetime;

        /// <summary>着弾時にネオン演出を出す色を設定する（キャラ別 AttackVfxProfile から）。</summary>
        public void SetImpactColors(Color primary, Color secondary)
        {
            _impactPrimary   = primary;
            _impactSecondary = secondary;
            _impactColorsSet = true;
        }

        public void SetStatusEffects(float stun, float root, float slowStrength, float slowDuration)
        {
            _stun         = stun;
            _root         = root;
            _slowStrength = slowStrength;
            _slowDuration = slowDuration;
        }

        /// <summary>命中した敵を発射者側へ引き寄せる距離(m)を設定する（thorne Q 等）。0以下=なし。</summary>
        public void SetPullDistance(float pullDistance)
        {
            _pullDistance = pullDistance;
        }

        private void Update()
        {
            _lifeTimer += Time.deltaTime;
            if (_lifeTimer >= _lifetime)
            {
                Destroy(gameObject);
                return;
            }

            if (_homingTarget != null && !_homingTarget.Model.IsDead)
            {
                _direction = (AimPoint(_homingTarget.transform) - transform.position).normalized;
                transform.rotation = Quaternion.LookRotation(_direction);
            }

            transform.Translate(_direction * (_speed * Time.deltaTime), Space.World);
        }

        private void OnTriggerEnter(Collider other)
        {
            // オーナー自身と Trigger は無視
            if (other.isTrigger) return;
            if (_owner != null && other.gameObject == _owner) return;

            // 味方には当たらず素通りさせる。ここで Destroy すると味方の体で射線が
            // 塞がってしまうため、ダメージも消滅もせずそのまま貫通させる。
            if (!TeamRules.CanDamage(ResolveTeam(_owner), ResolveTeam(other.gameObject)))
                return;

            var damageable = other.GetComponent<IDamageable>();
            if (damageable != null)
            {
                float finalDamage = DamageUtility.ApplyTeamBuff(_damage, _owner, other.gameObject);
                if (damageable is HealthComponent hc)
                {
                    hc.TakeDamage(finalDamage, _owner);
                    ApplyPullTo(hc.gameObject);
                    ApplyStatusTo(hc.gameObject);

                    // 操作プレイヤーが当てた一撃だけ手応え演出(微シェイク+大技ヒットストップ)
                    if (_owner != null && _owner.GetComponent<PlayerController>() != null)
                        Enigma.Vfx.AttackJuice.PlayerLandedHit(finalDamage, hc.Model.MaxHp, false);
                }
                else
                    damageable.TakeDamage(finalDamage);

                // タワー弾は重い着弾音、それ以外は AA ヒットのバリアントを鳴らす
                if (_owner != null && _owner.GetComponent<Enigma.Objective.TowerAttack>() != null)
                    GameSfx.Play("tower_hit", transform.position, 0.9f);
                else
                    GameSfx.PlayVariant("aa_hit", 3, transform.position, 0.55f);
            }

            // 着弾の小バースト。AA 連射のスパムに耐えるよう小さく短命(0.25s)に。
            // 色はトレイルがあればその色、無ければ白
            var hitColor = TryGetTrailColor();
            SkillVfx.SpawnBurst(transform.position, hitColor, 0.15f, 0.7f, 0.25f);

            // 操作プレイヤーの一撃のみ、キャラ別カラーでネオン着弾演出を出す（重い演出のため限定）。
            if (_impactColorsSet && _owner != null && _owner.GetComponent<PlayerController>() != null)
                Enigma.Vfx.NeonImpactEffect.Spawn(transform.position, _impactPrimary, _impactSecondary);

            Destroy(gameObject);
        }

        // 命中した敵を発射者側へ _pullDistance だけ引き寄せる(thorne Q チェーンフック等)。
        // y は変位させず、対象自身の CharacterController.Move に水平移動のみを渡すことで、
        // 起伏・重力の解決を既存の移動システム(CC+重力)に任せる(テレポート的な y 直指定はしない)。
        private void ApplyPullTo(GameObject go)
        {
            if (_pullDistance <= 0f || _owner == null) return;

            var cc = go.GetComponent<CharacterController>();
            if (cc == null) return;

            Vector3 targetPos = go.transform.position;
            Vector3 newPos = Enigma.Combat.PullDisplacementLogic.PullTarget(
                _owner.transform.position, targetPos, _pullDistance, MinPullSeparation);

            Vector3 delta = newPos - targetPos;
            delta.y = 0f;
            if (delta.sqrMagnitude > 0.0001f)
                cc.Move(delta);
        }

        private void ApplyStatusTo(GameObject go)
        {
            if (_stun <= 0f && _root <= 0f && _slowStrength <= 0f) return;
            var sc = Enigma.Combat.StatusEffectController.GetOrAdd(go);
            if (sc == null) return;
            if (_stun > 0f) sc.ApplyStun(_stun);
            if (_root > 0f) sc.ApplyRoot(_root);
            if (_slowStrength > 0f && _slowDuration > 0f) sc.ApplySlow(_slowStrength, _slowDuration);
        }

        // 弾に付いたトレイル色をヒット演出色として流用する。無ければ白。
        private Color TryGetTrailColor()
        {
            return TryGetComponent<TrailRenderer>(out var trail) ? trail.startColor : Color.white;
        }

        // TeamTag が無い側は中立扱い（誰にでも当たる）。
        private static TeamId ResolveTeam(GameObject go)
        {
            if (go == null) return TeamId.Neutral;
            var tag = go.GetComponentInParent<TeamTag>();
            return tag != null ? tag.Team : TeamId.Neutral;
        }
    }
}
