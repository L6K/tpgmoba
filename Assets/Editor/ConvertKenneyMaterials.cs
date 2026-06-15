using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Assets/External/Kenney 配下の全 FBX モデルに埋め込まれた Standard マテリアルを
/// Enigma/Toon シェーダーのマテリアルに置換するエディタユーティリティ。
/// BuildAetherRiftMap.Execute() の前に手動で実行すること。
/// </summary>
public static class ConvertKenneyMaterials
{
    private const string KenneyRoot  = "Assets/External/Kenney";
    private const string MatOutDir   = "Assets/External/Kenney/Materials";

    [MenuItem("Enigma/Convert Kenney Materials")]
    public static void Execute()
    {
        // 出力ディレクトリを確保
        if (!AssetDatabase.IsValidFolder(MatOutDir))
            AssetDatabase.CreateFolder("Assets/External/Kenney", "Materials");

        var toonShader = Shader.Find("Enigma/Toon")
                      ?? Shader.Find("Universal Render Pipeline/Lit");
        if (toonShader == null)
        {
            Debug.LogError("[ConvertKenneyMaterials] Enigma/Toon シェーダーが見つかりません。処理を中止します。");
            return;
        }

        // Kenney 配下の全モデルアセットを検索
        var guids = AssetDatabase.FindAssets("t:Model", new[] { KenneyRoot });
        int processedMats = 0;

        // モデルごとに処理（同名マテリアルはキャッシュして重複生成を防ぐ）
        var createdMaterials = new Dictionary<string, Material>();

        foreach (var guid in guids)
        {
            var assetPath = AssetDatabase.GUIDToAssetPath(guid);

            // モデルに埋め込まれたサブアセットのマテリアルを列挙
            var allAssets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
            foreach (var asset in allAssets)
            {
                if (asset is not Material srcMat) continue;

                string matName = srcMat.name;
                if (createdMaterials.ContainsKey(matName)) continue;

                var outPath = $"{MatOutDir}/{matName}.mat";
                var existing = AssetDatabase.LoadAssetAtPath<Material>(outPath);
                if (existing != null)
                {
                    createdMaterials[matName] = existing;
                    continue;
                }

                // 元マテリアルから色・テクスチャを引き継ぐ
                Color baseColor = Color.white;
                if (srcMat.HasProperty("_BaseColor"))
                    baseColor = srcMat.GetColor("_BaseColor");
                else if (srcMat.HasProperty("_Color"))
                    baseColor = srcMat.GetColor("_Color");

                Texture baseMap = null;
                if (srcMat.HasProperty("_BaseMap"))
                    baseMap = srcMat.GetTexture("_BaseMap");
                else if (srcMat.HasProperty("_MainTex"))
                    baseMap = srcMat.GetTexture("_MainTex");

                var newMat = new Material(toonShader);
                newMat.SetColor("_BaseColor", baseColor);
                if (baseMap != null)
                    newMat.SetTexture("_BaseMap", baseMap);
                if (newMat.HasProperty("_OutlineWidth"))
                    newMat.SetFloat("_OutlineWidth", 0.002f);
                // 鳴潮風: 柔らかいランプ + 青みの影（地面・木・岩系と統一）
                if (newMat.HasProperty("_RampSmoothing"))
                    newMat.SetFloat("_RampSmoothing", 0.18f);
                if (newMat.HasProperty("_ShadeColor"))
                    newMat.SetColor("_ShadeColor", new Color(0.58f, 0.62f, 0.80f, 1f));

                AssetDatabase.CreateAsset(newMat, outPath);
                createdMaterials[matName] = newMat;
                processedMats++;
            }

            // SearchAndRemap: 名前一致で新マテリアルをモデルに刺す
            var importer = AssetImporter.GetAtPath(assetPath) as ModelImporter;
            if (importer == null) continue;

            importer.SearchAndRemapMaterials(
                ModelImporterMaterialName.BasedOnMaterialName,
                ModelImporterMaterialSearch.Everywhere);
            importer.SaveAndReimport();
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[ConvertKenneyMaterials] 完了: {processedMats} マテリアルを生成/更新しました。");
    }
}
