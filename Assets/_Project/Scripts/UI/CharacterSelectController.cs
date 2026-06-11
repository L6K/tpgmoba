using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;
using Enigma.Character;
using Enigma.Core;
using Enigma.Data;

namespace Enigma.UI
{
    public class CharacterSelectController : MonoBehaviour
    {
        [FormerlySerializedAs("uiDocument")]
        [SerializeField] private UIDocument _uiDocument;

        [FormerlySerializedAs("characterDatabase")]
        [SerializeField] private CharacterDatabase _characterDatabase;

        // ── UI 要素 ─────────────────────────────────────────
        private Label         _timerLabel;
        private Button        _lockBtn;
        private VisualElement _previewIcon;
        private Label         _previewInitial;
        private Label         _previewName;
        private Label         _previewRole;
        private Label         _previewDesc;
        private ScrollView    _grid;

        private readonly VisualElement[] _slotCards    = new VisualElement[3];
        private readonly VisualElement[] _slotIcons    = new VisualElement[3];
        private readonly Label[]         _slotInitials = new Label[3];
        private readonly Label[]         _slotNames    = new Label[3];
        private readonly Label[]         _slotPicks    = new Label[3];

        // ── ピック状態 ──────────────────────────────────────
        // 0 = 自分、1 = AI アキラ、2 = AI サクラ
        private static readonly string[] SlotPlayerNames = { "あなた", "アキラ", "サクラ" };
        private readonly bool[]          _slotLocked     = new bool[3];
        private readonly int[]           _slotPickIndex  = { -1, -1, -1 };

        // キャラリスト（DB.Characters の順序でインデックス管理）
        private List<CharacterData> _chars;
        private bool[]              _owned;

        // 自分の選択インデックス（未ロック時に更新）
        private int _mySelection = -1;

        // ── タイマー ────────────────────────────────────────
        private float _timeRemaining = 30f;
        private bool  _timerExpired;

        // ── AI スケジュール ─────────────────────────────────
        // AI スロット（1, 2）ごとのピック予定時刻（0 = 未設定）
        private readonly float[] _aiPickTime = new float[2];

        // IRandomSource は GameServices に持たせていないため直接 new する。
        // キャラピック AI の乱数は UI 層のみで使い捨て可能なため、
        // テスト対象の CharSelectLogic には外部から注入できる設計にしてある。
        private readonly IRandomSource _random = new SystemRandomSource();

        // ── ライフサイクル ───────────────────────────────────
        private void OnEnable()
        {
            if (!GameServices.IsInitialized) GameServices.Initialize();

            var root = _uiDocument.rootVisualElement;

            _timerLabel     = root.Q<Label>("cs-timer");
            _lockBtn        = root.Q<Button>("cs-lock");
            _previewIcon    = root.Q<VisualElement>("cs-preview-icon");
            _previewInitial = root.Q<Label>("cs-preview-initial");
            _previewName    = root.Q<Label>("cs-preview-name");
            _previewRole    = root.Q<Label>("cs-preview-role");
            _previewDesc    = root.Q<Label>("cs-preview-desc");
            _grid           = root.Q<ScrollView>("cs-grid");

            for (int i = 0; i < 3; i++)
            {
                _slotCards[i]    = root.Q<VisualElement>($"cs-slot-{i}");
                _slotIcons[i]    = root.Q<VisualElement>($"cs-slot-icon-{i}");
                _slotInitials[i] = root.Q<Label>($"cs-slot-initial-{i}");
                _slotNames[i]    = root.Q<Label>($"cs-slot-name-{i}");
                _slotPicks[i]    = root.Q<Label>($"cs-slot-pick-{i}");

                // 初期プレイヤー名をセット
                _slotNames[i].text = SlotPlayerNames[i];
            }

            _lockBtn.clicked += OnLockIn;

            BuildCharacterGrid();
            ScheduleAiPicks();

            // タイマー初期表示
            UpdateTimerLabel();
        }

        private void OnDisable()
        {
            if (_lockBtn != null) _lockBtn.clicked -= OnLockIn;
        }

