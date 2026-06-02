using UnityEngine;
using UnityEngine.UIElements;

namespace Enigma.UI
{
    public class HomeScreenController : MonoBehaviour
    {
        [SerializeField] UIDocument uiDocument;

        [Header("Profile")]
        [SerializeField] Texture2D playerIcon;

        // ── メインタブ ─────────────────────────────────
        VisualElement pageGame, pageInventory, pageGacha;
        Button tabGame, tabInventory, tabGacha;
        Button btnPlay, btnProfile, btnSettings;

        // ── 設定オーバーレイ ───────────────────────────
        VisualElement settingsOverlay;
        VisualElement spageSound, spageGraphics, spageControls, spageGame;
        Button stabSound, stabGraphics, stabControls, stabGameTab;
        Button btnCloseSettings, btnApplySettings;

        Slider sliderBgm, sliderSe, sliderVoice;
        Label  labelBgmVal, labelSeVal, labelVoiceVal;
        DropdownField dropdownQuality, dropdownWindow;

        // ── フレンドデータ（仮） ───────────────────────
        static readonly (string name, bool online)[] FriendData =
        {
            ("山田", true), ("鈴木", false), ("田中", true),
        };

        void OnEnable()
        {
            SettingsManager.Load();
            var root = uiDocument.rootVisualElement;

            // メインタブ
            pageGame      = root.Q<VisualElement>("page-game");
            pageInventory = root.Q<VisualElement>("page-inventory");
            pageGacha     = root.Q<VisualElement>("page-gacha");
            tabGame       = root.Q<Button>("tab-game");
            tabInventory  = root.Q<Button>("tab-inventory");
            tabGacha      = root.Q<Button>("tab-gacha");
            btnPlay       = root.Q<Button>("btn-play");
            btnProfile    = root.Q<Button>("btn-profile");
            btnSettings   = root.Q<Button>("btn-settings");

            tabGame.clicked      += () => SwitchTab(0);
            tabInventory.clicked += () => SwitchTab(1);
            tabGacha.clicked     += () => SwitchTab(2);
            btnPlay.clicked      += OnPlayClicked;
            btnProfile.clicked   += OnProfileClicked;
            btnSettings.clicked  += OpenSettings;

            // 設定オーバーレイ
            settingsOverlay  = root.Q<VisualElement>("settings-overlay");
            spageSound       = root.Q<VisualElement>("spage-sound");
            spageGraphics    = root.Q<VisualElement>("spage-graphics");
            spageControls    = root.Q<VisualElement>("spage-controls");
            spageGame        = root.Q<VisualElement>("spage-game");
            stabSound        = root.Q<Button>("stab-sound");
            stabGraphics     = root.Q<Button>("stab-graphics");
            stabControls     = root.Q<Button>("stab-controls");
            stabGameTab      = root.Q<Button>("stab-game");
            btnCloseSettings = root.Q<Button>("btn-close-settings");
            btnApplySettings = root.Q<Button>("btn-settings-apply");

            stabSound.clicked    += () => SwitchSettingsTab(0);
            stabGraphics.clicked += () => SwitchSettingsTab(1);
            stabControls.clicked += () => SwitchSettingsTab(2);
            stabGameTab.clicked  += () => SwitchSettingsTab(3);
            btnCloseSettings.clicked += CloseSettings;
            btnApplySettings.clicked += ApplySettings;

            // スライダー・ドロップダウン
            sliderBgm   = root.Q<Slider>("slider-bgm");
            sliderSe    = root.Q<Slider>("slider-se");
            sliderVoice = root.Q<Slider>("slider-voice");
            labelBgmVal   = root.Q<Label>("label-bgm-val");
            labelSeVal    = root.Q<Label>("label-se-val");
            labelVoiceVal = root.Q<Label>("label-voice-val");
            dropdownQuality = root.Q<DropdownField>("dropdown-quality");
            dropdownWindow  = root.Q<DropdownField>("dropdown-window");

            sliderBgm.RegisterValueChangedCallback(e =>
                labelBgmVal.text = $"{Mathf.RoundToInt(e.newValue * 100)}%");
            sliderSe.RegisterValueChangedCallback(e =>
                labelSeVal.text = $"{Mathf.RoundToInt(e.newValue * 100)}%");
            sliderVoice.RegisterValueChangedCallback(e =>
                labelVoiceVal.text = $"{Mathf.RoundToInt(e.newValue * 100)}%");

            // 保存済み設定を反映
            sliderBgm.value   = SettingsManager.BgmVolume;
            sliderSe.value    = SettingsManager.SeVolume;
            sliderVoice.value = SettingsManager.VoiceVolume;
            dropdownQuality.index = SettingsManager.QualityLevel;
            dropdownWindow.index  = SettingsManager.WindowMode;

            // プロフィールアイコン
            if (playerIcon != null)
                root.Q<VisualElement>("profile-icon").style.backgroundImage =
                    Background.FromTexture2D(playerIcon);

            BuildFriendList(root.Q<ScrollView>("friend-list"));
            SwitchTab(0);
        }

        // ── メインタブ切り替え ─────────────────────────
        void SwitchTab(int index)
        {
            VisualElement[] pages = { pageGame, pageInventory, pageGacha };
            Button[]        tabs  = { tabGame, tabInventory, tabGacha };
            for (int i = 0; i < pages.Length; i++)
            {
                bool active = i == index;
                SetClass(pages[i], "page--active", active);
                SetClass(tabs[i],  "nav-tab--active", active);
            }
        }

        // ── 設定オーバーレイ開閉 ───────────────────────
        void OpenSettings()
        {
            settingsOverlay.style.display = DisplayStyle.Flex;
            SwitchSettingsTab(0);
        }

        void CloseSettings() =>
            settingsOverlay.style.display = DisplayStyle.None;

        void ApplySettings()
        {
            SettingsManager.Apply(
                sliderBgm.value,
                sliderSe.value,
                sliderVoice.value,
                dropdownQuality.index,
                dropdownWindow.index
            );
            CloseSettings();
        }

        // ── 設定タブ切り替え ───────────────────────────
        void SwitchSettingsTab(int index)
        {
            VisualElement[] pages = { spageSound, spageGraphics, spageControls, spageGame };
            Button[]        tabs  = { stabSound, stabGraphics, stabControls, stabGameTab };
            for (int i = 0; i < pages.Length; i++)
            {
                bool active = i == index;
                SetClass(pages[i], "settings-page--active", active);
                SetClass(tabs[i],  "settings-tab--active",  active);
            }
        }

        // ── その他ボタン ───────────────────────────────
        void OnPlayClicked()    => Debug.Log("[HomeScreen] プレイ開始");
        void OnProfileClicked() => Debug.Log("[HomeScreen] プロフィール");

        // ── フレンドリスト構築 ─────────────────────────
        void BuildFriendList(ScrollView list)
        {
            list.Clear();
            foreach (var (name, online) in FriendData)
            {
                var row = new VisualElement();
                row.AddToClassList("friend-row");
                var dot = new VisualElement();
                dot.AddToClassList("friend-dot");
                dot.AddToClassList(online ? "friend-dot--online" : "friend-dot--offline");
                var label = new Label(name);
                label.AddToClassList("friend-name");
                if (!online) label.AddToClassList("friend-name--offline");
                row.Add(dot);
                row.Add(label);
                list.Add(row);
            }
        }

        static void SetClass(VisualElement el, string cls, bool active)
        {
            if (active) el.AddToClassList(cls);
            else        el.RemoveFromClassList(cls);
        }
    }
}
