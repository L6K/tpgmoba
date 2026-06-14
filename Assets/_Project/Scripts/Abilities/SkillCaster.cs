using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using Enigma.Abilities;
using Enigma.Audio;
using Enigma.Character;
using Enigma.Combat;
using Enigma.Core;
using Enigma.Data;

namespace Enigma.Ability
{
    // スキル入力受付・インジケーター表示・発動を担うハンブルオブジェクト
    public sealed class SkillCaster : MonoBehaviour
    {
        [SerializeField] private SkillDefinition[]  _skills = new SkillDefinition[4];
        [SerializeField] private Projectile         _projectilePrefab;
        [SerializeField] private TelegraphCircle    _telegraphPrefab;
        [SerializeField] private GameObject         _directionIndicator;  // 子。カーソル方向に向ける矢印
        [SerializeField] private GameObject         _aoeIndicator;        // 子。AoE 地点の円
        [SerializeField] private TargetingSystem    _targeting;
        [SerializeField] private Transform          _muzzle;
        [SerializeField] private PlayerAttackMotor  _motor;

        // スロットごとのクールダウン
        private readonly AttackCooldown[] _cooldowns = new AttackCooldown[4];
        private CastModeLogic _castLogic;
        private CastMode      _lastSyncedMode = (CastMode)(-1); // 未初期化値

        // スキルランク進行（LoL 式）。HUD/外部から参照する
        private readonly SkillProgression _progression = new();
        public SkillProgression Progression => _progression;

        // レベルアップ購読元（同一 GO の PlayerProgression を Awake で解決）
        private PlayerProgression _playerProgression;

        // スロット色: Q=シアン, E=マゼンタ, R=ゴールド
        private static readonly Color[] _slotColors =
        {
            Color.cyan,
            Color.magenta,
            new Color(1f, 0.84f, 0.2f, 1f),
            Color.white,
        };

        // カーソル→地面の交差点（毎フレーム更新）
        private Vector3 _groundCursorPos;

        private StatusEffectController _statusEffects;
        private PlayerController _playerController;
        private HealthComponent _health;

        private void Awake()
        {
            for (int i = 0; i < 4; i++)
            {
                var def = _skills[i];
                float cd = def != null ? def.CooldownSeconds : 1f;
                _cooldowns[i] = new AttackCooldown(cd);
            }

            // 最初のモード同期は Update で行う（GameServices が未初期化の可能性）
            _castLogic = new CastModeLogic(CastMode.QuickWithIndicator);

            // 同一 GO のレベル進行を解決（チャンピオンレベルとポイント付与に使用）
            _playerProgression = GetComponent<PlayerProgression>();

            SetIndicatorActive(null);
            _statusEffects = StatusEffectController.GetOrAdd(gameObject);
            _playerController = GetComponent<PlayerController>();
            _health = GetComponent<HealthComponent>();
        }

        private void OnEnable()
        {
            if (_playerProgression != null)
                _playerProgression.Experience.LevelChanged += OnChampionLevelChanged;
        }

        private void OnDisable()
        {
            if (_playerProgression != null)
                _playerProgression.Experience.LevelChanged -= OnChampionLevelChanged;
        }

        // チャンピオンレベルアップ毎にスキルポイントを 1 付与
        private void OnChampionLevelChanged(int newLevel)
        {
            _progression.OnChampionLevelUp();
        }

        /// <summary>現在のチャンピオンレベル（1〜）。PlayerProgression 未設定時は 1。</summary>
        public int ChampionLevel => _playerProgression != null ? _playerProgression.Experience.Level : 1;

        // 「総ポイント数(未使用+取得済みランク) = チャンピオンレベル」の不変条件を毎フレーム強制する。
        // イベント購読のタイミング問題(参照の遅延結線・差し替え等)があってもレベル分のポイントへ追いつく
        private void SyncSkillPointsToLevel()
        {
            if (_playerProgression == null)
            {
                _playerProgression = GetComponent<PlayerProgression>();
                if (_playerProgression == null) return;
                _playerProgression.Experience.LevelChanged += OnChampionLevelChanged;
            }

            int have = _progression.UnspentPoints
                       + _progression.GetRank(0) + _progression.GetRank(1) + _progression.GetRank(2);
            int level = ChampionLevel;
            while (have < level)
            {
                _progression.OnChampionLevelUp();
                have++;
            }
        }

