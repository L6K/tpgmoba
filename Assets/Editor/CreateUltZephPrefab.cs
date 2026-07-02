using Enigma.Vfx;
using UnityEditor;
using UnityEngine;

public static class CreateUltZephPrefab
{
    private const string PrefabPath = "Assets/_Project/Resources/Vfx/Ult/Ult_Zeph.prefab";

    [MenuItem("Enigma/VFX/Create Ult Zeph Prefab")]
    public static void Execute()
    {
        GameObject root = new GameObject("Ult_Zeph");
        root.AddComponent<MeteorUltEffect>();

        EnsureFolder("Assets/_Project", "Resources");
        EnsureFolder("Assets/_Project/Resources", "Vfx");
        EnsureFolder("Assets/_Project/Resources/Vfx", "Ult");

        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Created " + PrefabPath);
    }

    private static void EnsureFolder(string parent, string child)
    {
        string path = parent + "/" + child;
        if (!AssetDatabase.IsValidFolder(path))
            AssetDatabase.CreateFolder(parent, child);
    }
}
