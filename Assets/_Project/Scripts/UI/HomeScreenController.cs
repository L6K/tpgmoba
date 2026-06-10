using UnityEngine;
using UnityEngine.UIElements;

namespace Enigma.UI
{
    public class HomeScreenController : MonoBehaviour
    {
        [SerializeField] UIDocument uiDocument;

        [Header("Profile")]
        [SerializeField] Texture2D playerIcon;

        [Header("Data")]
        [SerializeField] FriendDatabase friendDatabase;

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
            // 適用後も設定画面は閉じない。閉じるのは ✕ ボタン / ESC のみ
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

        // ── ESC キーで設定を閉じる ─────────────────────
        void Update()
        {
            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            if (keyboard == null) return;

            if (keyboard.escapeKey.wasPressedThisFrame &&
                settingsOverlay.style.display == DisplayStyle.Flex)
            {
                CloseSettings();
            }
        }

        // ── その他ボタン ───────────────────────────────
        void OnPlayClicked()    => Debug.Log("[HomeScreen] プレイ開始");
        void OnProfileClicked() => Debug.Log("[HomeScreen] プロフィール");

        // ── フレンドリスト構築 ─────────────────────────
        void BuildFriendList(ScrollView list)
        {
            list.Clear();

            if (friendDatabase == null)
            {
                Debug.LogWarning("[HomeScreen] FriendDatabase が設定されていません");
                return;
            }

            var friends = friendDatabase.GetSorted();

            // ヘッダーのオンライン人数を更新
            var countLabel = uiDocument.rootVisualElement.Q<Label>("friend-count");
            if (countLabel != null)
                countLabel.text = $"{friendDatabase.OnlineCount}/{friendDatabase.TotalCount}";

            foreach (var friend in friends)
            {
                var row = new VisualElement();
                row.AddToClassList("friend-row");

                // オンラインドット
                var dot = new VisualElement();
                dot.AddToClassList("friend-dot");
                dot.AddToClassList(friend.IsOnline ? "friend-dot--online" : "friend-dot--offline");

                // 名前＋ステータスの縦並び
                var info = new VisualElement();
                info.AddToClassList("friend-info");

                var nameLabel = new Label(friend.displayName);
                nameLabel.AddToClassList("friend-name");
                if (!friend.IsOnline) nameLabel.AddToClassList("friend-name--offline");

                var statusLabel = new Label(friend.StatusLabel);
                statusLabel.AddToClassList("friend-status");
                statusLabel.AddToClassList(friend.status switch
                {
                    FriendStatus.InGame  => "friend-status--ingame",
                    FriendStatus.InQueue => "friend-status--inqueue",
                    FriendStatus.Online  => "friend-status--online",
                    _                    => "",
                });

                info.Add(nameLabel);
                info.Add(statusLabel);

                // レベル表示
                var levelLabel = new Label($"Lv.{friend.level}");
                levelLabel.AddToClassList("friend-level");

                // 招待ボタン（オンラインのみ）
                var inviteBtn = new Button(() => OnInviteFriend(friend));
                inviteBtn.text = "招待";
                inviteBtn.AddToClassList("friend-invite-btn");
                if (!friend.IsOnline) inviteBtn.style.display = DisplayStyle.None;

                row.Add(dot);
                row.Add(info);
                row.Add(levelLabel);
                row.Add(inviteBtn);
                list.Add(row);
            }
        }

        void OnInviteFriend(FriendData friend)
        {
            Debug.Log($"[HomeScreen] {friend.displayName} を招待しました");
            // TODO: ネットワーク経由でパーティ招待を送信
        }

        static void SetClass(VisualElement el, string cls, bool active)
        {
            if (active) el.AddToClassList(cls);
            else        el.RemoveFromClassList(cls);
        }
    }
}
