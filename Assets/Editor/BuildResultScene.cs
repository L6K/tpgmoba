using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;
using Enigma.UI;

public static class BuildResultScene
{
    private const string ScenePath = "Assets/Scenes/Result.unity";

    public static void Execute()
    {
        // 1. 空シーン作成
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // 2. Main Camera（UI が全面を覆うため黒 SolidColor で十分）
        var camGo = new GameObject("Main Camera");
        camGo.tag = "MainCamera";
        var cam = camGo.AddComponent<Camera>();
        cam.clearFlags      = CameraClearFlags.SolidColor;
        cam.backgroundColor = Color.black;
        camGo.AddComponent<AudioListener>();

        // 3. ResultUI GameObject + UIDocument
        var uiGo  = new GameObject("ResultUI");
        var hudDoc = uiGo.AddComponent<UIDocument>();

        var panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>(
            "Assets/_Project/UI/HomeScreenPanelSettings.asset");
        if (panelSettings != null)
            hudDoc.panelSettings = panelSettings;

        var resultUxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
            "Assets/_Project/UI/Result.uxml");
        if (resultUxml != null)
            hudDoc.visualTreeAsset = resultUxml;

        EditorUtility.SetDirty(hudDoc);

        // 4. ResultScreenController をアタッチし SerializedObject で結線
        var ctrl   = uiGo.AddComponent<ResultScreenController>();
        var soCtrl = new SerializedObject(ctrl);
        soCtrl.FindProperty("_uiDocument").objectReferenceValue = hudDoc;
        soCtrl.ApplyModifiedPropertiesWithoutUndo();

        // 5. シーン保存
        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        // 6. EditorBuildSettings: MainMenu / CharacterSelect / AetherRift_Map / Result の4本
        var buildScenes = new[]
        {
            new EditorBuildSettingsScene("Assets/Scenes/MainMenu.unity",        true),
            new EditorBuildSettingsScene("Assets/Scenes/CharacterSelect.unity", true),
            new EditorBuildSettingsScene("Assets/Scenes/AetherRift_Map.unity",  true),
            new EditorBuildSettingsScene("Assets/Scenes/Result.unity",          true),
        };
        EditorBuildSettings.scenes = buildScenes;

        Debug.Log("[BuildResultScene] Result.unity を保存し、ビルド設定を更新しました。");
    }
}
