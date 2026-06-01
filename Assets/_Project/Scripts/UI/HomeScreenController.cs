using UnityEngine;
using UnityEngine.UIElements;

namespace Enigma.UI
{
    public class HomeScreenController : MonoBehaviour
    {
        [SerializeField] UIDocument uiDocument;

        // Tab pages
        VisualElement pageGame, pageInventory, pageGacha;
        Button tabGame, tabInventory, tabGacha;
        Button btnPlay;

        // Friend data (placeholder until networking is implemented)
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

            tabGame.clicked      += () => SwitchTab(0);
            tabInventory.clicked += () => SwitchTab(1);
            tabGacha.clicked     += () => SwitchTab(2);
            btnPlay.clicked      += OnPlayClicked;

            BuildFriendList(root.Q<ScrollView>("friend-list"));

            SwitchTab(0);
        }

        void SwitchTab(int index)
        {
            VisualElement[] pages = { pageGame, pageInventory, pageGacha };
            Button[] tabs         = { tabGame, tabInventory, tabGacha };

            for (int i = 0; i < pages.Length; i++)
            {
                bool active = i == index;
                SetClass(pages[i], "page--active", active);
                SetClass(tabs[i],  "nav-tab--active", active);
            }

            // プレイボタンはゲームタブのみ表示
            btnPlay.style.display = index == 0 ? DisplayStyle.Flex : DisplayStyle.None;
        }

        void OnPlayClicked()
        {
            Debug.Log("[HomeScreen] プレイ開始ボタンが押されました");
            // TODO: SceneManager.LoadScene("CharacterSelect");
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

        static void SetClass(VisualElement el, string className, bool active)
        {
            if (active) el.AddToClassList(className);
            else        el.RemoveFromClassList(className);
        }
    }
}
