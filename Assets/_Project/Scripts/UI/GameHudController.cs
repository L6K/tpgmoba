using UnityEngine;
using UnityEngine.UIElements;
using Enigma.Combat;
using Enigma.Ability;
using Enigma.Core;
using Enigma.Data;
using Enigma.Character;

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

        // レベル・XP 表示
        private Label         _levelLabel;
        private VisualElement _xpFill;
        private PlayerProgression _playerProgression;

        // スキルスロット（4スロット分）
        private readonly VisualElement[] _skillSlots    = new VisualElement[4];
        private readonly Label[]         _skillNames    = new Label[4];
        private readonly Label[]         _skillKeys     = new Label[4];
        private readonly VisualElement[] _skillCdOverlay = new VisualElement[4];
        private readonly Label[]         _skillCdText   = new Label[4];

        // 所持金ラベル
        private Label _goldLabel;

        // アイテムスロット（6枠）
        private readonly VisualElement[] _itemSlots    = new VisualElement[6];
        private readonly Label[]         _itemInitials = new Label[6];

        // Wallet / Items は _playerHealth と同一 GO から取得
        private PlayerWallet _playerWallet;
        private PlayerItems  _playerItems;

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
            _levelLabel = root.Q<Label>("hud-level");
            _xpFill     = root.Q<VisualElement>("hud-xp-fill");
            _goldLabel  = root.Q<Label>("hud-gold");

            for (int i = 0; i < 6; i++)
            {
                _itemSlots[i]    = root.Q<VisualElement>($"hud-item-slot-{i}");
                _itemInitials[i] = root.Q<Label>($"hud-item-initial-{i}");
            }

            // SerializedObject を増やさずに playerHealth と同一 GO から取得（null 安全）
            if (_playerHealth != null)
            {
                _playerProgression = _playerHealth.GetComponent<PlayerProgression>();
                _playerWallet      = _playerHealth.GetComponent<PlayerWallet>();
                _playerItems       = _playerHealth.GetComponent<PlayerItems>();
            }

            // スロット 0..2（Q/E/R）のみ。slot3 は HUD に存在しない
            for (int i = 0; i < 3; i++)
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
            UpdateLevelXp();
            UpdateGoldAndItems();
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

            // スロット 0..2（Q/E/R）のみ更新。slot3 は HUD に存在しない
            for (int i = 0; i < 3; i++)
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

        private void UpdateLevelXp()
        {
            if (_playerProgression == null) return;
            var exp = _playerProgression.Experience;

            if (_levelLabel != null)
                _levelLabel.text = $"Lv.{exp.Level}";

            if (_xpFill != null)
            {
                // 最大レベル時は100%固定
                float ratio = exp.Level >= ExperienceModel.MaxLevel
                    ? 1f
                    : (exp.XpToNext > 0f ? Mathf.Clamp01(exp.CurrentXp / exp.XpToNext) : 0f);
                _xpFill.style.width = Length.Percent(ratio * 100f);
            }
        }

        // 所持金ラベルとアイテム6枠を更新する
        private void UpdateGoldAndItems()
        {
            if (_goldLabel != null && _playerWallet != null)
                _goldLabel.text = $"{_playerWallet.Wallet.Gold} G";

            if (_playerItems == null) return;
            var items = _playerItems.Inventory.Items;

            for (int i = 0; i < 6; i++)
            {
                if (i < items.Count)
                {
                    var item = items[i];

                    // スロット背景色をアイテムのテーマカラーに設定
                    if (_itemSlots[i] != null)
                        _itemSlots[i].style.backgroundColor = new StyleColor(item.ThemeColor);

                    // 頭文字1文字を表示
                    if (_itemInitials[i] != null)
                        _itemInitials[i].text = item.ItemName.Length > 0 ? item.ItemName[..1] : "?";
                }
                else
                {
                    // 空枠: 背景色をデフォルトに戻して文字をクリア
                    if (_itemSlots[i] != null)
                        _itemSlots[i].style.backgroundColor = new StyleColor(new Color(10f / 255f, 12f / 255f, 22f / 255f, 0.60f));

                    if (_itemInitials[i] != null)
                        _itemInitials[i].text = "";
                }
            }
        }

        private static UnityEngine.InputSystem.Key GetFallbackKey(int slot) =>
            slot switch { 0 => UnityEngine.InputSystem.Key.Q,
                          1 => UnityEngine.InputSystem.Key.E,
                          2 => UnityEngine.InputSystem.Key.R,
                          _ => UnityEngine.InputSystem.Key.None };
    }
}
