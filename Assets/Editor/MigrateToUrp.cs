using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// HDRP → URP 移行（Hoyoverse 風トゥーン表現のため）。
// 一度だけ実行する想定の使い捨てツール。HDRP パッケージは検証完了後に手動で除去する。
public static class MigrateToUrp
{
    private const string SettingsDir = "Assets/Settings/URP";
    private const string MatDir      = "Assets/_Project/Materials/Map";

    // 半透明のまま URP/Unlit に変換するマテリアル（予兆・インジケーター系）
    private static readonly string[] TransparentUnlit =
        { "Telegraph", "TargetRing", "AoeCircle", "StackMarker", "DirArrow" };

    public static void Execute()
    {
        if (!AssetDatabase.IsValidFolder(SettingsDir))
            AssetDatabase.CreateFolder("Assets/Settings", "URP");

        // 1. URP アセット生成・有効化
        var rendererData = ScriptableObject.CreateInstance<UniversalRendererData>();
        AssetDatabase.CreateAsset(rendererData, SettingsDir + "/EnigmaRenderer.asset");

        var pipeline = UniversalRenderPipelineAsset.Create(rendererData);
        pipeline.supportsHDR = true;
        pipeline.shadowDistance = 90f;
        AssetDatabase.CreateAsset(pipeline, SettingsDir + "/EnigmaURP.asset");

        GraphicsSettings.defaultRenderPipeline = pipeline;
        int prevQuality = QualitySettings.GetQualityLevel();
        for (int i = 0; i < QualitySettings.names.Length; i++)
        {
            QualitySettings.SetQualityLevel(i, false);
            QualitySettings.renderPipeline = pipeline;
        }
        QualitySettings.SetQualityLevel(prevQuality, false);

        // 2. マテリアル変換
        var toon = Shader.Find("Enigma/Toon");
        var unlit = Shader.Find("Universal Render Pipeline/Unlit");
        foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { MatDir }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            string name = Path.GetFileNameWithoutExtension(path);
            Color baseColor = mat.HasProperty("_BaseColor") ? mat.GetColor("_BaseColor") : Color.white;

            if (System.Array.IndexOf(TransparentUnlit, name) >= 0)
            {
                mat.shader = unlit;
                mat.SetColor("_BaseColor", baseColor);
                SetUnlitTransparent(mat);
            }
            else
            {
                mat.shader = toon;
                mat.SetColor("_BaseColor", baseColor);
            }
            EditorUtility.SetDirty(mat);
        }

        // 3. アニメ調スカイボックス
        var sky = AssetDatabase.LoadAssetAtPath<Material>(MatDir + "/AnimeSky.mat");
        if (sky == null)
        {
            sky = new Material(Shader.Find("Skybox/Procedural"));
            AssetDatabase.CreateAsset(sky, MatDir + "/AnimeSky.mat");
        }
        sky.SetColor("_SkyTint", new Color(0.45f, 0.65f, 0.95f));
        sky.SetFloat("_Exposure", 1.25f);
        sky.SetFloat("_AtmosphereThickness", 0.7f);
        EditorUtility.SetDirty(sky);

        // 4. ポスプロ用 VolumeProfile
        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(SettingsDir + "/EnigmaPost.asset");
        if (profile == null)
        {
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, SettingsDir + "/EnigmaPost.asset");
        }
        AddOrGet<Bloom>(profile, b => { b.intensity.Override(0.5f); b.threshold.Override(0.95f); });
        AddOrGet<ColorAdjustments>(profile, c => { c.saturation.Override(12f); c.contrast.Override(8f); });
        AddOrGet<Tonemapping>(profile, t => t.mode.Override(TonemappingMode.Neutral));
        AddOrGet<Vignette>(profile, v => v.intensity.Override(0.18f));
        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssets();

        // 5. シーン修正
        FixScene("Assets/Scenes/AetherRift_Map.unity", sky, profile);
        FixScene("Assets/Scenes/MainMenu.unity", sky, profile);

        AssetDatabase.SaveAssets();
        Debug.Log("[MigrateToUrp] 完了");
    }

    private static void SetUnlitTransparent(Material mat)
    {
        mat.SetFloat("_Surface", 1f);
        mat.SetFloat("_Blend", 0f);
        mat.SetOverrideTag("RenderType", "Transparent");
        mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        mat.SetFloat("_ZWrite", 0f);
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.renderQueue = (int)RenderQueue.Transparent;
    }

    private static void AddOrGet<T>(VolumeProfile profile, System.Action<T> setup)
        where T : VolumeComponent
    {
        if (!profile.TryGet(out T comp))
            comp = profile.Add<T>(true);
        setup(comp);
        comp.active = true;
    }

    private static void FixScene(string scenePath, Material sky, VolumeProfile profile)
    {
        var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

        // HDRP 専用コンポーネントの除去 + ライト/カメラの URP 化
        foreach (var light in Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            RemoveComponent(light.gameObject, "UnityEngine.Rendering.HighDefinition.HDAdditionalLightData");
            if (light.type == LightType.Directional)
            {
                light.intensity = 1.35f;
                light.color = new Color(1f, 0.97f, 0.92f);
            }
        }
        foreach (var cam in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            RemoveComponent(cam.gameObject, "UnityEngine.Rendering.HighDefinition.HDAdditionalCameraData");
            var data = cam.GetComponent<UniversalAdditionalCameraData>();
            if (data == null) data = cam.gameObject.AddComponent<UniversalAdditionalCameraData>();
            data.renderPostProcessing = true;
        }

        // HDRP の空ボリュームを除去
        var oldSky = GameObject.Find("Sky and Fog Volume");
        if (oldSky != null) Object.DestroyImmediate(oldSky);

        // スカイボックス + 環境光
        RenderSettings.skybox = sky;
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor     = new Color(0.62f, 0.72f, 0.88f);
        RenderSettings.ambientEquatorColor = new Color(0.52f, 0.56f, 0.62f);
        RenderSettings.ambientGroundColor  = new Color(0.34f, 0.32f, 0.30f);

        // グローバルポスプロボリューム
        var volumeGo = GameObject.Find("Global Post Volume");
        if (volumeGo == null) volumeGo = new GameObject("Global Post Volume");
        var volume = volumeGo.GetComponent<Volume>();
        if (volume == null) volume = volumeGo.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.sharedProfile = profile;

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    private static void RemoveComponent(GameObject go, string fullTypeName)
    {
        foreach (var comp in go.GetComponents<Component>())
        {
            if (comp != null && comp.GetType().FullName == fullTypeName)
                Object.DestroyImmediate(comp, true);
        }
    }
}
