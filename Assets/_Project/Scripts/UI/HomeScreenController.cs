using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.Serialization;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using System;
using Enigma.Character;
using Enigma.Core;
using Enigma.Data;

namespace Enigma.UI
{
    public class HomeScreenController : MonoBehaviour
    {
        [FormerlySerializedAs("uiDocument")]
        [SerializeField] private UIDocument _uiDocument;

        [Header("Profile")]
        [FormerlySerializedAs("playerIcon")]
        [SerializeField] private Texture2D _playerIcon;

        [Header("Data")]
        [FormerlySerializedAs("friendDatabase")]
        [SerializeField] private FriendDatabase _friendDatabase;

        [FormerlySerializedAs("characterDatabase")]
        [SerializeField] private CharacterDatabase _characterDatabase;

        // ── メインタブ ─────────────────────────────────
        private VisualElement _pageGame, _pageInventory, _pageGacha;
        private Button _tabGame, _tabInventory, _tabGacha;
        private Button _btnPlay, _btnProfile, _btnSettings;

        // ── 所持品サブタブ ─────────────────────────────
        private Button _itabChars, _itabSkins, _itabItems;
        private ScrollView _charGrid;
        private VisualElement _ipageSkins, _ipageItems;

        // ── 設定オーバーレイ ───────────────────────────
        private VisualElement _settingsOverlay;
        private VisualElement _spageSound, _spageGraphics, _spageControls, _spageGame;
        private Button _stabSound, _stabGraphics, _stabControls, _stabGameTab;
        private Button _btnCloseSettings, _btnApplySettings;

        // ── ガチャ ─────────────────────────────────────
        private VisualElement _gachaResultOverlay;
        private ScrollView    _gachaResultGrid;
        private Button        _btnGacha1, _btnGacha10, _btnCloseGachaResult;
        private Label         _labelCrystals;

        // ── プロフィールオーバーレイ ───────────────────
        private VisualElement _profileOverlay;
        private VisualElement _profileOverlayIcon;
        private Button        _btnCloseProfile;
        private Label         _profileCharCount, _profileCrystalCount;
        private Label         _profileOverlayName, _profileOverlayLevel;

        private Slider        _sliderBgm, _sliderSe, _sliderVoice;
        private Label         _labelBgmVal, _labelSeVal, _labelVoiceVal;
        private DropdownField _dropdownQuality, _dropdownWindow;

        // ── マッチメイキング ───────────────────────────
        private Label         _matchmakingStatus;

        // ── ゲームタブ ─────────────────────────────────
        private DropdownField _dropdownCastMode;
        private Button[]      _rebindBtns;
        // 現在リバインド待ちのスロット（-1 = 待ち状態なし）
        private int           _rebindingSlot = -1;