        // ── Update: タイマー + AI ───────────────────────────
        private void Update()
        {
            if (_timerExpired) return;

            _timeRemaining -= Time.deltaTime;
            if (_timeRemaining < 0f) _timeRemaining = 0f;

            UpdateTimerLabel();

            // AI ピックスケジュール確認（スロット 1, 2 = AI インデックス 0, 1）
            for (int ai = 0; ai < 2; ai++)
            {
                int slot = ai + 1;
                if (_slotLocked[slot]) continue;
                if (_aiPickTime[ai] > 0f && Time.time >= _aiPickTime[ai])
                {
                    ExecuteAiPick(slot);
                }
            }

            // タイマー満了
            if (_timeRemaining <= 0f && !_timerExpired)
            {
                _timerExpired = true;
                HandleTimerExpired();
            }
        }

        // ── グリッド構築 ────────────────────────────────────
        private void BuildCharacterGrid()
        {
            _grid.Clear();
            if (_characterDatabase == null)
            {
                Debug.LogWarning("[CharSelect] CharacterDatabase が設定されていません");
                return;
            }

            _chars = _characterDatabase.Characters;
            _owned = new bool[_chars.Count];

            for (int i = 0; i < _chars.Count; i++)
            {
                var chara = _chars[i];
                if (chara == null) continue;

                bool isOwned = GameServices.Ownership.IsOwned(chara);
                _owned[i] = isOwned;

                var card = new VisualElement();
                card.AddToClassList("cs-card");

                if (!isOwned)
                {
                    card.AddToClassList("cs-card--locked");
                    // 未所持はクリック不能にするため pickable を外す
                    card.pickingMode = PickingMode.Ignore;
                }

                // アイコン領域
                var iconEl = new VisualElement();
                iconEl.AddToClassList("cs-card__icon");
                iconEl.style.backgroundColor = new StyleColor(chara.ThemeColor);

                if (chara.Icon != null)
                    iconEl.style.backgroundImage = Background.FromTexture2D(chara.Icon);

                var initial = new Label(chara.DisplayName.Length > 0 ? chara.DisplayName[..1] : "?");
                initial.AddToClassList("cs-card__initial");
                if (chara.Icon != null) initial.style.display = DisplayStyle.None;
                iconEl.Add(initial);

                var nameLabel = new Label(chara.DisplayName);
                nameLabel.AddToClassList("cs-card__name");

                card.Add(iconEl);
                card.Add(nameLabel);

                int capturedIndex = i;
                if (isOwned)
                {
                    card.RegisterCallback<ClickEvent>(_ => OnCardClicked(capturedIndex));
                }

                _grid.Add(card);
            }
        }

        // ── カードクリック ───────────────────────────────────
        private void OnCardClicked(int index)
        {
            // ロック済みなら選択変更不可
            if (_slotLocked[0]) return;

            _mySelection = index;
            UpdateCardSelection();
            UpdatePreview(index);
            UpdateSlot(0, index, locked: false);
        }

        private void UpdateCardSelection()
        {
            var cards = _grid.Query<VisualElement>(className: "cs-card").ToList();
            for (int i = 0; i < cards.Count; i++)
            {
                if (i == _mySelection)
                    cards[i].AddToClassList("cs-card--selected");
                else
                    cards[i].RemoveFromClassList("cs-card--selected");
            }
        }

        private void UpdatePreview(int index)
        {
            if (index < 0 || index >= _chars.Count) return;
            var chara = _chars[index];

            _previewIcon.style.backgroundColor = new StyleColor(chara.ThemeColor);
            if (chara.Icon != null)
            {
                _previewIcon.style.backgroundImage = Background.FromTexture2D(chara.Icon);
                _previewInitial.style.display = DisplayStyle.None;
            }
            else
            {
                _previewIcon.style.backgroundImage = StyleKeyword.None;
                _previewInitial.style.display = DisplayStyle.Flex;
                _previewInitial.text = chara.DisplayName.Length > 0 ? chara.DisplayName[..1] : "?";
            }

            _previewName.text = chara.DisplayName;
            _previewRole.text = chara.RoleLabel;
            _previewDesc.text = chara.Description;
        }