        private void Update()
        {
            // プレイ開始直後の初期化前 Update 対策(TowerAttack 同様の防御)
            if (_castLogic == null) return;

            SyncSkillPointsToLevel();
            SyncCastMode();
            UpdateGroundCursor();

            var keyboard = Keyboard.current;
            var mouse    = Mouse.current;
            if (keyboard == null || mouse == null) return;

            bool isArmed = _castLogic.ArmedSlot >= 0;
            bool canAct  = _statusEffects == null || _statusEffects.CanAct;

            // アーム中インジケーター更新
            if (isArmed)
            {
                UpdateArmedIndicator(_castLogic.ArmedSlot);
            }

            // Normal モード: アーム中の左クリックはスキル確定（TargetingSystem より優先）
            if (canAct && isArmed && _lastSyncedMode == CastMode.Normal && mouse.leftButton.wasPressedThisFrame)
            {
                // TargetingSystem のクリックペンディングを横取り
                _targeting?.CancelPendingClick();
                int cachedSlot = _castLogic.ArmedSlot;
                var action = _castLogic.HandleConfirm();
                ExecuteIfCast(action, cachedSlot);
                SetIndicatorActive(null);
                return;
            }

            // ESC / 右クリックでキャンセル（アーム中のみ）
            if (isArmed)
            {
                bool cancel = (keyboard.escapeKey.wasPressedThisFrame)
                           || (mouse.rightButton.wasPressedThisFrame);
                if (cancel)
                {
                    // 右クリックのペンディングを横取りし、TargetingSystem の右クリック選択が誤発動しないようにする
                    if (mouse.rightButton.wasPressedThisFrame)
                        _targeting?.CancelPendingRightClick();
                    _castLogic.HandleCancel();
                    SetIndicatorActive(null);
                    return;
                }
            }

            // スロット 0..3 の入力処理
            for (int slot = 0; slot < 4; slot++)
            {
                var def = _skills[slot];
                if (def == null) continue;

                var key = GameServices.ControlSettings?.GetSkillKey(slot) ?? GetFallbackKey(slot);
                if (key == Key.None) continue;

                var keyControl = GetKeyControl(keyboard, key);
                if (keyControl == null) continue;

                if (keyControl.wasPressedThisFrame)
                {
                    if (!canAct) continue;
                    // rank 0（未習得）のスキルは発動不可
                    if (!IsSlotUnlocked(slot)) continue;

                    // CD 中はアーム開始しない
                    if (!_cooldowns[slot].IsReady(Time.time)) continue;

                    bool isInstant = def.Targeting == SkillTargeting.Targeted || def.Targeting == SkillTargeting.TargetedAlly;
                    var action = _castLogic.HandleKeyDown(slot, isInstant);
                    if (action == CastAction.Cast)
                    {
                        TryCast(slot);
                    }
                    else if (action == CastAction.ShowIndicator)
                    {
                        SetIndicatorActive(def);
                        UpdateArmedIndicator(slot);
                    }
                }
                else if (keyControl.wasReleasedThisFrame)
                {
                    var action = _castLogic.HandleKeyUp(slot);
                    if (action == CastAction.Cast)
                    {
                        TryCast(slot);
                        SetIndicatorActive(null);
                    }
                }
            }
        }

        // --- ヘルパー ---

        private void SyncCastMode()
        {
            var cs = GameServices.ControlSettings;
            if (cs == null) return;

            var currentMode = cs.CastMode;
            if (currentMode == _lastSyncedMode) return;

            _lastSyncedMode = currentMode;
            _castLogic.SyncMode(currentMode);
            SetIndicatorActive(null);
        }

        private void UpdateGroundCursor()
        {
            var cam = Camera.main;
            if (cam == null) return;

            var mouse = Mouse.current;
            if (mouse == null) return;

            var ray   = cam.ScreenPointToRay(new Vector3(mouse.position.ReadValue().x, mouse.position.ReadValue().y, 0f));
            var plane = new Plane(Vector3.up, new Vector3(0f, transform.position.y, 0f));

            if (plane.Raycast(ray, out float enter))
            {
                _groundCursorPos = ray.GetPoint(enter);
            }
        }