        private void OnEnable()
        {
            if (!GameServices.IsInitialized) GameServices.Initialize();

            GameServices.Settings.Load();
            var root = _uiDocument.rootVisualElement;

            // メインタブ
            _pageGame      = root.Q<VisualElement>("page-game");
            _pageInventory = root.Q<VisualElement>("page-inventory");
            _pageGacha     = root.Q<VisualElement>("page-gacha");
            _tabGame       = root.Q<Button>("tab-game");
            _tabInventory  = root.Q<Button>("tab-inventory");
            _tabGacha      = root.Q<Button>("tab-gacha");
            _btnPlay       = root.Q<Button>("btn-play");
            _btnProfile    = root.Q<Button>("btn-profile");
            _btnSettings   = root.Q<Button>("btn-settings");

            _matchmakingStatus = root.Q<Label>("matchmaking-status");

            _tabGame.clicked      += () => SwitchTab(0);
            _tabInventory.clicked += () => SwitchTab(1);
            _tabGacha.clicked     += () => SwitchTab(2);
            _btnPlay.clicked      += OnPlayClicked;
            _btnProfile.clicked   += OnProfileClicked;
            _btnSettings.clicked  += OpenSettings;

            GameServices.Matchmaking.MatchFound += OnMatchFound;

            // 設定オーバーレイ
            _settingsOverlay  = root.Q<VisualElement>("settings-overlay");
            _spageSound       = root.Q<VisualElement>("spage-sound");
            _spageGraphics    = root.Q<VisualElement>("spage-graphics");
            _spageControls    = root.Q<VisualElement>("spage-controls");
            _spageGame        = root.Q<VisualElement>("spage-game");
            _stabSound        = root.Q<Button>("stab-sound");
            _stabGraphics     = root.Q<Button>("stab-graphics");
            _stabControls     = root.Q<Button>("stab-controls");
            _stabGameTab      = root.Q<Button>("stab-game");
            _btnCloseSettings = root.Q<Button>("btn-close-settings");
            _btnApplySettings = root.Q<Button>("btn-settings-apply");

            _stabSound.clicked    += () => SwitchSettingsTab(0);
            _stabGraphics.clicked += () => SwitchSettingsTab(1);
            _stabControls.clicked += () => SwitchSettingsTab(2);
            _stabGameTab.clicked  += () => SwitchSettingsTab(3);
            _btnCloseSettings.clicked += CloseSettings;
            _btnApplySettings.clicked += ApplySettings;

            // スライダー・ドロップダウン
            _sliderBgm   = root.Q<Slider>("slider-bgm");
            _sliderSe    = root.Q<Slider>("slider-se");
            _sliderVoice = root.Q<Slider>("slider-voice");
            _labelBgmVal   = root.Q<Label>("label-bgm-val");
            _labelSeVal    = root.Q<Label>("label-se-val");
            _labelVoiceVal = root.Q<Label>("label-voice-val");
            _dropdownQuality = root.Q<DropdownField>("dropdown-quality");
            _dropdownWindow  = root.Q<DropdownField>("dropdown-window");

            _sliderBgm.RegisterValueChangedCallback(e =>
                _labelBgmVal.text = $"{Mathf.RoundToInt(e.newValue * 100)}%");
            _sliderSe.RegisterValueChangedCallback(e =>
                _labelSeVal.text = $"{Mathf.RoundToInt(e.newValue * 100)}%");
            _sliderVoice.RegisterValueChangedCallback(e =>
                _labelVoiceVal.text = $"{Mathf.RoundToInt(e.newValue * 100)}%");

            // 保存済み設定を反映
            _sliderBgm.value         = GameServices.Settings.BgmVolume;
            _sliderSe.value          = GameServices.Settings.SeVolume;
            _sliderVoice.value       = GameServices.Settings.VoiceVolume;
            _dropdownQuality.index   = GameServices.Settings.QualityLevel;
            _dropdownWindow.index    = GameServices.Settings.WindowMode;

            // ナビバーのプロフィールアイコン画像設定
            if (_playerIcon != null)
                root.Q<VisualElement>("profile-icon").style.backgroundImage =
                    Background.FromTexture2D(_playerIcon);

            // プロフィールオーバーレイ
            _profileOverlay      = root.Q<VisualElement>("profile-overlay");
            _profileOverlayIcon  = root.Q<VisualElement>("profile-overlay-icon");
            _btnCloseProfile     = root.Q<Button>("btn-close-profile");
            _profileCharCount    = root.Q<Label>("profile-char-count");
            _profileCrystalCount = root.Q<Label>("profile-crystal-count");
            _profileOverlayName  = root.Q<Label>("profile-overlay-name");
            _profileOverlayLevel = root.Q<Label>("profile-overlay-level");

            _btnCloseProfile.clicked += CloseProfile;

            // オーバーレイアイコンにも同じ画像を設定（ナビアイコンと共通）
            if (_playerIcon != null)
                _profileOverlayIcon.style.backgroundImage =
                    Background.FromTexture2D(_playerIcon);

            // 所持品サブタブ
            _itabChars  = root.Q<Button>("itab-chars");
            _itabSkins  = root.Q<Button>("itab-skins");
            _itabItems  = root.Q<Button>("itab-items");
            _charGrid   = root.Q<ScrollView>("char-grid");
            _ipageSkins = root.Q<VisualElement>("ipage-skins");
            _ipageItems = root.Q<VisualElement>("ipage-items");

            _itabChars.clicked += () => SwitchInventoryTab(0);
            _itabSkins.clicked += () => SwitchInventoryTab(1);
            _itabItems.clicked += () => SwitchInventoryTab(2);

            BuildFriendList(root.Q<ScrollView>("friend-list"));
            BuildCharacterGrid();

            // ガチャ
            _labelCrystals      = root.Q<Label>("crystal-count");
            _btnGacha1          = root.Q<Button>("btn-gacha-1");
            _btnGacha10         = root.Q<Button>("btn-gacha-10");
            _gachaResultOverlay = root.Q<VisualElement>("gacha-result-overlay");
            _gachaResultGrid    = root.Q<ScrollView>("gacha-result-grid");
            _btnCloseGachaResult = root.Q<Button>("btn-close-gacha-result");

            _btnGacha1.clicked           += () => OnGachaPull(1);
            _btnGacha10.clicked          += () => OnGachaPull(10);
            _btnCloseGachaResult.clicked += () => _gachaResultOverlay.style.display = DisplayStyle.None;

            RefreshGachaUI();

            // ゲームタブ: キャスト方式ドロップダウン
            _dropdownCastMode = root.Q<DropdownField>("dropdown-castmode");
            _dropdownCastMode.index = (int)GameServices.ControlSettings.CastMode;
            _dropdownCastMode.RegisterValueChangedCallback(_ =>
                GameServices.ControlSettings.SetCastMode((CastMode)_dropdownCastMode.index));

            // ゲームタブ: リバインドボタン（スロット 0..3）
            _rebindBtns = new Button[4];
            for (int i = 0; i < 4; i++)
            {
                var slot = i;
                _rebindBtns[i] = root.Q<Button>($"btn-rebind-{slot}");
                _rebindBtns[i].text = GameServices.ControlSettings.GetSkillKey(slot).ToString();
                _rebindBtns[i].clicked += () => StartRebind(slot);
            }

            SwitchTab(0);
        }

