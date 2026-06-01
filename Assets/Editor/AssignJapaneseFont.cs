using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;

public class AssignJapaneseFont
{
    const string FontAssetPath = "Assets/_Project/Fonts/NotoSansJP_SDF.asset";
    const string ScenePath = "Assets/Scenes/MainMenu.unity";

    public static void Execute()
    {
        var fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
        if (fontAsset == null)
        {
            Debug.LogError("[Enigma] Font asset not found at: " + FontAssetPath);
            return;
        }

        // シーンを開く
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        // シーン内の全 TextMeshProUGUI を取得して SerializedProperty 経由でフォントを割り当て
        int count = 0;
        var roots = scene.GetRootGameObjects();
        foreach (var root in roots)
        {
            foreach (var tmp in root.GetComponentsInChildren<TextMeshProUGUI>(true))
            {
                var so = new SerializedObject(tmp);
                var fontProp = so.FindProperty("m_fontAsset");
                if (fontProp != null)
                {
                    fontProp.objectReferenceValue = fontAsset;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    count++;
                }
            }
        }

        EditorSceneManager.SaveScene(scene);
        Debug.Log($"[Enigma] Assigned Japanese font to {count} TMP components.");
    }
}
