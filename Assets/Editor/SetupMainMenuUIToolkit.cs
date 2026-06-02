using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.UIElements;
using TMPro;

public class SetupMainMenuUIToolkit
{
    public static void Execute()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Scenes"))
            AssetDatabase.CreateFolder("Assets", "Scenes");

        var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

        // PanelSettings を取得（なければ作成）
        string psPath = "Assets/_Project/UI/HomeScreenPanelSettings.asset";
        var panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>(psPath);
        if (panelSettings == null)
        {
            panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
            panelSettings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            panelSettings.referenceResolution = new Vector2Int(1920, 1080);
            panelSettings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            panelSettings.match = 0.5f;

            // NotoSansJP フォントを設定
            var fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/_Project/Fonts/NotoSansJP-Regular SDF.asset");
            if (fontAsset != null)
            {
                panelSettings.textSettings = null; // UI Toolkit のテキスト設定は別途
                Debug.Log("[Enigma] Found NotoSansJP font asset.");
            }

            AssetDatabase.CreateAsset(panelSettings, psPath);
            AssetDatabase.SaveAssets();
            Debug.Log("[Enigma] PanelSettings created: " + psPath);
        }

        // UXML アセット取得
        var uxmlAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/_Project/UI/HomeScreen.uxml");
        if (uxmlAsset == null)
        {
            Debug.LogError("[Enigma] HomeScreen.uxml not found.");
            return;
        }

        // UIDocument GameObject を作成
        var uiGO = new GameObject("UIDocument");
        var uiDoc = uiGO.AddComponent<UIDocument>();
        uiDoc.panelSettings = panelSettings;
        uiDoc.visualTreeAsset = uxmlAsset;
        uiDoc.sortingOrder = 0;

        // HomeScreenController を追加
        var controller = uiGO.AddComponent<Enigma.UI.HomeScreenController>();
        // SerializedObject 経由で uiDocument フィールドを設定
        var so = new SerializedObject(controller);
        so.FindProperty("uiDocument").objectReferenceValue = uiDoc;
        so.ApplyModifiedProperties();

        // EventSystem（新InputSystem対応）
        var es = new GameObject("EventSystem");
        es.AddComponent<UnityEngine.EventSystems.EventSystem>();
        es.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();

        string scenePath = "Assets/Scenes/MainMenu.unity";
        EditorSceneManager.SaveScene(scene, scenePath);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(scenePath, true) };
        AssetDatabase.Refresh();
        Debug.Log("[Enigma] MainMenu (UI Toolkit) scene saved.");
    }
}