        private void OnDisable()
        {
            if (GameServices.Matchmaking != null)
                GameServices.Matchmaking.MatchFound -= OnMatchFound;
        }

        // ── メインタブ切り替え ─────────────────────────
        private void SwitchTab(int index)
        {
            VisualElement[] pages = { _pageGame, _pageInventory, _pageGacha };
            Button[]        tabs  = { _tabGame, _tabInventory, _tabGacha };
            for (int i = 0; i < pages.Length; i++)
            {
                bool active = i == index;
                SetClass(pages[i], "page--active", active);
                SetClass(tabs[i],  "nav-tab--active", active);
            }
        }

        // ── 所持品サブタブ切り替え ─────────────────────
        private void SwitchInventoryTab(int index)
        {
            Button[]        tabs  = { _itabChars, _itabSkins, _itabItems };
            // ipage-chars は ScrollView (_charGrid) なので VisualElement[] に合わせて扱う
            VisualElement[] pages = { _charGrid, _ipageSkins, _ipageItems };
            for (int i = 0; i < pages.Length; i++)
            {
                bool active = i == index;
                pages[i].style.display = active ? DisplayStyle.Flex : DisplayStyle.None;
                SetClass(tabs[i], "inventory-tab--active", active);
            }
        }

        // ── キャラクターグリッド構築 ───────────────────
        private void BuildCharacterGrid()
        {
            if (_charGrid == null) return;
            _charGrid.Clear();

            if (_characterDatabase == null)
            {
                Debug.LogWarning("[HomeScreen] CharacterDatabase が設定されていません");
                return;
            }

            var chars = _characterDatabase.GetSorted(GameServices.Ownership);
            foreach (var chara in chars)
            {
                var card = new VisualElement();
                card.AddToClassList("char-card");
                if (!GameServices.Ownership.IsOwned(chara))
                    card.AddToClassList("char-card--locked");

                // アイコン領域
                var iconEl = new VisualElement();
                iconEl.AddToClassList("char-card__icon");

                if (chara.Icon != null)
                {
                    iconEl.style.backgroundImage = Background.FromTexture2D(chara.Icon);
                }
                else
                {
                    // Icon が null → ThemeColor 背景 + 頭文字
                    iconEl.style.backgroundColor = new StyleColor(chara.ThemeColor);
                    var initial = new Label(chara.DisplayName.Length > 0
                        ? chara.DisplayName[..1]
                        : "?");
                    initial.AddToClassList("char-card__initial");
                    iconEl.Add(initial);
                }

                // 未所持ラベル（カード右上）
                if (!GameServices.Ownership.IsOwned(chara))
                {
                    var lockedLabel = new Label("未所持");
                    lockedLabel.AddToClassList("char-card__locked-label");
                    iconEl.Add(lockedLabel);
                }

                // 名前
                var nameLabel = new Label(chara.DisplayName);
                nameLabel.AddToClassList("char-card__name");

                // ロール
                var roleLabel = new Label(chara.RoleLabel);
                roleLabel.AddToClassList("char-card__role");

                card.Add(iconEl);
                card.Add(nameLabel);
                card.Add(roleLabel);
                _charGrid.Add(card);
            }
        }