        private void UpdateArmedIndicator(int slot)
        {
            var def = (slot >= 0 && slot < 4) ? _skills[slot] : null;
            if (def == null) return;

            if (def.Targeting == SkillTargeting.Directional && _directionIndicator != null)
            {
                var dir = (_groundCursorPos - transform.position);
                dir.y = 0f;
                if (dir.sqrMagnitude > 0.001f)
                    _directionIndicator.transform.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
            }
            else if (def.Targeting == SkillTargeting.GroundAoe && _aoeIndicator != null)
            {
                var dir    = _groundCursorPos - transform.position;
                dir.y      = 0f;
                float dist = Mathf.Min(dir.magnitude, def.Range);
                var   pos  = transform.position + dir.normalized * dist;
                pos.y      = transform.position.y + 0.05f;
                _aoeIndicator.transform.position = pos;
            }
        }

        private void TryCast(int slot)
        {
            var def = (slot >= 0 && slot < 4) ? _skills[slot] : null;
            if (def == null) return;

            // モーターが存在し Windup 中なら新規発動を受け付けない（クールダウンも消費しない）
            if (_motor != null && _motor.Motion.Phase == AttackPhase.Windup) return;

            if (!_cooldowns[slot].TryConsume(Time.time)) return;

            if (_motor != null)
            {
                // Strike 時点のカーソル位置/ターゲットを使うため、クロージャで現在値を参照
                var capturedGroundPos = _groundCursorPos;
                HealthComponent capturedTarget = _targeting?.CurrentTarget;
                var capturedDef       = def;

                int cachedSlot = slot;
                _motor.RequestAttack(def.WindupSeconds, def.RecoverySeconds, () =>
                {
                    _motor.SnapToLunge();
                    FireSkill(cachedSlot, capturedDef, capturedGroundPos, capturedTarget);
                });
            }
            else
            {
                // _motor 未設定時は従来どおり即時発動（後方互換）
                FireSkill(slot, def, _groundCursorPos, _targeting?.CurrentTarget);
            }
        }

        private void FireSkill(int slot, SkillDefinition def, Vector3 groundCursorPos, HealthComponent target)
        {
            // ランクに応じたダメージ倍率（rank0 はここに到達しない想定だが安全に等倍以上）
            float scale = DamageScale(slot);

            switch (def.Targeting)
            {
                case SkillTargeting.Directional:  CastDirectional(slot, def, groundCursorPos, scale); break;
                case SkillTargeting.GroundAoe:    CastGroundAoe(slot, def, groundCursorPos, scale); break;
                case SkillTargeting.Targeted:     CastTargeted(slot, def, target, scale); break;
                case SkillTargeting.TargetedAlly: CastTargetedAlly(slot, def, scale); break;
            }

            if (def.Targeting != SkillTargeting.TargetedAlly)
                ApplySelfBuffs(def);            // shield/heal を自分へ(TargetedAlly は味方へ別途)
            TryDash(def, groundCursorPos, target);
        }

        private void CastDirectional(int slot, SkillDefinition def, Vector3 groundCursorPos, float scale)
        {
            if (_projectilePrefab == null || _muzzle == null) return;

            var dir = (groundCursorPos - _muzzle.position);
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.001f) dir = transform.forward;
            dir.Normalize();

            // 射程÷速度で弾が自然消滅する寿命を設定
            float lifetime = def.ProjectileSpeed > 0f ? def.Range / def.ProjectileSpeed : 1.5f;

            var proj = Instantiate(_projectilePrefab, _muzzle.position, Quaternion.identity);
            proj.Init(dir, def.ProjectileSpeed, def.Damage * scale, gameObject, lifetime);
            proj.SetStatusEffects(def.StunDuration, def.RootDuration, def.SlowStrength, def.SlowDuration);

            // 発光コア + トレイル + 二段バースト（白コア小 + スロット色大）
            var color = SlotColor(slot);
            SkillVfx.FireDirectionalVisuals(proj.gameObject, _muzzle.position, dir, color);
            GameSfx.Play("skill_q_fire", _muzzle.position);
        }

        private void CastGroundAoe(int slot, SkillDefinition def, Vector3 groundCursorPos, float scale)
        {
            if (_telegraphPrefab == null) return;

            var dir    = groundCursorPos - transform.position;
            dir.y      = 0f;
            float dist = Mathf.Min(dir.magnitude, def.Range);
            var   pos  = transform.position + (dir.sqrMagnitude > 0.001f ? dir.normalized * dist : transform.forward * dist);
            pos.y      = transform.position.y;

            var telegraph = Instantiate(_telegraphPrefab, pos, Quaternion.identity);
            telegraph.Init(def.Radius, 0.8f, def.Damage * scale, gameObject);
            telegraph.SetStatusEffects(def.StunDuration, def.RootDuration, def.SlowStrength, def.SlowDuration);

            // マズルバースト + 着弾地点に大きめバースト
            var color = SlotColor(slot);
            SkillVfx.SpawnBurst(_muzzle != null ? _muzzle.position : transform.position, color, 0.3f, 1.2f, 0.25f);
            SkillVfx.SpawnBurst(pos, color, 1f, 4f, 0.4f);
        }

