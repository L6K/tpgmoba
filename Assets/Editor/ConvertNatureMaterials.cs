using UnityEditor;
using UnityEngine;

/// <summary>
/// Assets/External/Nature 配下の高品質樹木 FBX 用に Enigma/Toon マテリアルを生成する
/// エディタユーティリティ。ConvertKenneyMaterials の流儀（Enigma/Toon + 鳴潮風ランプ）を踏襲する。
///
/// ConvertKenney は FBX 埋め込みマテリアル名に依存する SearchAndRemapMaterials を使うが、
/// ここで扱う FBX は埋め込みトゥーンマテリアル名が不定で、かつ葉は 3 トーンの色バリエーションを
/// インスタンス単位で振り分ける必要がある。そのため FBX へリマップせず、命名済みの .mat を生成し、
/// BuildAetherRiftMap 側で renderer.sharedMaterials を名前ベースで差し替える方式を採る。
///
/// 命名規約（BuildAetherRiftMap.PlaceOneNatureTree が参照）:
///   幹: Nature_{Species}_Bark
///   葉: Nature_{Species}_Leaf_0 / _1 / _2  （0=標準, 1=黄緑, 2=深緑）
/// </summary>
public static class ConvertNatureMaterials
{
    private const string TexDir   = "Assets/External/Nature/Textures";
    private const string MatOutDir = "Assets/External/Nature/Materials";

    // 鳴潮風: 柔らかいランプ + 青みの影（ConvertKenney と統一）
    private const float RampSmoothing = 0.18f;
    private static readonly Color ShadeColor = new Color(0.58f, 0.62f, 0.80f, 1f);

    // 葉の 3 トーン（_BaseColor 乗算）。0=標準 / 1=黄緑 / 2=深緑
    private static readonly Color[] LeafTones =
    {
        Color.white,
        new Color(1.05f, 1.08f, 0.92f, 1f),
        new Color(0.88f, 0.96f, 0.88f, 1f),
    };

    [MenuItem("Enigma/Convert Nature Materials")]
    public static void Execute()
    {
        if (!AssetDatabase.IsValidFolder(MatOutDir))
            AssetDatabase.CreateFolder("Assets/External/Nature", "Materials");

        var shader = Shader.Find("Enigma/Toon")
                  ?? Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
        {
            Debug.LogError("[ConvertNatureMaterials] Enigma/Toon シェーダーが見つかりません。処理を中止します。");
            return;
        }

        // FBX のファイル単位(cm系)とインポートスケールの不整合で樹高が極小になる事故を防ぐ。
        // ファイルスケールを無視して 1unit=1m 固定にし、実寸はビルダー側の正規化スケールに任せる
        NormalizeModelImportScale();

        int count = 0;

        // --- テクスチャ有りの樹種 ---
        // Tree_1: 幹 Tree_Bark.jpg / 葉 Tree_Leaves.png
        count += CreateBark(shader, "Tree", "Tree_Bark.jpg");
        count += CreateLeafTones(shader, "Tree", "Tree_Leaves.png");

        // Birch_1: 幹 Birch_Bark.png / 葉 Birch_Leaves_Green.png
        count += CreateBark(shader, "Birch", "Birch_Bark.png");
        count += CreateLeafTones(shader, "Birch", "Birch_Leaves_Green.png");

        // Pine_1: 葉のみ Pine_Leaves.png（幹テクスチャ無し → フラット幹色）
        count += CreateFlatBark(shader, "Pine", new Color(0.40f, 0.30f, 0.22f));
        count += CreateLeafTones(shader, "Pine", "Pine_Leaves.png");

        // TreeToonStylized01: 幹葉とも TreeToonStylized_Diffuse.png（単一拡散）
        count += CreateBark(shader, "TreeToon", "TreeToonStylized_Diffuse.png");
        count += CreateLeafTones(shader, "TreeToon", "TreeToonStylized_Diffuse.png");

        // DeadTree_1: 枯木。テクスチャ無し → 灰褐色フラット幹のみ（葉なし）
        count += CreateFlatBark(shader, "DeadTree", new Color(0.42f, 0.36f, 0.30f));

        // --- テクスチャ無し（SinuousToonTree_SeparateParts）: フラット色 ---
        count += CreateFlatBark(shader, "Sinuous", new Color(0.45f, 0.33f, 0.25f));
        count += CreateFlatLeafTones(shader, "Sinuous", new Color(0.32f, 0.55f, 0.30f));

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[ConvertNatureMaterials] 完了: {count} マテリアルを生成/更新しました。");
    }