        // ── ガチャ UI ──────────────────────────────────
        private void RefreshGachaUI()
        {
            if (_labelCrystals != null)
                _labelCrystals.text = GameServices.Gacha.Crystals.ToString("N0");

            if (_btnGacha1  != null) _btnGacha1.SetEnabled(GameServices.Gacha.Crystals  >= GachaService.SinglePullCost);
            if (_btnGacha10 != null) _btnGacha10.SetEnabled(GameServices.Gacha.Crystals >= GachaService.TenPullCost);
        }

        private void OnGachaPull(int count)
        {
            var results = new List<PullResult>();

            if (!GameServices.Gacha.TryPull(_characterDatabase.Characters, count, results))
            {
                Debug.LogWarning("[HomeScreen] ガチャ失敗: 残高不足またはキャラクター未登録");
                return;
            }

            // 結果オーバーレイを表示
            _gachaResultOverlay.style.display = DisplayStyle.Flex;
            _gachaResultGrid.Clear();

            foreach (var result in results)
            {
                var chara = result.Character;

                var card = new VisualElement();
                card.AddToClassList("char-card");

                // アイコン領域
                var iconEl = new VisualElement();
                iconEl.AddToClassList("char-card__icon");

                if (chara.Icon != null)
                {
                    iconEl.style.backgroundImage = Background.FromTexture2D(chara.Icon);
                }
                else
                {
                    iconEl.style.backgroundColor = new StyleColor(chara.ThemeColor);
                    var initial = new Label(chara.DisplayName.Length > 0 ? chara.DisplayName[..1] : "?");
                    initial.AddToClassList("char-card__initial");
                    iconEl.Add(initial);
                }

                // NEW / 重複バッジ（アイコン左上に絶対配置）
                var badge = new Label(result.IsNew ? "NEW!" : "重複");
                badge.AddToClassList(result.IsNew ? "gacha-badge--new" : "gacha-badge--dupe");
                iconEl.Add(badge);

                var nameLabel = new Label(chara.DisplayName);
                nameLabel.AddToClassList("char-card__name");

                var roleLabel = new Label(chara.RoleLabel);
                roleLabel.AddToClassList("char-card__role");

                card.Add(iconEl);
                card.Add(nameLabel);
                card.Add(roleLabel);
                _gachaResultGrid.Add(card);
            }

            // 所持状態が変わったのでグリッドを更新
            RefreshGachaUI();
            BuildCharacterGrid();
        }

