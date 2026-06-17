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

        private float _lifetime = DefaultLifetime;

        public void SetStatusEffects(float stun, float root, float slowStrength, float slowDuration)
        {
            _stun         = stun;
            _root         = root;
            _slowStrength = slowStrength;
            _slowDuration = slowDuration;
        }

        private void Update()
        {
            _lifeTimer += Time.deltaTime;
            if (_lifeTimer >= _lifetime)
            {
                Destroy(gameObject);
                return;
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

            Destroy(gameObject);
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
