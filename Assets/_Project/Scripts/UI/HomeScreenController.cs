using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using Enigma.Character;
using Enigma.Data;

namespace Enigma.UI
{
    public class HomeScreenController : MonoBehaviour
    {
        [SerializeField] UIDocument uiDocument;

        [Header("Profile")]
        [SerializeField] Texture2D playerIcon;

        [Header("Data")]
        [SerializeField] FriendDatabase friendDatabase;
        [SerializeField] CharacterDatabase characterDatabase;

        // ── メインタブ ─────────────────────────────────
        VisualElement pageGame, pageInventory, pageGacha;
        Button tabGame, tabInventory, tabGacha;
        Button btnPlay, btnProfile, btnSettings;

        // ── 所持品サブタブ ─────────────────────────────
        Button itabChars, itabSkins, itabItems;
        ScrollView charGrid;
        VisualElement ipageSkins, ipageItems;

        // ── 設定オーバーレイ ───────────────────────────
        VisualElement settingsOverlay;
        VisualElement spageSound, spageGraphics, spageControls, spageGame;
        Button stabSound, stabGraphics, stabControls, stabGameTab;
        Button btnCloseSettings, btnApplySettings;

        // ── ガチャ ─────────────────────────────────────
        VisualElement gachaResultOverlay;
        ScrollView    gachaResultGrid;
        Button        btnGacha1, btnGacha10, btnCloseGachaResult;
        Label         labelCrystals;

        // ── プロフィールオーバーレイ ───────────────────
        VisualElement profileOverlay;
        VisualElement profileOverlayIcon;
        Button        btnCloseProfile;
        Label         profileCharCount, profileCrystalCount;
        Label         profileOverlayName, profileOverlayLevel;

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

            // ナビバーのプロフィールアイコン画像設定
            if (playerIcon != null)
                root.Q<VisualElement>("profile-icon").style.backgroundImage =
                    Background.FromTexture2D(playerIcon);

            // プロフィールオーバーレイ
            profileOverlay      = root.Q<VisualElement>("profile-overlay");
            profileOverlayIcon  = root.Q<VisualElement>("profile-overlay-icon");
            btnCloseProfile     = root.Q<Button>("btn-close-profile");
            profileCharCount    = root.Q<Label>("profile-char-count");
            profileCrystalCount = root.Q<Label>("profile-crystal-count");
            profileOverlayName  = root.Q<Label>("profile-overlay-name");
            profileOverlayLevel = root.Q<Label>("profile-overlay-level");

            btnCloseProfile.clicked += CloseProfile;

            // オーバーレイアイコンにも同じ画像を設定（ナビアイコンと共通）
            if (playerIcon != null)
                profileOverlayIcon.style.backgroundImage =
                    Background.FromTexture2D(playerIcon);

            // 所持品サブタブ
            itabChars  = root.Q<Button>("itab-chars");
            itabSkins  = root.Q<Button>("itab-skins");
            itabItems  = root.Q<Button>("itab-items");
            charGrid   = root.Q<ScrollView>("char-grid");
            ipageSkins = root.Q<VisualElement>("ipage-skins");
            ipageItems = root.Q<VisualElement>("ipage-items");

            itabChars.clicked += () => SwitchInventoryTab(0);
            itabSkins.clicked += () => SwitchInventoryTab(1);
            itabItems.clicked += () => SwitchInventoryTab(2);

            BuildFriendList(root.Q<ScrollView>("friend-list"));
            BuildCharacterGrid();

            // ガチャ
            labelCrystals      = root.Q<Label>("crystal-count");
            btnGacha1          = root.Q<Button>("btn-gacha-1");
            btnGacha10         = root.Q<Button>("btn-gacha-10");
            gachaResultOverlay = root.Q<VisualElement>("gacha-result-overlay");
            gachaResultGrid    = root.Q<ScrollView>("gacha-result-grid");
            btnCloseGachaResult = root.Q<Button>("btn-close-gacha-result");

            btnGacha1.clicked           += () => OnGachaPull(1);
            btnGacha10.clicked          += () => OnGachaPull(10);
            btnCloseGachaResult.clicked += () => gachaResultOverlay.style.display = DisplayStyle.None;

            RefreshGachaUI();
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

        // ── 所持品サブタブ切り替え ─────────────────────
        void SwitchInventoryTab(int index)
        {
            Button[]        tabs  = { itabChars, itabSkins, itabItems };
            // ipage-chars は ScrollView (charGrid) なので VisualElement[] に合わせて扱う
            VisualElement[] pages = { charGrid, ipageSkins, ipageItems };
            for (int i = 0; i < pages.Length; i++)
            {
                bool active = i == index;
                pages[i].style.display = active ? DisplayStyle.Flex : DisplayStyle.None;
                SetClass(tabs[i], "inventory-tab--active", active);
            }
        }