        // ── プロフィールオーバーレイ開閉 ───────────────
        private void OpenProfile()
        {
            // 開くたびに最新値に更新（ガチャ後に古い値が残らないようにするため）
            if (_characterDatabase != null)
            {
                int owned = _characterDatabase.CountOwned(GameServices.Ownership);
                int total = _characterDatabase.TotalCount;
                _profileCharCount.text = $"{owned} / {total}";
            }
            else
            {
                _profileCharCount.text = "-";
            }

            _profileCrystalCount.text = GameServices.Gacha.Crystals.ToString("N0");

            _profileOverlay.style.display = DisplayStyle.Flex;
        }

        private void CloseProfile() =>
            _profileOverlay.style.display = DisplayStyle.None;

        // ── 設定オーバーレイ開閉 ───────────────────────
        private void OpenSettings()
        {
            _settingsOverlay.style.display = DisplayStyle.Flex;
            SwitchSettingsTab(0);
        }

        private void CloseSettings() =>
            _settingsOverlay.style.display = DisplayStyle.None;

        private void ApplySettings()
        {
            GameServices.Settings.Apply(
                _sliderBgm.value,
                _sliderSe.value,
                _sliderVoice.value,
                _dropdownQuality.index,
                _dropdownWindow.index
            );
            // 適用後も設定画面は閉じない。閉じるのは ✕ ボタン / ESC のみ
        }

        // ── 設定タブ切り替え ───────────────────────────
        private void SwitchSettingsTab(int index)
        {
            VisualElement[] pages = { _spageSound, _spageGraphics, _spageControls, _spageGame };
            Button[]        tabs  = { _stabSound, _stabGraphics, _stabControls, _stabGameTab };
            for (int i = 0; i < pages.Length; i++)
            {
                bool active = i == index;
                SetClass(pages[i], "settings-page--active", active);
                SetClass(tabs[i],  "settings-tab--active",  active);
            }
        }

        // ── リバインド開始 ─────────────────────────────
        private void StartRebind(int slot)
        {
            // 別スロットが待受中なら先にキャンセル
            if (_rebindingSlot >= 0) CancelRebind();

            _rebindingSlot = slot;
            _rebindBtns[slot].text = "キー入力待ち...";
            _rebindBtns[slot].AddToClassList("rebind-btn--listening");
        }

        private void CancelRebind()
        {
            if (_rebindingSlot < 0) return;
            var slot = _rebindingSlot;
            _rebindingSlot = -1;
            _rebindBtns[slot].text = GameServices.ControlSettings.GetSkillKey(slot).ToString();
            _rebindBtns[slot].RemoveFromClassList("rebind-btn--listening");
        }

        private void ConfirmRebind(Key key)
        {
            var slot = _rebindingSlot;
            _rebindingSlot = -1;
            GameServices.ControlSettings.SetSkillKey(slot, key);
            _rebindBtns[slot].text = key.ToString();
            _rebindBtns[slot].RemoveFromClassList("rebind-btn--listening");
        }

        // ── ESC キーでオーバーレイを閉じる ────────────
        private void Update()
        {
            // マッチング中は毎フレーム経過時間を進め、ラベルを更新する
            var mm = GameServices.Matchmaking;
            if (mm != null && mm.State == MatchmakingState.Searching)
            {
                mm.Tick(Time.deltaTime);
                int totalSec = Mathf.FloorToInt(mm.ElapsedSeconds);
                int m = totalSec / 60;
                int s = totalSec % 60;
                _matchmakingStatus.text = $"マッチング中… {m}:{s:D2}";
            }

            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            // リバインド待受中: ESC はキャンセルのみ（オーバーレイは閉じない）
            if (_rebindingSlot >= 0)
            {
                if (keyboard.escapeKey.wasPressedThisFrame)
                {
                    CancelRebind();
                    return;
                }

                // 全 Key を走査して最初に押されたキーを確定
                foreach (Key k in Enum.GetValues(typeof(Key)))
                {
                    // None・不正値はスキップ
                    if (k == Key.None) continue;
                    try
                    {
                        if (keyboard[k].wasPressedThisFrame)
                        {
                            ConfirmRebind(k);
                            return;
                        }
                    }
                    catch
                    {
                        // IMESelected 等、インデクサが対応しないキーは無視
                    }
                }
                return;
            }

            if (!keyboard.escapeKey.wasPressedThisFrame) return;

            // 優先順: ガチャ結果 > プロフィール > 設定（最前面の1つだけ閉じる）
            if (_gachaResultOverlay != null &&
                _gachaResultOverlay.style.display == DisplayStyle.Flex)
            {
                _gachaResultOverlay.style.display = DisplayStyle.None;
                return;
            }

            if (_profileOverlay != null &&
                _profileOverlay.style.display == DisplayStyle.Flex)
            {
                CloseProfile();
                return;
            }

            if (_settingsOverlay.style.display == DisplayStyle.Flex)
            {
                CloseSettings();
            }
        }

