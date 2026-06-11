using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;
using Enigma.UI;

public static class BuildCharacterSelectScene
{
    private const string ScenePath = "Assets/Scenes/CharacterSelect.unity";

    public static void Execute()
    {
        // 1. 空シーン作成
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // 2. Main Camera（背景は UI が全面を覆うため黒 SolidColor で十分）
        var camGo = new GameObject("Main Camera");
        camGo.tag = "MainCamera";
        var cam = camGo.AddComponent<Camera>();
        cam.clearFlags       = CameraClearFlags.SolidColor;
        cam.backgroundColor  = new Color(10f / 255f, 12f / 255f, 22f / 255f, 1f);
        camGo.AddComponent<AudioListener>();

        // 3. CharacterSelectUI GameObject + UIDocument
        var uiGo = new GameObject("CharacterSelectUI");
        var hudDoc = uiGo.AddComponent<UIDocument>();

        // HomeScreenPanelSettings を流用（プロジェクト共通のパネル設定）
        var panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>(
            "Assets/_Project/UI/HomeScreenPanelSettings.asset");
        if (panelSettings != null)
            hudDoc.panelSettings = panelSettings;

        var csUxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
            "Assets/_Project/UI/CharacterSelect.uxml");
        if (csUxml != null)
            hudDoc.visualTreeAsset = csUxml;

        EditorUtility.SetDirty(hudDoc);

        // 4. CharacterSelectController をアタッチし SerializedObject で結線
        var ctrl   = uiGo.AddComponent<CharacterSelectController>();
        var soCtrl = new SerializedObject(ctrl);

        soCtrl.FindProperty("_uiDocument").objectReferenceValue = hudDoc;

        var db = AssetDatabase.LoadAssetAtPath<Enigma.Character.CharacterDatabase>(
            "Assets/_Project/Data/Characters/CharacterDatabase.asset");
        if (db != null)
            soCtrl.FindProperty("_characterDatabase").objectReferenceValue = db;

        soCtrl.ApplyModifiedPropertiesWithoutUndo();

        // 5. シーン保存
        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        // 6. EditorBuildSettings にシーンを登録
        //    MainMenu → CharacterSelect → AetherRift_Map の順を維持する
        var buildScenes = new[]
        {
            new EditorBuildSettingsScene("Assets/Scenes/MainMenu.unity",      true),
            new EditorBuildSettingsScene("Assets/Scenes/CharacterSelect.unity", true),
            new EditorBuildSettingsScene("Assets/Scenes/AetherRift_Map.unity", true),
        };
        EditorBuildSettings.scenes = buildScenes;

        Debug.Log("[BuildCharacterSelectScene] CharacterSelect.unity を保存し、ビルド設定を更新しました。");
    }
}