    /// <summary>
    /// Nature FBX 群の ModelImporter を 1unit=1m に統一する。
    /// useFileScale=true のままだと cm 系ファイルが 1/100 サイズで入り、
    /// ビルド時と再インポート後で bounds が食い違って樹高が極小になる。
    /// </summary>
    private static void NormalizeModelImportScale()
    {
        string[] fbxFiles =
        {
            "Tree_1", "Birch_1", "Pine_1", "TreeToonStylized01",
            "DeadTree_1", "SinuousToonTree", "SinuousToonTree_SeparateParts",
        };
        foreach (var name in fbxFiles)
        {
            var path     = $"Assets/External/Nature/{name}.fbx";
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null) continue;
            if (!importer.useFileScale && Mathf.Approximately(importer.globalScale, 1f)) continue;
            importer.useFileScale = false;
            importer.globalScale  = 1f;
            importer.SaveAndReimport();
        }
    }

    /// <summary>幹マテリアル（_Cutoff 0、テクスチャ付き）を生成する。</summary>
    private static int CreateBark(Shader shader, string species, string texFile)
    {
        var tex = LoadTex(texFile);
        var mat = MakeMaterial(shader, Color.white, tex, cutoff: 0f);
        Save(mat, $"Nature_{species}_Bark");
        return 1;
    }

    /// <summary>幹マテリアル（フラット色、_Cutoff 0）を生成する。</summary>
    private static int CreateFlatBark(Shader shader, string species, Color color)
    {
        var mat = MakeMaterial(shader, color, null, cutoff: 0f);
        Save(mat, $"Nature_{species}_Bark");
        return 1;
    }

    /// <summary>葉マテリアル 3 トーン（_Cutoff 0.5 でリーフカードのアルファ抜き、テクスチャ付き）を生成する。</summary>
    private static int CreateLeafTones(Shader shader, string species, string texFile)
    {
        // リーフカードは単面ポリゴンのため、輪郭線パス付きの Enigma/Toon だと
        // 裏面が黒塗りになる。両面・輪郭線なしの専用シェーダーを使う
        var leafShader = Shader.Find("Enigma/ToonLeaf") ?? shader;
        var tex = LoadTex(texFile);
        for (int i = 0; i < LeafTones.Length; i++)
        {
            var mat = MakeMaterial(leafShader, LeafTones[i], tex, cutoff: 0.5f);
            Save(mat, $"Nature_{species}_Leaf_{i}");
        }
        return LeafTones.Length;
    }

    /// <summary>葉マテリアル 3 トーン（フラット色、_Cutoff 0）を生成する。テクスチャ無し樹種用。</summary>
    private static int CreateFlatLeafTones(Shader shader, string species, Color baseColor)
    {
        for (int i = 0; i < LeafTones.Length; i++)
        {
            // フラット葉はアルファ抜き不要なので _Cutoff 0。トーンは baseColor へ乗算。
            var tone = new Color(
                baseColor.r * LeafTones[i].r,
                baseColor.g * LeafTones[i].g,
                baseColor.b * LeafTones[i].b, 1f);
            var mat = MakeMaterial(shader, tone, null, cutoff: 0f);
            Save(mat, $"Nature_{species}_Leaf_{i}");
        }
        return LeafTones.Length;
    }

    private static Material MakeMaterial(Shader shader, Color baseColor, Texture tex, float cutoff)
    {
        var mat = new Material(shader);
        mat.SetColor("_BaseColor", baseColor);
        if (tex != null && mat.HasProperty("_BaseMap"))
            mat.SetTexture("_BaseMap", tex);
        if (mat.HasProperty("_RampSmoothing"))
            mat.SetFloat("_RampSmoothing", RampSmoothing);
        if (mat.HasProperty("_ShadeColor"))
            mat.SetColor("_ShadeColor", ShadeColor);
        if (mat.HasProperty("_Cutoff"))
            mat.SetFloat("_Cutoff", cutoff);
        if (cutoff > 0f)
        {
            // アルファ抜き（リーフカード）: AlphaTest を有効化。Enigma/Toon の _Cutoff キーワード前提。
            mat.EnableKeyword("_ALPHATEST_ON");
            mat.SetOverrideTag("RenderType", "TransparentCutout");
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
        }
        return mat;
    }

    private static Texture2D LoadTex(string fileName)
    {
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexDir}/{fileName}");
        if (tex == null)
            Debug.LogWarning($"[ConvertNatureMaterials] テクスチャが見つかりません: {TexDir}/{fileName}");
        return tex;
    }

    private static void Save(Material mat, string name)
    {
        var path = $"{MatOutDir}/{name}.mat";
        var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null)
        {
            existing.shader = mat.shader;
            existing.CopyPropertiesFromMaterial(mat);
            EditorUtility.SetDirty(existing);
            Object.DestroyImmediate(mat);
            return;
        }
        AssetDatabase.CreateAsset(mat, path);
    }
}
