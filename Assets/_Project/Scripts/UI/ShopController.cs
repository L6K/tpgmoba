using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.InputSystem;
using Enigma.Combat;
using Enigma.Item;
using Enigma.Character;

namespace Enigma.UI
{
    /// <summary>
    /// アイテムショップオーバーレイの表示制御と購入処理を担うハンブルオブジェクト。
    /// </summary>
    public sealed class ShopController : MonoBehaviour
    {
        [SerializeField] private UIDocument      _uiDocument;
        [SerializeField] private ItemShopCatalog _catalog;
        [SerializeField] private Transform       _player;

        // ビルダーが青本拠地中心 (-56,0,0) を設定する
        [SerializeField] private Vector3 _shopCenter = new Vector3(-56f, 0f, 0f);

        // ショップは後方の安全パッド(泉)と同じコンパクト範囲に限定する。基地全体や
        // タイタン前広場をショップ圏にしないことで「後方=復帰/回復/購入」の役割を分離する。
        private const float ShopRadius = 6f;

        private VisualElement _shopOverlay;
        private VisualElement _shopGrid;
        private Label         _shopGold;
        private Label         _shopHint;
        private Button        _shopClose;

        private PlayerItems  _playerItems;
        private PlayerWallet _playerWallet;

        private bool _isOpen;

        // hint 表示コルーチンの二重起動を防ぐフラグ
        private bool _hintShowing;

        private void OnEnable()
        {
            if (_uiDocument == null) return;
            var root = _uiDocument.rootVisualElement;

            _shopOverlay = root.Q<VisualElement>("shop-overlay");
            _shopGrid    = root.Q<VisualElement>("shop-grid");
            _shopGold    = root.Q<Label>("shop-gold");
            _shopHint    = root.Q<Label>("hud-shop-hint");
            _shopClose   = root.Q<Button>("shop-close");

            if (_shopClose != null)
                _shopClose.clicked += CloseShop;
        }

        private void OnDisable()
        {
            if (_shopClose != null)
                _shopClose.clicked -= CloseShop;
        }

        private void Start()
        {
            // プレイヤー Transform から Wallet / Items を遅延取得
            // （ビルダーが Player GO を先に生成するため Start で安全）
            if (_player != null)
            {
                _playerWallet = _player.GetComponent<PlayerWallet>();
                _playerItems  = _player.GetComponent<PlayerItems>();
            }
        }

        private void Update()
        {
            if (Keyboard.current == null) return;

            // P キーでショップトグル
            if (Keyboard.current.pKey.wasPressedThisFrame)
            {
                if (_isOpen)
                {
                    CloseShop();
                }
                else if (IsPlayerInShopRange())
                {
                    OpenShop();
                }
                else
                {
                    // 範囲外メッセージを2秒表示（二重起動しない）
                    if (!_hintShowing)
                        StartCoroutine(ShowHint());
                }
            }

            // ESC で閉じる（開いているときのみ。SkillCaster の ESC と競合しても実害なし）
            if (_isOpen && Keyboard.current.escapeKey.wasPressedThisFrame)
                CloseShop();

            // ショップ開放中は所持金ラベルとカードの disabled 状態を毎フレーム更新
            if (_isOpen)
                RefreshShopState();
        }

        // ── private ───────────────────────────────────────────────

        private bool IsPlayerInShopRange()
        {
            if (_player == null) return false;
            var diff = _player.position - _shopCenter;
            diff.y = 0f;
            return diff.sqrMagnitude <= ShopRadius * ShopRadius;
        }

        private void OpenShop()
        {
            if (_shopOverlay == null) return;

            BuildGrid();
            _shopOverlay.style.display = DisplayStyle.Flex;
            _isOpen = true;
        }

        private void CloseShop()
        {
            if (_shopOverlay == null) return;

            _shopOverlay.style.display = DisplayStyle.None;
            _isOpen = false;
        }

        // カードを全再構築する（開くたびに呼ぶ）
        private void BuildGrid()
        {
            if (_shopGrid == null || _catalog == null) return;

            _shopGrid.Clear();

            foreach (var item in _catalog.Items)
            {
                if (item == null) continue;

                var card = BuildCard(item);
                _shopGrid.Add(card);
            }
        }

        private VisualElement BuildCard(ItemData item)
        {
            // カード本体
            var card = new VisualElement();
            card.AddToClassList("shop-card");

            // 色タイル（ThemeColor 背景）
            var tile = new VisualElement();
            tile.AddToClassList("shop-card-tile");
            tile.style.backgroundColor = new StyleColor(item.ThemeColor);

            var initial = new Label(item.ItemName.Length > 0 ? item.ItemName[..1] : "?");
            initial.AddToClassList("shop-card-initial");
            tile.Add(initial);
            card.Add(tile);

            // 情報エリア
            var info = new VisualElement();
            info.AddToClassList("shop-card-info");

            var nameLabel = new Label(item.ItemName);
            nameLabel.AddToClassList("shop-card-name");

            var effectLabel = new Label(item.Description);
            effectLabel.AddToClassList("shop-card-effect");

            var priceLabel = new Label($"{item.Price} G");
            priceLabel.AddToClassList("shop-card-price");

            info.Add(nameLabel);
            info.Add(effectLabel);
            info.Add(priceLabel);
            card.Add(info);

            // disabled 状態の初期設定
            UpdateCardDisabled(card, item);

            // クリックで購入試行
            card.RegisterCallback<ClickEvent>(_ => OnCardClicked(card, item));

            return card;
        }

        private void OnCardClicked(VisualElement card, ItemData item)
        {
            if (_playerItems == null) return;

            bool success = _playerItems.TryPurchase(item);
            if (success)
            {
                // 購入成功: グリッドと disabled 状態を全更新
                BuildGrid();
            }
        }

        // ショップ開放中に毎フレーム呼ばれる: 所持金・disabled を軽量更新
        private void RefreshShopState()
        {
            if (_playerWallet != null && _shopGold != null)
                _shopGold.text = $"{_playerWallet.Wallet.Gold} G";

            if (_shopGrid == null || _catalog == null) return;

            int cardIndex = 0;
            foreach (var child in _shopGrid.Children())
            {
                if (cardIndex >= _catalog.Items.Count) break;
                var item = _catalog.Items[cardIndex];
                if (item != null)
                    UpdateCardDisabled(child, item);
                cardIndex++;
            }
        }

        // ゴールド不足 or 6枠満杯のとき disabled クラスを付与
        private void UpdateCardDisabled(VisualElement card, ItemData item)
        {
            bool notEnoughGold = _playerWallet == null || _playerWallet.Wallet.Gold < item.Price;
            bool slotsFull     = _playerItems  == null || _playerItems.Inventory.Items.Count >= 6;

            card.EnableInClassList("shop-card--disabled", notEnoughGold || slotsFull);
        }

        private IEnumerator ShowHint()
        {
            if (_shopHint == null) yield break;

            _hintShowing = true;
            _shopHint.style.display = DisplayStyle.Flex;
            yield return new WaitForSeconds(2f);
            _shopHint.style.display = DisplayStyle.None;
            _hintShowing = false;
        }
    }
}
