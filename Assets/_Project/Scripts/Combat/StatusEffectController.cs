using UnityEngine;

namespace Enigma.Combat
{
    public sealed class StatusEffectController : MonoBehaviour
    {
        private StatusEffectModel _model;
        public StatusEffectModel Model => _model ??= new StatusEffectModel();

        // リスポーン時に CC を自動クリアするため、同一 GO の HealthComponent.Model.Revived を購読する
        private HealthComponent _health;

        public bool CanMove => Model.CanMove;
        public bool CanAct => Model.CanAct;
        public float MoveSpeedMultiplier => Model.MoveSpeedMultiplier;
        public bool IsStunned => Model.IsStunned;
        public bool IsRooted => Model.IsRooted;
        public bool IsSlowed => Model.IsSlowed;
        public bool IsHasted => Model.IsHasted;

        public void ApplyStun(float d) => Model.ApplyStun(d);
        public void ApplyRoot(float d) => Model.ApplyRoot(d);
        public void ApplySlow(float s, float d) => Model.ApplySlow(s, d);
        public void ApplyHaste(float s, float d) => Model.ApplyHaste(s, d);
        public void Clear() => Model.Clear();

        private void Awake()
        {
            _health = GetComponent<HealthComponent>();
        }

        private void OnEnable()
        {
            // GetOrAdd が HealthComponent 追加前に呼ばれた場合に備え、ここで再取得する
            if (_health == null) _health = GetComponent<HealthComponent>();
            if (_health != null)
                _health.Model.Revived += Clear;
        }

        private void OnDisable()
        {
            if (_health != null)
                _health.Model.Revived -= Clear;
        }

        private void Update()
        {
            Model.Tick(Time.deltaTime);
        }

        /// <summary>
        /// go に既存の StatusEffectController があれば返し、なければ AddComponent して返す。
        /// </summary>
        public static StatusEffectController GetOrAdd(GameObject go)
        {
            if (go == null) return null;
            var c = go.GetComponent<StatusEffectController>();
            return c != null ? c : go.AddComponent<StatusEffectController>();
        }
    }
}
