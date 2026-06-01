using UnityEngine;
using UnityEngine.UIElements;

namespace Enigma.UI
{
    public class HomeScreenController : MonoBehaviour
    {
        [SerializeField] UIDocument uiDocument;

        [Header("Profile")]
        [SerializeField] Texture2D playerIcon; // Inspector でアイコン画像を設定

        VisualElement pageGame, pageInventory, pageGacha;
        Button tabGame, tabInventory, tabGacha;
        Button btnPlay, btnProfile;

        static readonly (string name, bool online)[] FriendData =
        {
            ("山田", true),
            ("鈴木", false),
            ("田中", true),
        };

        void OnEnable()
        {
            var root = uiDocument.rootVisualElement;

            pageGame      = root.Q<VisualElement>("page-game");
            pageInventory = root.Q<VisualElement>("page-inventory");
            pageGacha     = root.Q<VisualElement>("page-gacha");

            tabGame      = root.Q<Button>("tab-game");
            tabInventory = root.Q<Button>("tab-inventory");
            tabGacha     = root.Q<Button>("tab-gacha");
            btnPlay      = root.Q<Button>("btn-play");
            btnProfile   = root.Q<Button>("btn-profile");

            tabGame.clicked      += () => SwitchTab(0);
            tabInventory.clicked += () => SwitchTab(1);
            tabGacha.clicked     += () => SwitchTab(2);
            btnPlay.clicked      += OnPlayClicked;
            btnProfile.clicked   += OnProfileClicked;

            // プロフィールアイコン画像が設定されていれば適用
            if (playerIcon != null)
            {
                var profileIcon = btnProfile.Q<VisualElement>("profile-icon");
                profileIcon?.style.backgroundImage.Equals(Background.FromTexture2D(playerIcon));
                profileIcon.style.backgroundImage = Background.FromTexture2D(playerIcon);
            }

            BuildFriendList(root.Q<ScrollView>("friend-list"));
            SwitchTab(0);
        }

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

        void OnPlayClicked()
        {
            Debug.Log("[HomeScreen] プレイ開始");
            // TODO: SceneManager.LoadScene("CharacterSelect");
        }

        void OnProfileClicked()
        {
            Debug.Log("[HomeScreen] プロフィール表示");
            // TODO: プロフィールパネルを開く
        }

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
