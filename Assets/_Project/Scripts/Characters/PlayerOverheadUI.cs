using UnityEngine;
using Enigma.Combat;

namespace Enigma.Character
{
    // プレイヤー頭上 HP バー & レベル表示を管理する。
    // FillWrapper.localScale.x = ratio (0〜1) で左詰め HP バーを表現する（CreateWorldHealthBar と同方式）。
    public sealed class PlayerOverheadUI : MonoBehaviour
    {
        private const float LerpSpeed = 8f;

        [SerializeField] private Transform _barFill;
        [SerializeField] private HealthComponent _healthComponent;
        [SerializeField] private PlayerProgression _progression;

        private TextMesh _levelText;
        private float    _targetRatio;
        private float    _currentRatio;

        private void Start()
        {
            // LevelText は HealthBar GO の子として配置されているためここで取得する
            var levelTextGo = transform.Find("HealthBar/LevelText");
            if (levelTextGo != null)
                _levelText = levelTextGo.GetComponent<TextMesh>();

            // 購読
            if (_healthComponent != null)
                _healthComponent.Model.Changed += OnHealthChanged;

            if (_progression != null)
                _progression.Experience.LevelChanged += OnLevelChanged;

            // 初期反映（スムーズ補間なしで即時セット）
            RefreshBar();
            _currentRatio = _targetRatio;
            ApplyBarScale(_currentRatio);

            RefreshLevel();
        }

        private void Update()
        {
            if (_barFill == null) return;

            // 指数補間でスムーズ減少。Mathf.MoveTowards より自然なイーズアウト。
            _currentRatio = Mathf.Lerp(_currentRatio, _targetRatio, 1f - Mathf.Exp(-LerpSpeed * Time.deltaTime));
            ApplyBarScale(_currentRatio);
        }

        private void OnDestroy()
        {
            if (_healthComponent != null)
                _healthComponent.Model.Changed -= OnHealthChanged;

            if (_progression != null)
                _progression.Experience.LevelChanged -= OnLevelChanged;
        }

        // Changed は (currentHp, maxHp) の2引数シグネチャ
        private void OnHealthChanged(float _cur, float _max) => RefreshBar();
        private void OnLevelChanged(int _) => RefreshLevel();

        private void RefreshBar()
        {
            if (_barFill == null || _healthComponent == null) return;
            var m = _healthComponent.Model;
            _targetRatio = m.MaxHp > 0f ? m.CurrentHp / m.MaxHp : 0f;
        }

        private void ApplyBarScale(float ratio)
        {
            var s = _barFill.localScale;
            _barFill.localScale = new Vector3(ratio, s.y, s.z);
        }

        private void RefreshLevel()
        {
            if (_levelText == null || _progression == null) return;
            _levelText.text = _progression.Experience.Level.ToString();
        }
    }
}
