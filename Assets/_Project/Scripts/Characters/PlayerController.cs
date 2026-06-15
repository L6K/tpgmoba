using UnityEngine;
using UnityEngine.InputSystem;
using Enigma.Combat;
using Enigma.Core;
using Enigma.GameModes;

namespace Enigma.Character
{
    // 暫定実装: Input System の直接ポーリング（InputActions アセット未使用）
    [RequireComponent(typeof(CharacterController))]
    public sealed class PlayerController : MonoBehaviour
    {
        [SerializeField] private Transform         _cameraTransform;
        [SerializeField] private float             _moveSpeed = 6f;
        [SerializeField] private float             _turnSpeedDegrees = 720f;
        [SerializeField] private Animator          _animator; // 見た目モデルのアニメーター（任意）
        [SerializeField] private PlayerAttackMotor _motor;

        private static readonly int SpeedParam = Animator.StringToHash("Speed");

        private const float Gravity = -20f;
        private const float JumpSpeed = 8f;
        // ジャンプのスクワッシュ&ストレッチ演出(全モデル共通・クリップ不要)
        private const float JumpAnimDuration = 0.7f;
        private float     _jumpAnimTime;
        private Transform _jumpModel;
        private Vector3   _jumpModelBaseScale = Vector3.one;

        private CharacterController _cc;
        private float _verticalVelocity;
        private PlayerItems _playerItems;
        private StatusEffectController _statusEffects;
        private TeamTag _teamTag;
        private float _dashTimeRemaining;
        private Vector3 _dashVelocity;

        // characters.json を正とするピック済み移動速度を反映する（composition root から呼ぶ）
        public void SetMoveSpeed(float moveSpeed)
        {
            _moveSpeed = moveSpeed;
        }

        private void Awake()
        {
            _cc           = GetComponent<CharacterController>();
            _playerItems  = GetComponent<PlayerItems>();
            _statusEffects = StatusEffectController.GetOrAdd(gameObject);
            _teamTag      = GetComponentInParent<TeamTag>();
        }

        // dir は水平方向(正規化不要、内部で正規化)。distance(m) を duration 秒で移動するダッシュ。
        public void RequestDash(Vector3 dir, float distance, float duration = 0.15f)
        {
            if (duration <= 0f || distance <= 0f) return;
            if (_statusEffects != null && !_statusEffects.CanMove) return; // ルート/スタン中はダッシュ不可
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f) dir = transform.forward;
            _dashVelocity = dir.normalized * (distance / duration);
            _dashTimeRemaining = duration;
        }

        // ジャンプ演出開始。見た目モデル(Animator のある子)を対象にスクワッシュ&ストレッチする。
        private void StartJumpMotion()
        {
            var model = _animator != null ? _animator.transform : null;
            if (model == null)
            {
                var a = GetComponentInChildren<Animator>();
                if (a != null) model = a.transform;
            }
            if (model == null) return;
            // 演出中でなければ基準スケールを記録(多重ジャンプで基準が崩れないように)
            if (_jumpAnimTime <= 0f || _jumpModel != model)
            {
                _jumpModel = model;
                _jumpModelBaseScale = model.localScale;
            }
            _jumpAnimTime = JumpAnimDuration;
        }

        // 踏み切り潰れ→空中で縦伸び→着地で復元、のスケール演出を進める。
        private void UpdateJumpMotion()
        {
            if (_jumpAnimTime <= 0f || _jumpModel == null) return;
            _jumpAnimTime -= Time.deltaTime;
            float p = 1f - Mathf.Clamp01(_jumpAnimTime / JumpAnimDuration);

            float sy, sxz;
            if (p < 0.18f)            { float k = p / 0.18f;          sy = Mathf.Lerp(1f, 0.82f, k);  sxz = Mathf.Lerp(1f, 1.12f, k); }
            else if (p < 0.75f)       { float k = (p - 0.18f) / 0.57f; sy = Mathf.Lerp(0.82f, 1.12f, Mathf.Sin(k * Mathf.PI * 0.5f)); sxz = Mathf.Lerp(1.12f, 0.94f, k); }
            else                      { float k = (p - 0.75f) / 0.25f; sy = Mathf.Lerp(1.12f, 1f, k);  sxz = Mathf.Lerp(0.94f, 1f, k); }

            _jumpModel.localScale = new Vector3(_jumpModelBaseScale.x * sxz, _jumpModelBaseScale.y * sy, _jumpModelBaseScale.z * sxz);
            if (_jumpAnimTime <= 0f) _jumpModel.localScale = _jumpModelBaseScale; // 復元
        }