        // ── スロット表示更新 ─────────────────────────────────
        private void UpdateSlot(int slotIndex, int charIndex, bool locked)
        {
            if (charIndex < 0 || charIndex >= _chars.Count) return;
            var chara = _chars[charIndex];

            _slotIcons[slotIndex].style.backgroundColor = new StyleColor(chara.ThemeColor);
            _slotInitials[slotIndex].text = chara.DisplayName.Length > 0 ? chara.DisplayName[..1] : "?";
            _slotPicks[slotIndex].text    = chara.DisplayName;

            if (locked)
                _slotCards[slotIndex].AddToClassList("cs-slot--locked");
        }

        // ── ロックイン ───────────────────────────────────────
        private void OnLockIn()
        {
            if (_slotLocked[0]) return;

            // ResolveAutoLock と同じロジック: 未選択または未所持なら最初の所持キャラへ
            int finalIndex = CharSelectLogic.ResolveAutoLock(_mySelection, _owned);
            if (finalIndex < 0) return;

            _mySelection = finalIndex;
            LockSlot(0, finalIndex);
        }

        private void LockSlot(int slotIndex, int charIndex)
        {
            if (_slotLocked[slotIndex]) return;
            _slotLocked[slotIndex]    = true;
            _slotPickIndex[slotIndex] = charIndex;

            UpdateSlot(slotIndex, charIndex, locked: true);

            if (slotIndex == 0)
            {
                // 自分ロック: ボタン無効化
                _lockBtn.SetEnabled(false);
                _lockBtn.text = "ロック済み";

                // GameServices にピック結果を反映
                if (charIndex >= 0 && charIndex < _chars.Count)
                    GameServices.Match.PickedCharacter = _chars[charIndex];
            }

            CheckAllLocked();
        }

        // ── AI スケジュール ─────────────────────────────────
        private void ScheduleAiPicks()
        {
            for (int ai = 0; ai < 2; ai++)
            {
                // 開始から 3 + random(0..5) 秒後にピック（各 AI 独立）
                _aiPickTime[ai] = Time.time + 3f + _random.Next(6);
            }
        }

        private void ExecuteAiPick(int slotIndex)
        {
            // taken: 既にロック済みのキャラインデックスに true を立てる
            var taken = BuildTakenArray();
            int ai    = slotIndex - 1;

            int pick = CharSelectLogic.ChooseAiPick(taken, _owned, _random);
            if (pick < 0) return;

            // 選んだ直後に taken に追加（AI 同士の重複を防ぐ）
            LockSlot(slotIndex, pick);
        }

        private bool[] BuildTakenArray()
        {
            var taken = new bool[_chars.Count];
            for (int s = 0; s < 3; s++)
            {
                if (_slotLocked[s] && _slotPickIndex[s] >= 0)
                    taken[_slotPickIndex[s]] = true;
            }
            return taken;
        }

        // ── タイマー満了 ─────────────────────────────────────
        private void HandleTimerExpired()
        {
            // 未ロックの AI スロットを強制ピック
            for (int s = 1; s < 3; s++)
            {
                if (!_slotLocked[s]) ExecuteAiPick(s);
            }

            // 自分が未ロックなら ResolveAutoLock で確定
            if (!_slotLocked[0])
            {
                int finalIndex = CharSelectLogic.ResolveAutoLock(_mySelection, _owned);
                if (finalIndex >= 0)
                {
                    _mySelection = finalIndex;
                    LockSlot(0, finalIndex);
                }
            }
        }

        private void UpdateTimerLabel()
        {
            int total = Mathf.CeilToInt(_timeRemaining);
            int m = total / 60;
            int s = total % 60;
            _timerLabel.text = $"{m}:{s:D2}";
        }

        // ── 全員ロック確認 ───────────────────────────────────
        private void CheckAllLocked()
        {
            for (int i = 0; i < 3; i++)
            {
                if (!_slotLocked[i]) return;
            }
            // 全員ロック確認: 1.5 秒後に AetherRift_Map へ遷移
            StartCoroutine(LoadMapAfterDelay());
        }

        private IEnumerator LoadMapAfterDelay()
        {
            yield return new WaitForSeconds(1.5f);
            SceneManager.LoadScene("AetherRift_Map");
        }
    }
}