        // ── キャラクターグリッド構築 ───────────────────
        void BuildCharacterGrid()
        {
            if (charGrid == null) return;
            charGrid.Clear();

            if (characterDatabase == null)
            {
                Debug.LogWarning("[HomeScreen] CharacterDatabase が設定されていません");
                return;
            }

            var chars = characterDatabase.GetSorted();
            foreach (var chara in chars)
            {
                var card = new VisualElement();
                card.AddToClassList("char-card");
                if (!CharacterOwnership.IsOwned(chara))
                    card.AddToClassList("char-card--locked");

                // アイコン領域
                var iconEl = new VisualElement();
                iconEl.AddToClassList("char-card__icon");

                if (chara.icon != null)
                {
                    iconEl.style.backgroundImage = Background.FromTexture2D(chara.icon);
                }
                else
                {
                    // icon が null → themeColor 背景 + 頭文字
                    iconEl.style.backgroundColor = new StyleColor(chara.themeColor);
                    var initial = new Label(chara.displayName.Length > 0
                        ? chara.displayName[..1]
                        : "?");
                    initial.AddToClassList("char-card__initial");
                    iconEl.Add(initial);
                }

                // 未所持ラベル（カード右上）
                if (!CharacterOwnership.IsOwned(chara))
                {
                    var lockedLabel = new Label("未所持");
                    lockedLabel.AddToClassList("char-card__locked-label");
                    iconEl.Add(lockedLabel);
                }

                // 名前
                var nameLabel = new Label(chara.displayName);
                nameLabel.AddToClassList("char-card__name");

                // ロール
                var roleLabel = new Label(chara.RoleLabel);
                roleLabel.AddToClassList("char-card__role");

                card.Add(iconEl);
                card.Add(nameLabel);
                card.Add(roleLabel);
                charGrid.Add(card);
            }
        }

        // ── ガチャ UI ──────────────────────────────────
        void RefreshGachaUI()
        {
            if (labelCrystals != null)
                labelCrystals.text = GachaService.Crystals.ToString("N0");

            if (btnGacha1  != null) btnGacha1.SetEnabled(GachaService.Crystals  >= GachaService.SinglePullCost);
            if (btnGacha10 != null) btnGacha10.SetEnabled(GachaService.Crystals >= GachaService.TenPullCost);
        }

        void OnGachaPull(int count)
        {
            var results = new List<GachaService.PullResult>();

            if (!GachaService.TryPull(characterDatabase, count, results))
            {
                Debug.LogWarning("[HomeScreen] ガチャ失敗: 残高不足またはキャラクター未登録");
                return;
            }

            // 結果オーバーレイを表示
            gachaResultOverlay.style.display = DisplayStyle.Flex;
            gachaResultGrid.Clear();

            foreach (var result in results)
            {
                var chara = result.character;

                var card = new VisualElement();
                card.AddToClassList("char-card");

                // アイコン領域
                var iconEl = new VisualElement();
                iconEl.AddToClassList("char-card__icon");

                if (chara.icon != null)
                {
                    iconEl.style.backgroundImage = Background.FromTexture2D(chara.icon);
                }
                else
                {
                    iconEl.style.backgroundColor = new StyleColor(chara.themeColor);
                    var initial = new Label(chara.displayName.Length > 0 ? chara.displayName[..1] : "?");
                    initial.AddToClassList("char-card__initial");
                    iconEl.Add(initial);
                }

                // NEW / 重複バッジ（アイコン左上に絶対配置）
                var badge = new Label(result.isNew ? "NEW!" : "重複");
                badge.AddToClassList(result.isNew ? "gacha-badge--new" : "gacha-badge--dupe");
                iconEl.Add(badge);

                var nameLabel = new Label(chara.displayName);
                nameLabel.AddToClassList("char-card__name");

                var roleLabel = new Label(chara.RoleLabel);
                roleLabel.AddToClassList("char-card__role");

                card.Add(iconEl);
                card.Add(nameLabel);
                card.Add(roleLabel);
                gachaResultGrid.Add(card);
            }

            // 所持状態が変わったのでグリッドを更新
            RefreshGachaUI();
            BuildCharacterGrid();
        }

        // ── プロフィールオーバーレイ開閉 ───────────────
        void OpenProfile()
        {
            // 開くたびに最新値に更新（ガチャ後に古い値が残らないようにするため）
            if (characterDatabase != null)
            {
                var all   = characterDatabase.GetSorted();
                int owned = 0;
                foreach (var c in all)
                    if (CharacterOwnership.IsOwned(c)) owned++;
                profileCharCount.text = $"{owned} / {all.Count}";
            }
            else
            {
                profileCharCount.text = "-";
            }

            profileCrystalCount.text = GachaService.Crystals.ToString("N0");

            profileOverlay.style.display = DisplayStyle.Flex;
        }

        void CloseProfile() =>
            profileOverlay.style.display = DisplayStyle.None;

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

        // ── ESC キーでオーバーレイを閉じる ────────────
        void Update()
        {
            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            if (keyboard == null) return;

            if (!keyboard.escapeKey.wasPressedThisFrame) return;

            // 優先順: ガチャ結果 > プロフィール > 設定（最前面の1つだけ閉じる）
            if (gachaResultOverlay != null &&
                gachaResultOverlay.style.display == DisplayStyle.Flex)
            {
                gachaResultOverlay.style.display = DisplayStyle.None;
                return;
            }

            if (profileOverlay != null &&
                profileOverlay.style.display == DisplayStyle.Flex)
            {
                CloseProfile();
                return;
            }

            if (settingsOverlay.style.display == DisplayStyle.Flex)
            {
                CloseSettings();
            }
        }

        // ── その他ボタン ───────────────────────────────
        void OnPlayClicked()    => Debug.Log("[HomeScreen] プレイ開始");
        void OnProfileClicked() => OpenProfile();

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
