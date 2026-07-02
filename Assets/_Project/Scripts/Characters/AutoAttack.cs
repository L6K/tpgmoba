using UnityEngine;
using Enigma.Audio;
using Enigma.Combat;
using Enigma.Ability;
using Enigma.Vfx;

namespace Enigma.Character
{
    // TargetingSystem.CurrentTarget が射程内であれば CD ごとに自動発射
    public sealed class AutoAttack : MonoBehaviour
    {
        [SerializeField] private Projectile        _projectilePrefab;
        [SerializeField] private Transform         _muzzle;
        [SerializeField] private PlayerAttackMotor _motor;

        // characters.json を正としてランタイム/インポータから上書きできるよう、定数からフィールドへ昇格（既定値は従来の定数値）
        [SerializeField] private float _attackDamage   = 15f;
        [SerializeField] private float _projectileSpeed = 30f;
        [SerializeField] private float _attackCooldown = 1.5f;
        [SerializeField] private float _attackRange    = 12f;

        // 射程リング表示など外部が現在の AA 射程を参照するための公開プロパティ。
        // ピックや Configure で変動するため都度読み取りたい。
        public float AttackRange => _attackRange;

        private const float AutoWindup   = 0.15f;
        private const float AutoRecovery = 0.25f;

        // この射程以下のキャラは飛翔弾を使わず即時近接斬撃にする（ガロン3.5/ヴェイル4/ソーン3.5）
        private const float MeleeRangeThreshold = 7f;

        // 設定された射程が閾値以下なら近接キャラと判定
        private bool IsMelee => _attackRange <= MeleeRangeThreshold;

        // AA モーション中(準備〜後隙)にターゲットへ向き直る速度
        private const float FaceTurnSpeed = 14f;

        private AttackCooldown         _cooldown;
        private TargetingSystem        _targeting;
        private Transform              _faceTarget;
        private StatusEffectController _statusEffects;

        // AA ビーム/マズルのネオン着色に使う champion 別プロファイル。
        // 未設定(ピック未適用)時は既定の Zeph。MatchBootstrap / BotChampionBootstrap が CharId で設定する
        private ChampionVfx _championVfx = ChampionVfx.Zeph;

        /// <summary>characters.json の id（"zeph"/"garon"…）から VFX プロファイルを解決して保持する。</summary>
        public void SetChampion(string charId) => _championVfx = AttackVfxProfiles.Parse(charId);

        // MatchBootstrap など composition root からピック済みステータスを反映する。
        // Awake 済みなら CD インスタンスも作り直す
        public void Configure(float damage, float range, float cooldownSeconds)
        {
            _attackDamage   = damage;
            _attackRange    = range;
            _attackCooldown = cooldownSeconds;
            if (_cooldown != null)
                _cooldown = new AttackCooldown(_attackCooldown);
        }

        private void Awake()
        {
            _cooldown      = new AttackCooldown(_attackCooldown);
            _targeting     = GetComponent<TargetingSystem>();
            _statusEffects = StatusEffectController.GetOrAdd(gameObject);
        }

        private void Update()
        {
            FaceTargetDuringMotion();

            if (_targeting == null) return;

            var target = _targeting.CurrentTarget;
            if (target == null) return;

            float dist = Vector3.Distance(transform.position, target.transform.position);
            if (dist > _attackRange) return;

            // Windup 中はクールダウンを消費しない
            if (_motor != null && _motor.Motion.Phase == AttackPhase.Windup) return;

            if (_statusEffects != null && !_statusEffects.CanAct) return;
            if (!_cooldown.TryConsume(Time.time)) return;
            // 近接キャラは飛翔弾を使わないため、ここでは _muzzle のみ必須にする。
            // 弾プレハブの null 検査は遠距離経路の FireProjectile 側でのみ行う。
            if (_muzzle == null) return;

            if (_motor != null)
            {
                // Strike 時点のターゲット位置を使うためキャプチャ
                HealthComponent capturedTarget = target;
                _faceTarget = capturedTarget.transform;
                _motor.RequestAttack(AutoWindup, AutoRecovery, () =>
                {
                    _motor.SnapToLunge();
                    // 近接キャラは飛翔弾を出さず即時斬撃。ランジ前進は近接にも自然に合う
                    if (IsMelee)
                        StrikeMelee(capturedTarget);
                    else
                        FireProjectile(capturedTarget);
                });
            }
            else
            {
                // _motor 未設定時は従来どおり即時発射（後方互換）。近接キャラは斬撃
                if (IsMelee)
                    StrikeMelee(target);
                else
                    FireProjectile(target);
            }
        }

