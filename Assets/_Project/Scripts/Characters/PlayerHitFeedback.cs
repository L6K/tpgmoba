using UnityEngine;
using Enigma.Combat;
using Enigma.UI;
using Enigma.Vfx;

namespace Enigma.Character
{
    // プレイヤー被弾時の画面フィードバックを束ねる Humble Object。
    // HealthComponent.Damaged を購読し、カメラシェイクと HUD の赤ビネットフラッシュを発火する。
    [RequireComponent(typeof(HealthComponent))]
    public sealed class PlayerHitFeedback : MonoBehaviour
    {
        private const float ShakeAmplitude = 0.15f;

        [SerializeField] private OrbitCamera _camera;
        [SerializeField] private GameHudController _hud;

        private HealthComponent _health;

        private void Awake()
        {
            _health = GetComponent<HealthComponent>();

            // ビルダー結線が無くても動くよう、未設定なら自動解決する
            if (_camera == null)
                _camera = Object.FindFirstObjectByType<OrbitCamera>();
            if (_hud == null)
                _hud = Object.FindFirstObjectByType<GameHudController>();
        }

        private void OnEnable()
        {
            _health.Damaged += OnDamaged;
        }

        private void OnDisable()
        {
            _health.Damaged -= OnDamaged;
        }

        private void OnDamaged(float amount)
        {
            if (_camera != null) _camera.AddShake(ShakeAmplitude);
            if (_hud == null) return;

            // 被ダメ割合に応じてフラッシュ強度/保持時間を、残HPに応じて低HPビネットを駆動する。
            // Damaged は amount のみ通知（攻撃者方向/クリット情報なし）なので direction=0/crit=false。
            var fb = PlayerHitFeedbackModel.Evaluate(
                amount, _health.Model.MaxHp, _health.Model.CurrentHp, false, 0f);
            _hud.FlashDamageVignette(fb.FlashAlpha, fb.FlashSeconds);
            _hud.SetLowHpVignette(fb.VignetteStrength);
        }
    }
}
