using Enigma.Vfx;
using UnityEditor;
using UnityEngine;

public static class CreateRotatingMagicCircleEffectPrefab
{
    private const string PrefabPath = "Assets/_Project/Prefabs/RotatingMagicCircleEffect.prefab";

    [MenuItem("Tools/Enigma/VFX/Create Rotating Magic Circle Prefab")]
    public static void CreatePrefab()
    {
        GameObject root = new GameObject("RotatingMagicCircleEffect");
        var effect = root.AddComponent<RotatingMagicCircleEffect>();

        SerializedObject serialized = new SerializedObject(effect);
        SetMaterial(serialized, "ringMaterial", "Assets/_Project/Materials/VFX/Vfx_Glow.mat");
        SetMaterial(serialized, "glyphMaterial", "Assets/_Project/Materials/VFX/Vfx_Slash.mat");
        SetMaterial(serialized, "sparkMaterial", "Assets/_Project/Materials/VFX/Vfx_Spark.mat");
        serialized.FindProperty("playOnEnable").boolValue = true;
        serialized.FindProperty("loop").boolValue = true;
        serialized.FindProperty("fadeInSeconds").floatValue = 0.45f;
        serialized.FindProperty("fadeOutSeconds").floatValue = 0.35f;
        serialized.FindProperty("lifeSeconds").floatValue = 4.5f;
        serialized.FindProperty("coreColor").colorValue = new Color(1f, 0.08f, 0.02f, 1f);
        serialized.FindProperty("edgeColor").colorValue = new Color(1f, 0.32f, 0.08f, 1f);
        serialized.FindProperty("radius").floatValue = 3.15f;
        serialized.FindProperty("rotationSpeed").floatValue = 16f;
        serialized.FindProperty("pulseSpeed").floatValue = 1.1f;
        serialized.FindProperty("runeCount").intValue = 14;
        serialized.FindProperty("emberCount").intValue = 28;
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