        private void CastTargeted(int slot, SkillDefinition def, HealthComponent target, float scale)
        {
            if (target == null) return;

            float dist = Vector3.Distance(transform.position, target.transform.position);
            if (dist > def.Range) return;

            // 味方は対象指定スキルでダメージを受けない
            if (!CanDamageTarget(target.gameObject)) return;

            float finalDamage = DamageUtility.ApplyTeamBuff(def.Damage * scale, gameObject);
            target.TakeDamage(finalDamage, gameObject);

            var sc = StatusEffectController.GetOrAdd(target.gameObject);
            if (sc != null)
            {
                if (def.StunDuration > 0f) sc.ApplyStun(def.StunDuration);
                if (def.RootDuration > 0f) sc.ApplyRoot(def.RootDuration);
                if (def.SlowStrength > 0f && def.SlowDuration > 0f) sc.ApplySlow(def.SlowStrength, def.SlowDuration);
            }

            // 胸元→対象へビーム一閃 + 対象位置にバースト+小リング
            var color = SlotColor(slot);
            var from  = ChestPoint(transform);
            var to    = ChestPoint(target.transform);
            SkillVfx.SpawnBurst(_muzzle != null ? _muzzle.position : from, color, 0.3f, 1.2f, 0.25f);
            SkillVfx.TargetedHitVisuals(from, to, color);
            GameSfx.Play("skill_r_beam", _muzzle != null ? _muzzle.position : from);
            GameSfx.Play("skill_r_hit", target.transform.position, 0.8f);
        }

        // shield/heal を自分へ適用
        private void ApplySelfBuffs(SkillDefinition def)
        {
            if (_health == null) return;
            if (def.HealAmount > 0f) _health.Model.Heal(def.HealAmount);
            if (def.ShieldAmount > 0f && def.ShieldDuration > 0f) _health.Model.AddShield(def.ShieldAmount, def.ShieldDuration);
        }

        // dash(自分)。Targeted は対象方向、それ以外はカーソル方向へ。
        private void TryDash(SkillDefinition def, Vector3 groundCursorPos, HealthComponent target)
        {
            if (def.DashDistance <= 0f || _playerController == null) return;
            Vector3 dir = (def.Targeting == SkillTargeting.Targeted && target != null)
                ? (target.transform.position - transform.position)
                : (groundCursorPos - transform.position);
            _playerController.RequestDash(dir, def.DashDistance);
        }

        // 味方対象の回復+シールド。カーソル下の味方を探し、無ければ自分。
        private void CastTargetedAlly(int slot, SkillDefinition def, float scale)
        {
            var ally = ResolveAllyUnderCursor(def.Range);
            if (ally == null) ally = _health;
            if (ally == null) return;
            if (def.HealAmount > 0f) ally.Model.Heal(def.HealAmount);
            if (def.ShieldAmount > 0f && def.ShieldDuration > 0f) ally.Model.AddShield(def.ShieldAmount, def.ShieldDuration);

            // 演出: 胸元→対象へビーム + 対象にヒーリングバースト(緑系)
            var color = new Color(0.36f, 0.84f, 0.42f, 1f);
            var from  = ChestPoint(transform);
            var to    = ChestPoint(ally.transform);
            SkillVfx.TargetedHitVisuals(from, to, color);
            SkillVfx.SpawnBurst(ally.transform.position, color, 0.5f, 2.5f, 0.4f);
            GameSfx.Play("skill_r_hit", ally.transform.position, 0.8f);
        }

        // カーソル下にいる味方(同チーム, 射程内, 自分以外)を返す。無ければ null。
        private HealthComponent ResolveAllyUnderCursor(float range)
        {
            var cam = Camera.main; var mouse = Mouse.current;
            if (cam == null || mouse == null) return null;
            var ray = cam.ScreenPointToRay(new Vector3(mouse.position.ReadValue().x, mouse.position.ReadValue().y, 0f));
            if (!Physics.Raycast(ray, out var hit, 200f)) return null;
            var hc = hit.collider.GetComponentInParent<HealthComponent>();
            if (hc == null || hc.gameObject == gameObject) return null;
            // 同チームのみ(CanDamageTarget が false = 味方)
            if (CanDamageTarget(hc.gameObject)) return null;
            if (Vector3.Distance(transform.position, hc.transform.position) > range) return null;
            return hc;
        }

