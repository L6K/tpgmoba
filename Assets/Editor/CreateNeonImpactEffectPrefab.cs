using Enigma.Vfx;
using UnityEditor;
using UnityEngine;

public static class CreateNeonImpactEffectPrefab
{
    private const string PrefabPath = "Assets/_Project/Prefabs/NeonImpactEffect.prefab";

    [MenuItem("Tools/Enigma/VFX/Create Neon Impact Effect Prefab")]
    public static void CreatePrefab()
    {
        GameObject root = new GameObject("NeonImpactEffect");
        var effect = root.AddComponent<NeonImpactEffect>();

        SerializedObject serialized = new SerializedObject(effect);
        SetMaterial(serialized, "ringMaterial", "Assets/_Project/Materials/VFX/Vfx_Impact.mat");
        SetMaterial(serialized, "slashMaterial", "Assets/_Project/Materials/VFX/Vfx_Slash.mat");
        SetMaterial(serialized, "sparkMaterial", "Assets/_Project/Materials/VFX/Vfx_Spark.mat");
        SetMaterial(serialized, "coreMaterial", "Assets/_Project/Materials/VFX/Vfx_Glow.mat");
        serialized.FindProperty("duration").floatValue = 0.72f;
        serialized.FindProperty("playOnEnable").boolValue = true;
        serialized.FindProperty("destroyOnComplete").boolValue = false;
        serialized.FindProperty("primary").colorValue = new Color(0.1f, 0.9f, 1f, 1f);
        serialized.FindProperty("secondary").colorValue = new Color(1f, 0.2f, 0.85f, 1f);
        serialized.FindProperty("radius").floatValue = 2.4f;
        serialized.FindProperty("height").floatValue = 1.4f;
        serialized.FindProperty("sparks").intValue = 18;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        string folder = System.IO.Path.GetDirectoryName(PrefabPath);
        if (!AssetDatabase.IsValidFolder(folder))
            AssetDatabase.CreateFolder("Assets/_Project", "Prefabs");

        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Created " + PrefabPath);
    }

    private static void SetMaterial(SerializedObject serialized, string propertyName, string materialPath)
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        serialized.FindProperty(propertyName).objectReferenceValue = material;
    }
}
