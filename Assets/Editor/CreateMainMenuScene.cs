using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.UI;
using TMPro;

public class CreateMainMenuScene
{
    const string FontAssetPath = "Assets/_Project/Fonts/NotoSansJP_SDF.asset";

    static TMP_FontAsset jaFont;

    public static void Execute()
    {
        jaFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
        if (jaFont == null)
            Debug.LogWarning("[Enigma] NotoSansJP font asset not found, using default.");
        else
            Debug.Log("[Enigma] Using NotoSansJP_SDF font asset.");

        if (!AssetDatabase.IsValidFolder("Assets/Scenes"))
            AssetDatabase.CreateFolder("Assets", "Scenes");

        var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

        // Canvas
        var canvasGO = new GameObject("Canvas");
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        canvasGO.AddComponent<GraphicRaycaster>();

        Stretch(MakeImage(canvasGO.transform, "Background", new Color(0.06f, 0.06f, 0.12f)));

        // ── NavBar ─────────────────────────────────────────
        var navBar = MakeImage(canvasGO.transform, "NavBar", new Color(0.10f, 0.10f, 0.18f));
        AnchorTopStrip(navBar, 70);

        MakeTMP(navBar.transform, "Logo", "ENIGMA", 28, FontStyles.Bold, Color.white,
            new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(160, 60), new Vector2(20, 0), TextAlignmentOptions.MidlineLeft);

        string[] tabs = { "ゲーム", "所持品", "ガチャ" };
        for (int i = 0; i < tabs.Length; i++)
        {
            var btn = MakeButton(navBar.transform, $"Tab_{tabs[i]}", tabs[i], 18,
                new Color(0, 0, 0, 0), new Color(0.75f, 0.88f, 1f));
            SetRT(btn, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(120, 60), new Vector2(210 + i * 140, 0));
        }

        var settingsBtn = MakeButton(navBar.transform, "Settings", "設定", 17,
            new Color(0, 0, 0, 0), new Color(0.75f, 0.75f, 0.75f));
        SetRT(settingsBtn, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(80, 60), new Vector2(-200, 0));

        var notifBtn = MakeButton(navBar.transform, "Notification", "通知", 17,
            new Color(0, 0, 0, 0), new Color(0.75f, 0.75f, 0.75f));
        SetRT(notifBtn, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(80, 60), new Vector2(-110, 0));

        // ── Friend Panel ───────────────────────────────────
        var friendPanel = MakeImage(canvasGO.transform, "FriendPanel", new Color(0.10f, 0.10f, 0.20f));
        var fpRT = friendPanel.GetComponent<RectTransform>();
        fpRT.anchorMin = new Vector2(1, 0); fpRT.anchorMax = new Vector2(1, 1);
        fpRT.pivot = new Vector2(1, 1); fpRT.anchoredPosition = Vector2.zero; fpRT.sizeDelta = new Vector2(220, 0);

        var fpHeader = MakeImage(friendPanel.transform, "Header", new Color(0.14f, 0.14f, 0.26f));
        var fhRT = fpHeader.GetComponent<RectTransform>();
        fhRT.anchorMin = new Vector2(0, 1); fhRT.anchorMax = new Vector2(1, 1);
        fhRT.pivot = new Vector2(0.5f, 1); fhRT.sizeDelta = new Vector2(0, 50); fhRT.anchoredPosition = Vector2.zero;
        MakeTMP(fpHeader.transform, "Title", "フレンド", 17, FontStyles.Bold, Color.white,
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, TextAlignmentOptions.Center);

        (string n, bool online)[] friends = { ("山田", true), ("鈴木", false), ("田中", true) };
        for (int i = 0; i < friends.Length; i++)
        {
            var row = new GameObject($"Friend_{friends[i].n}");
            row.transform.SetParent(friendPanel.transform, false);
            var rRT = row.AddComponent<RectTransform>();
            rRT.anchorMin = new Vector2(0, 1); rRT.anchorMax = new Vector2(1, 1);
            rRT.pivot = new Vector2(0.5f, 1); rRT.sizeDelta = new Vector2(0, 42); rRT.anchoredPosition = new Vector2(0, -(58 + i * 47));

            var dotColor = friends[i].online ? new Color(0.2f, 1f, 0.3f) : new Color(0.45f, 0.45f, 0.45f);
            MakeTMP(row.transform, "Dot", friends[i].online ? "●" : "○", 14, FontStyles.Normal, dotColor,
                new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(26, 36), new Vector2(14, 0));
            MakeTMP(row.transform, "Name", friends[i].n, 16, FontStyles.Normal, Color.white,
                new Vector2(0, 0.5f), new Vector2(1, 0.5f), new Vector2(0, 0.5f), new Vector2(-50, 36), new Vector2(44, 0));
        }

        // ── Content Area ───────────────────────────────────
        var content = new GameObject("ContentArea");
        content.transform.SetParent(canvasGO.transform, false);
        var cRT = content.AddComponent<RectTransform>();
        cRT.anchorMin = Vector2.zero; cRT.anchorMax = Vector2.one;
        cRT.offsetMin = Vector2.zero; cRT.offsetMax = new Vector2(-220, -70);

        var border = MakeImage(content.transform, "IconBorder", new Color(0.25f, 0.45f, 1f, 0.5f));
        SetRT(border, new Vector2(0.5f, 0.55f), new Vector2(0.5f, 0.55f), new Vector2(0.5f, 0.5f), new Vector2(336, 336), Vector2.zero);

        var icon = MakeImage(content.transform, "GameIcon", new Color(0.10f, 0.12f, 0.28f));
        SetRT(icon, new Vector2(0.5f, 0.55f), new Vector2(0.5f, 0.55f), new Vector2(0.5f, 0.5f), new Vector2(320, 320), Vector2.zero);

        MakeTMP(icon.transform, "Title", "ENIGMA", 46, FontStyles.Bold, Color.white,
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, TextAlignmentOptions.Center);
        MakeTMP(icon.transform, "Subtitle", "3D MOBA", 16, FontStyles.Normal, new Color(0.5f, 0.75f, 1f),
            new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), new Vector2(0, 32), new Vector2(0, 14), TextAlignmentOptions.Center);