        // 中央オブジェクト撃破報酬の MoveSpeed バフ倍率（自チーム）。未生成時は 1。
        private float ObjectiveMoveSpeedMultiplier()
        {
            var buffs = GameServices.ObjectiveBuffs;
            if (buffs == null || _teamTag == null) return 1f;
            return 1f + buffs.GetMagnitude(_teamTag.Team, ObjectiveBuffType.MoveSpeed, Time.time);
        }

        private void Update()
        {
            UpdateJumpMotion(); // ジャンプ演出は毎フレーム駆動(ダッシュ中も継続)

            if (_dashTimeRemaining > 0f)
            {
                _dashTimeRemaining -= Time.deltaTime;
                if (_cc.isGrounded && _verticalVelocity < 0f) _verticalVelocity = -2f;
                _verticalVelocity += Gravity * Time.deltaTime;
                var dstep = _dashVelocity * Time.deltaTime;
                dstep.y = _verticalVelocity * Time.deltaTime;
                _cc.Move(dstep);
                var look = _dashVelocity; look.y = 0f;
                if (look.sqrMagnitude > 0.0001f)
                    transform.rotation = MovementLogic.RotateTowards(transform.rotation, look.normalized, _turnSpeedDegrees * Time.deltaTime);
                return;
            }

            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            // カーソルロック状態に依存しない（FF14準拠操作）
            var input = Vector2.zero;
            if (keyboard.wKey.isPressed) input.y += 1f;
            if (keyboard.sKey.isPressed) input.y -= 1f;
            if (keyboard.aKey.isPressed) input.x -= 1f;
            if (keyboard.dKey.isPressed) input.x += 1f;

            bool hasInput = input != Vector2.zero;

            // Windup 中は移動入力を無視（重力は適用継続）
            bool ccImmobile    = _statusEffects != null && !_statusEffects.CanMove;
            bool movementLocked = (_motor != null && _motor.Motion.MovementLocked) || ccImmobile;

            // 移動入力があり Recovery 中はリカバリをキャンセルしてから移動
            if (hasInput && _motor != null && _motor.Motion.Phase == AttackPhase.Recovery)
            {
                _motor.Motion.CancelRecovery();
            }

            float cameraYaw = _cameraTransform != null ? _cameraTransform.eulerAngles.y : 0f;
            var moveDir = movementLocked
                ? Vector3.zero
                : MovementLogic.CameraRelativeMove(input, cameraYaw);

            // ジャンプ(スペース): 接地中かつ移動可能(スタン/ルート/Windup でない)時のみ
            if (_cc.isGrounded && keyboard.spaceKey.wasPressedThisFrame && !movementLocked)
            {
                _verticalVelocity = JumpSpeed;
                StartJumpMotion();
            }
            else if (_cc.isGrounded && _verticalVelocity < 0f)
            {
                _verticalVelocity = -2f;
            }
            _verticalVelocity += Gravity * Time.deltaTime;

            var motion = moveDir * (_moveSpeed * (_playerItems != null ? _playerItems.MoveSpeedMultiplier : 1f) * (_statusEffects != null ? _statusEffects.MoveSpeedMultiplier : 1f) * ObjectiveMoveSpeedMultiplier() * Time.deltaTime);
            motion.y = _verticalVelocity * Time.deltaTime;
            _cc.Move(motion);

            // 移動中は移動方向へ回頭
            if (moveDir != Vector3.zero)
            {
                transform.rotation = MovementLogic.RotateTowards(
                    transform.rotation, moveDir, _turnSpeedDegrees * Time.deltaTime);
            }

            if (_animator != null)
                _animator.SetFloat(SpeedParam, moveDir == Vector3.zero ? 0f : 1f);
        }
    }
}