        // キャスター/対象の「胸元」高さ(足元 +1.2m)を返す。ビームの見栄え用。
        private static Vector3 ChestPoint(Transform t)
        {
            return t.position + Vector3.up * 1.2f;
        }

        private void SetIndicatorActive(SkillDefinition armedDef)
        {
            if (_directionIndicator != null)
                _directionIndicator.SetActive(armedDef != null && armedDef.Targeting == SkillTargeting.Directional);
            if (_aoeIndicator != null)
                _aoeIndicator.SetActive(armedDef != null && armedDef.Targeting == SkillTargeting.GroundAoe);
        }

        // スロットが習得済み（rank>=1）か。進行管理外のスロット3は常に習得扱い
        private bool IsSlotUnlocked(int slot)
        {
            if (slot < 0 || slot > 2) return true;
            return _progression.GetRank(slot) >= 1;
        }

        // スロットのランク倍率。進行管理外スロットは等倍
        private float DamageScale(int slot)
        {
            if (slot < 0 || slot > 2) return 1f;
            return SkillProgression.DamageMultiplier(_progression.GetRank(slot));
        }

        private static Color SlotColor(int slot)
        {
            return (slot >= 0 && slot < _slotColors.Length) ? _slotColors[slot] : Color.white;
        }

        // 味方（同チーム）にはスキルダメージを与えない。TeamTag が無い側は中立扱いで攻撃可。
        private bool CanDamageTarget(GameObject target)
        {
            var myTag     = GetComponentInParent<TeamTag>();
            var otherTag  = target != null ? target.GetComponentInParent<TeamTag>() : null;
            var myTeam    = myTag    != null ? myTag.Team    : TeamId.Neutral;
            var otherTeam = otherTag != null ? otherTag.Team : TeamId.Neutral;
            return TeamRules.CanDamage(myTeam, otherTeam);
        }

        private static Key GetFallbackKey(int slot)
        {
            return slot switch { 0 => Key.Q, 1 => Key.E, 2 => Key.R, _ => Key.None };
        }

        private static KeyControl GetKeyControl(Keyboard kb, Key key)
        {
            try { return kb[key]; }
            catch { return null; }
        }

        private void ExecuteIfCast(CastAction action, int slot)
        {
            if (action == CastAction.Cast && slot >= 0)
                TryCast(slot);
        }

        // ── ピック反映 API ────────────────────────────────────

        /// <summary>
        /// キャラクターピック時に呼び出し、スキルセットを差し替える。
        /// アーム状態とインジケーターをリセットしてから CD を再構成する。
        /// </summary>
        public void SetSkills(SkillDefinition[] skills)
        {
            // アーム中の状態を先にクリアしてインジケーターを隠す
            _castLogic.HandleCancel();
            SetIndicatorActive(null);

            for (int i = 0; i < 4; i++)
            {
                _skills[i] = (skills != null && i < skills.Length) ? skills[i] : null;
                float cd = _skills[i] != null ? _skills[i].CooldownSeconds : 1f;
                _cooldowns[i] = new AttackCooldown(cd);
            }
        }

        // ── HUD 公開 API ──────────────────────────────────────

        /// <summary>スロット番号に対応する SkillDefinition を返す。未設定は null。</summary>
        public SkillDefinition GetSkill(int slot)
        {
            if (slot < 0 || slot >= _skills.Length) return null;
            return _skills[slot];
        }

        /// <summary>残りクールダウン秒（0〜Duration）。スキル未設定のスロットは 0 を返す。</summary>
        public float GetCooldownRemaining(int slot)
        {
            if (slot < 0 || slot >= _cooldowns.Length || _skills[slot] == null) return 0f;
            return _cooldowns[slot].Remaining(Time.time);
        }

        /// <summary>残りクールダウンの割合（0〜1）。スキル未設定のスロットは 0 を返す。</summary>
        public float GetCooldownFraction(int slot)
        {
            if (slot < 0 || slot >= _cooldowns.Length || _skills[slot] == null
                || _cooldowns[slot] == null) return 0f;
            float duration = _cooldowns[slot].Duration;
            if (duration <= 0f) return 0f;
            return Mathf.Clamp01(_cooldowns[slot].Remaining(Time.time) / duration);
        }
    }
}