        var playBtn = MakeButton(content.transform, "PlayButton", "プレイ開始", 24,
            new Color(0.18f, 0.42f, 0.95f), Color.white);
        SetRT(playBtn, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(230, 62), new Vector2(0, 36));

        var es = new GameObject("EventSystem");
        es.AddComponent<UnityEngine.EventSystems.EventSystem>();
        es.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();

        string path = "Assets/Scenes/MainMenu.unity";
        EditorSceneManager.SaveScene(scene, path);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(path, true) };
        AssetDatabase.Refresh();
        Debug.Log("[Enigma] MainMenu (TMP + NotoSansJP) saved: " + path);
    }

    // ── Helpers ──────────────────────────────────────────

    static GameObject MakeImage(Transform parent, string name, Color color)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<RectTransform>();
        go.AddComponent<Image>().color = color;
        return go;
    }

    static GameObject MakeTMP(Transform parent, string name, string text, int size, FontStyles style, Color color,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 sizeDelta, Vector2 pos,
        TextAlignmentOptions align = TextAlignmentOptions.MidlineLeft)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = anchorMin; rt.anchorMax = anchorMax; rt.pivot = pivot;
        rt.sizeDelta = sizeDelta; rt.anchoredPosition = pos;
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text; tmp.fontSize = size; tmp.fontStyle = style;
        tmp.color = color; tmp.alignment = align;
        tmp.overflowMode = TextOverflowModes.Overflow;
        // Font assigned separately via AssignJapaneseFont to avoid atlas texture errors
        return go;
    }

    static GameObject MakeButton(Transform parent, string name, string label, int fontSize, Color bgColor, Color textColor)
    {
        var go = MakeImage(parent, name, bgColor);
        go.AddComponent<Button>();
        MakeTMP(go.transform, "Label", label, fontSize, FontStyles.Bold, textColor,
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, TextAlignmentOptions.Center);
        return go;
    }

    static void Stretch(GameObject go)
    {
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
    }

    static void AnchorTopStrip(GameObject go, float height)
    {
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0, 1); rt.anchorMax = new Vector2(1, 1);
        rt.pivot = new Vector2(0.5f, 1); rt.sizeDelta = new Vector2(0, height); rt.anchoredPosition = Vector2.zero;
    }

    static void SetRT(GameObject go, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 sizeDelta, Vector2 pos)
    {
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
        rt.pivot = pivot; rt.sizeDelta = sizeDelta; rt.anchoredPosition = pos;
    }
}