        // ── その他ボタン ───────────────────────────────
        private void OnPlayClicked()
        {
            var mm = GameServices.Matchmaking;
            if (mm.State == MatchmakingState.Idle || mm.State == MatchmakingState.Found)
            {
                mm.StartQueue();
                _btnPlay.text = "キャンセル";
                _matchmakingStatus.style.display = DisplayStyle.Flex;
                _matchmakingStatus.RemoveFromClassList("matchmaking-status--found");
            }
            else if (mm.State == MatchmakingState.Searching)
            {
                mm.Cancel();
                _btnPlay.text = "プレイ開始";
                _btnPlay.SetEnabled(true);
                _matchmakingStatus.style.display = DisplayStyle.None;
            }
        }

        private void OnMatchFound()
        {
            _matchmakingStatus.text = "マッチが見つかりました!";
            _matchmakingStatus.AddToClassList("matchmaking-status--found");
            _btnPlay.SetEnabled(false);
            StartCoroutine(LoadCharacterSelectAfterDelay());
        }

        private IEnumerator LoadCharacterSelectAfterDelay()
        {
            yield return new WaitForSeconds(1f);
            SceneManager.LoadScene("CharacterSelect");
        }

        private void OnProfileClicked() => OpenProfile();

        // ── フレンドリスト構築 ─────────────────────────
        private void BuildFriendList(ScrollView list)
        {
            list.Clear();

            if (_friendDatabase == null)
            {
                Debug.LogWarning("[HomeScreen] FriendDatabase が設定されていません");
                return;
            }

            var friends = _friendDatabase.GetSorted();

            // ヘッダーのオンライン人数を更新
            var countLabel = _uiDocument.rootVisualElement.Q<Label>("friend-count");
            if (countLabel != null)
                countLabel.text = $"{_friendDatabase.OnlineCount}/{_friendDatabase.TotalCount}";

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

                var nameLabel = new Label(friend.DisplayName);
                nameLabel.AddToClassList("friend-name");
                if (!friend.IsOnline) nameLabel.AddToClassList("friend-name--offline");

                var statusLabel = new Label(friend.StatusLabel);
                statusLabel.AddToClassList("friend-status");
                statusLabel.AddToClassList(friend.Status switch
                {
                    FriendStatus.InGame  => "friend-status--ingame",
                    FriendStatus.InQueue => "friend-status--inqueue",
                    FriendStatus.Online  => "friend-status--online",
                    _                    => "",
                });

                info.Add(nameLabel);
                info.Add(statusLabel);

                // レベル表示
                var levelLabel = new Label($"Lv.{friend.Level}");
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

        private void OnInviteFriend(FriendData friend)
        {
            Debug.Log($"[HomeScreen] {friend.DisplayName} を招待しました");
            // TODO: ネットワーク経由でパーティ招待を送信
        }

        private static void SetClass(VisualElement el, string cls, bool active)
        {
            if (active) el.AddToClassList(cls);
            else        el.RemoveFromClassList(cls);
        }
    }
}
