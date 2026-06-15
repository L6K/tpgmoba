using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// VFX 仮テクスチャ(手続き生成)の import 設定を整え、URP/Unlit 加算マテリアルを生成する。
/// Unity を閉じた状態で -executeMethod BuildVfxMaterials.Execute から実行する(batch)。
/// 後で 5.5 の個別生成テクスチャに差し替えても、同名なら同マテリアルがそのまま使える。
/// </summary>
public static class BuildVfxMaterials
{
    private const string TexDir = "Assets/_Project/VFX/Textures";
    private const string MatDir = "Assets/_Project/Materials/VFX";

    // (テクスチャ名, sRGBか, タイル(Repeat)か)
    private static readonly (string tex, bool srgb, bool repeat)[] Imports =
    {
        ("glow_dot",             true,  false),
        ("spark_streak",         true,  false),
        ("beam_core_gradient",   true,  false),
        ("impact_burst_flipbook",true,  false),
        ("ring_shock_flipbook",  true,  false),
        ("slash_arc",            true,  false),
        ("hit_flash_radial",     true,  false),
        ("zeph_circuit_mask",    false, true),
        ("veil_smoke_wisp",      true,  false),
        ("rune_circle_arcane",   true,  false),
        ("neon_trim_strip",      true,  true),
        ("hex_panel_emissive",   false, true),
        ("energy_flow_strip",    true,  true),
        ("objective_core_glow",  true,  false),
        ("soft_noise_tile",      false, true),
    };

    // (マテリアル名, 元テクスチャ名)
    private static readonly (string mat, string tex)[] Materials =
    {
        ("Vfx_Glow",     "glow_dot"),
        ("Vfx_Spark",    "spark_streak"),
        ("Vfx_Beam",     "beam_core_gradient"),
        ("Vfx_Impact",   "impact_burst_flipbook"),
        ("Vfx_Slash",    "slash_arc"),
        ("Vfx_Core",     "objective_core_glow"),
        ("Vfx_NeonTrim", "neon_trim_strip"),
        ("Vfx_Hex",      "hex_panel_emissive"),
    };

    public static void Execute()
    {
        if (!AssetDatabase.IsValidFolder(MatDir))
        {
            AssetDatabase.CreateFolder("Assets/_Project/Materials", "VFX");
        }

        // 1. テクスチャ import 設定
        foreach (var (tex, srgb, repeat) in Imports)
        {
            string path = $"{TexDir}/{tex}.png";
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) { Debug.LogWarning($"[BuildVfxMaterials] importer 無し: {path}"); continue; }
            importer.textureType        = TextureImporterType.Default;
            importer.alphaIsTransparency = true;
            importer.sRGBTexture        = srgb;
            importer.wrapMode           = repeat ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
            importer.mipmapEnabled      = true;
            importer.SaveAndReimport();
        }

        // 2. URP/Unlit 加算マテリアル生成
        var shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) { Debug.LogError("[BuildVfxMaterials] URP/Unlit シェーダーが見つかりません"); return; }

        int made = 0;
        foreach (var (matName, texName) in Materials)
        {
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexDir}/{texName}.png");
            if (tex == null) { Debug.LogWarning($"[BuildVfxMaterials] テクスチャ無し: {texName}"); continue; }

            string matPath = $"{MatDir}/{matName}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null) { mat = new Material(shader); AssetDatabase.CreateAsset(mat, matPath); }
            else mat.shader = shader;

            ConfigureAdditive(mat, tex);
            EditorUtility.SetDirty(mat);
            made++;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[BuildVfxMaterials] テクスチャ {Imports.Length} 件設定 / マテリアル {made} 件生成しました。");
    }

    // URP/Unlit を透明・加算(One/One)・ZWrite オフに設定し、白基調の BaseMap を割り当てる。
    private static void ConfigureAdditive(Material mat, Texture2D tex)
    {
        mat.SetTexture("_BaseMap", tex);
        mat.SetColor("_BaseColor", Color.white);
        mat.SetFloat("_Surface", 1f); // Transparent
        mat.SetFloat("_Blend", 2f);   // Additive
        mat.SetFloat("_SrcBlend", (float)BlendMode.One);
        mat.SetFloat("_DstBlend", (float)BlendMode.One);
        mat.SetFloat("_ZWrite", 0f);
        mat.SetOverrideTag("RenderType", "Transparent");
        mat.renderQueue = (int)RenderQueue.Transparent;
        mat.DisableKeyword("_SURFACE_TYPE_OPAQUE");
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.EnableKeyword("_ALPHAPREMULTIPLY_ON");
    }
}