        // AA モーション中(準備〜後隙)はターゲットの方向へ滑らかに向き直る。
        // 移動入力による向きは PlayerController が担うが、Windup 中は移動がロックされるため競合しない
        private void FaceTargetDuringMotion()
        {
            if (_motor == null || _faceTarget == null) return;

            if (_motor.Motion.Phase == AttackPhase.None)
            {
                _faceTarget = null;
                return;
            }

            var flat = _faceTarget.position - transform.position;
            flat.y = 0f;
            if (flat.sqrMagnitude < 0.01f) return;

            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                Quaternion.LookRotation(flat),
                FaceTurnSpeed * Time.deltaTime);
        }

        private void StrikeMelee(HealthComponent target)
        {
            if (target == null || _muzzle == null) return;

            var dir = target.transform.position - transform.position;
            dir.y = 0f;
            dir = dir.sqrMagnitude > 0.001f ? dir.normalized : transform.forward;

            // 命中（飛翔なしの即時ヒット。ターゲットは射程内確定）
            float finalDamage = DamageUtility.ApplyTeamBuff(_attackDamage, gameObject, target.gameObject);
            target.TakeDamage(finalDamage, gameObject);

            // 操作プレイヤーの一撃だけ手応え演出（Projectile と同じ条件）
            bool isPlayer = GetComponent<PlayerController>() != null;
            if (isPlayer)
                Enigma.Vfx.AttackJuice.PlayerLandedHit(finalDamage, target.Model.MaxHp, false);

            // 斬撃VFX: キャラ別カラーで前方に一閃 + 接触バースト
            var profile = AttackVfxProfiles.For(_championVfx);
            Color slashColor = SkillVfx.ToColor(profile.Primary, profile.EmissionIntensity);
            Vector3 contact  = target.transform.position + Vector3.up * 1.0f;
            Vector3 center   = transform.position + dir * (_attackRange * 0.5f) + Vector3.up * 1.0f;
            Vector3 rightV   = Vector3.Cross(Vector3.up, dir);
            // 横薙ぎの一閃（dir に対し左右に振る短いストローク）
            SkillVfx.SpawnBeam(center - rightV * 1.2f + dir * 0.3f, center + rightV * 1.2f - dir * 0.3f, slashColor, 0.25f, 0.18f);
            SkillVfx.SpawnBurst(contact, slashColor, 0.15f, 0.9f, 0.22f);

            // 操作プレイヤーの一撃のみネオン着弾（Projectile と同じ限定）
            if (isPlayer)
                Enigma.Vfx.NeonImpactEffect.Spawn(contact, SkillVfx.ToColor(profile.Primary), SkillVfx.ToColor(profile.Secondary));

            GameSfx.PlayVariant("aa_hit", 3, contact, 0.55f);
        }

        private bool _warnedMissingProjectile;

        private void FireProjectile(HealthComponent target)
        {
            if (target == null || _muzzle == null) return;
            if (_projectilePrefab == null)
            {
                // シーン再生成でプレハブ fileID 参照が切れると無音で空振りし続けバグ発見が遅れる。
                // 一度だけ警告して結線切れを可視化する(Sandbox の AaBeam 参照切れの実績あり)。
                if (!_warnedMissingProjectile)
                {
                    _warnedMissingProjectile = true;
                    Debug.LogWarning($"[AutoAttack] {name}: _projectilePrefab が未結線のため遠距離AAが発射できません(シーンの参照切れ疑い)", this);
                }
                return;
            }
            var dir = (target.transform.position - _muzzle.position).normalized;
            // ビーム見た目を進行方向へ向けるため LookRotation を与える
            var proj = Instantiate(_projectilePrefab, _muzzle.position, Quaternion.LookRotation(dir));
            proj.Init(dir, _projectileSpeed, _attackDamage, gameObject);

            // champion 別ネオン着色: 弾本体/トレイルを per-instance で染め、発射口にフラッシュ。
            // 連続ヒットのコンボ倍率で発光/トレイル幅を段階的に派手化する。
            var profile = AttackVfxProfiles.For(_championVfx);
            SkillVfx.TintBeamProjectile(proj.gameObject, profile, Enigma.Vfx.AttackJuice.ComboMultiplier);
            SkillVfx.SpawnMuzzleFlash(_muzzle.position, dir, profile);

            GameSfx.PlayVariant("aa_fire", 3, _muzzle.position, 0.7f);
        }
    }
}
