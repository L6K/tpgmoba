using UnityEngine;
using UnityEngine.UIElements;
using Enigma.Combat;
using Enigma.Ability;
using Enigma.Core;
using Enigma.Data;

namespace Enigma.UI
{
    /// <summary>
    /// ゲーム内 HUD の毎フレーム更新を担うハンブルオブジェクト。
    /// 要素の取得・バインドは OnEnable で行い、Update は値の書き込みのみ。
    /// </summary>
    public sealed class GameHudController : MonoBehaviour
    {
        [SerializeField] private UIDocument      _uiDocument;
        [SerializeField] private HealthComponent _playerHealth;
        [SerializeField] private SkillCaster     _skillCaster;

        // タイマー
        private Label _timerLabel;

        // HP バー
        private VisualElement _hpFill;
        private Label         _hpText;

        // チームバフ残り時間ラベル
        private Label _buffLabel;

        // スキルスロット（4スロット分）
        private readonly VisualElement[] _skillSlots    = new VisualElement[4];
        private readonly Label[]         _skillNames    = new Label[4];
        private readonly Label[]         _skillKeys     = new Label[4];
        private readonly VisualElement[] _skillCdOverlay = new VisualElement[4];
        private readonly Label[]         _skillCdText   = new Label[4];

        private void OnEnable()
        {
            // GameServices が未初期化の場合の保険（HomeScreenController と同様）
            if (!GameServices.IsInitialized) GameServices.Initialize();

            if (_uiDocument == null) return;
            var root = _uiDocument.rootVisualElement;

            _timerLabel = root.Q<Label>("hud-timer");
            _hpFill     = root.Q<VisualElement>("hud-hp-fill");
            _hpText     = root.Q<Label>("hud-hp-text");
            _buffLabel  = root.Q<Label>("hud-buff");

            for (int i = 0; i < 4; i++)
            {
                _skillSlots[i]     = root.Q<VisualElement>($"hud-skill-{i}");
                _skillNames[i]     = root.Q<Label>($"hud-skill-name-{i}");
                _skillKeys[i]      = root.Q<Label>($"hud-skill-key-{i}");
                _skillCdOverlay[i] = root.Q<VisualElement>($"hud-skill-cd-{i}");
                _skillCdText[i]    = root.Q<Label>($"hud-skill-cdtext-{i}");
            }
        }

        private void Update()
        {
            UpdateTimer();
            UpdateHp();
            UpdateSkills();
            UpdateBuff();
        }

        private void UpdateTimer()
        {
            if (_timerLabel == null) return;
            float elapsed = Time.timeSinceLevelLoad;
            int   minutes = (int)(elapsed / 60f);
            int   seconds = (int)(elapsed % 60f);
            _timerLabel.text = $"{minutes:D2}:{seconds:D2}";
        }

        private void UpdateHp()
        {
            if (_playerHealth == null || _playerHealth.Model == null) return;
            var model    = _playerHealth.Model;
            float ratio  = model.MaxHp > 0f ? Mathf.Clamp01(model.CurrentHp / model.MaxHp) : 0f;

            if (_hpFill != null)
                _hpFill.style.width = Length.Percent(ratio * 100f);

            if (_hpText != null)
                _hpText.text = $"{Mathf.CeilToInt(model.CurrentHp)} / {Mathf.CeilToInt(model.MaxHp)}";
        }

        private void UpdateSkills()
        {
            if (_skillCaster == null) return;

            for (int i = 0; i < 4; i++)
            {
                var def = _skillCaster.GetSkill(i);

                // 未設定スロットは暗く表示して残処理をスキップ
                _skillSlots[i]?.EnableInClassList("hud-skill-slot--empty", def == null);

                if (_skillNames[i] != null)
                    _skillNames[i].text = def != null ? def.SkillName : "";

                // キーバインド表示
                if (_skillKeys[i] != null)
                {
                    var key = GameServices.ControlSettings?.GetSkillKey(i)
                              ?? GetFallbackKey(i);
                    _skillKeys[i].text = key.ToString();
                }

                // クールダウンオーバーレイ（上から fraction % 分を覆う）
                float fraction = _skillCaster.GetCooldownFraction(i);
                if (_skillCdOverlay[i] != null)
                    _skillCdOverlay[i].style.height = Length.Percent(fraction * 100f);

                // 残秒テキスト（0.1 秒単位。CD 中のみ表示）
                if (_skillCdText[i] != null)
                {
                    float remaining = _skillCaster.GetCooldownRemaining(i);
                    bool  active    = remaining > 0.05f;
                    _skillCdText[i].style.display = active ? DisplayStyle.Flex : DisplayStyle.None;
                    if (active)
                        _skillCdText[i].text = remaining.ToString("F1");
                }
            }
        }

        private void UpdateBuff()
        {
            if (_buffLabel == null) return;

            var buffs = GameServices.TeamBuffs;
            float remaining = buffs?.GetRemainingSeconds(Enigma.Combat.TeamId.Blue, Time.time) ?? 0f;

            if (remaining > 0f)
            {
                int min = (int)(remaining / 60f);
                int sec = (int)(remaining % 60f);
                _buffLabel.text = $"エニグマバフ {min}:{sec:D2}";
                _buffLabel.style.display = DisplayStyle.Flex;
            }
            else
            {
                _buffLabel.style.display = DisplayStyle.None;
            }
        }

        private static UnityEngine.InputSystem.Key GetFallbackKey(int slot) =>
            slot switch { 0 => UnityEngine.InputSystem.Key.Q,
                          1 => UnityEngine.InputSystem.Key.W,
                          2 => UnityEngine.InputSystem.Key.E,
                          3 => UnityEngine.InputSystem.Key.R,
                          _ => UnityEngine.InputSystem.Key.None };
    }
}
