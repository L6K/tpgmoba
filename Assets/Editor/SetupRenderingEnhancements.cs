using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Enigma.EditorTools
{
    /// <summary>
    /// 鳴潮風の絵作りに必要なレンダリング拡張をアセットへ一括適用する一回実行用ユーティリティ。
    ///   1. EnigmaRenderer.asset に SSAO レンダラーフィーチャを内包追加（冪等）
    ///   2. EnigmaPost.asset の VolumeProfile に ColorAdjustments / Vignette を追加
    /// 呼び出し元（Unity エディタ）が <see cref="Execute"/> を実行する想定。
    /// </summary>
    public static class SetupRenderingEnhancements
    {
        private const string RendererPath = "Assets/Settings/URP/EnigmaRenderer.asset";
        private const string PostPath     = "Assets/Settings/URP/EnigmaPost.asset";
        private const string SsaoTypeName =
            "UnityEngine.Rendering.Universal.ScreenSpaceAmbientOcclusion, Unity.RenderPipelines.Universal.Runtime";

        public static void Execute()
        {
            AddSsaoFeature();
            SetupPostProcessing();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[SetupRenderingEnhancements] 完了: SSAO + ポスプロを適用しました。");
        }

        // ---------------------------------------------------------------
        // 1. SSAO レンダラーフィーチャ
        // ---------------------------------------------------------------

        private static void AddSsaoFeature()
        {
            var rendererData = AssetDatabase.LoadAssetAtPath<ScriptableObject>(RendererPath);
            if (rendererData == null)
            {
                Debug.LogError($"[SetupRenderingEnhancements] {RendererPath} が読み込めません。");
                return;
            }

            // 既に "SSAO" があれば冪等に終了
            foreach (var sub in AssetDatabase.LoadAllAssetRepresentationsAtPath(RendererPath))
            {
                if (sub != null && sub.name == "SSAO")
                {
                    Debug.Log("[SetupRenderingEnhancements] SSAO は既に存在します。スキップ。");
                    return;
                }
            }

            // SSAO 型は URP 17 では internal。リフレクションで解決して生成する。
            var ssaoType = Type.GetType(SsaoTypeName);
            if (ssaoType == null)
            {
                Debug.LogError($"[SetupRenderingEnhancements] SSAO 型を解決できません: {SsaoTypeName}");
                return;
            }

            var ssao = ScriptableObject.CreateInstance(ssaoType);
            ssao.name = "SSAO";

            ConfigureSsaoSettings(ssao);

            // サブアセットとして EnigmaRenderer.asset に内包
            ssao.hideFlags = HideFlags.HideInHierarchy;
            AssetDatabase.AddObjectToAsset(ssao, rendererData);
            AssetDatabase.SaveAssets();

            // localId を取得して m_RendererFeatureMap と整合させる
            if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(ssao, out _, out long localId))
            {
                Debug.LogError("[SetupRenderingEnhancements] SSAO の localId を取得できませんでした。");
                return;
            }

            var so = new SerializedObject(rendererData);
            var featuresProp = so.FindProperty("m_RendererFeatures");
            var mapProp      = so.FindProperty("m_RendererFeatureMap");

            int idx = featuresProp.arraySize;
            featuresProp.InsertArrayElementAtIndex(idx);
            featuresProp.GetArrayElementAtIndex(idx).objectReferenceValue = ssao;

            // m_RendererFeatureMap は各フィーチャの localId を並べた long 配列
            mapProp.InsertArrayElementAtIndex(idx);
            mapProp.GetArrayElementAtIndex(idx).longValue = localId;

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(rendererData);

            Debug.Log($"[SetupRenderingEnhancements] SSAO を追加しました (localId={localId})。");
        }

        /// <summary>
        /// SSAO の public settings フィールドを設定する。
        /// Intensity 1.5 / Radius 0.5 / Downsample true / Source DepthNormals。
        /// settings の型・メンバ名はリフレクションで解決し、無い場合は黙ってスキップする。
        /// </summary>
        private static void ConfigureSsaoSettings(ScriptableObject ssao)
        {
            var settingsField = ssao.GetType().GetField(
                "settings", BindingFlags.Public | BindingFlags.Instance);
            if (settingsField == null) return;

            object settings = settingsField.GetValue(ssao);
            if (settings == null) return;
            var st = settings.GetType();

            SetField(settings, st, "Intensity", 1.5f);
            SetField(settings, st, "Radius", 0.5f);
            SetField(settings, st, "Downsample", true);

            // Source は enum (DepthSource)。DepthNormals が選べれば選ぶ。
            var sourceField = st.GetField("Source", BindingFlags.Public | BindingFlags.Instance);
            if (sourceField != null && sourceField.FieldType.IsEnum)
            {
                foreach (var nm in Enum.GetNames(sourceField.FieldType))
                {
                    if (nm == "DepthNormals")
                    {
                        sourceField.SetValue(settings,
                            Enum.Parse(sourceField.FieldType, nm));
                        break;
                    }
                }
            }

            // 値型 struct の場合はフィールドへ書き戻す
            if (settingsField.FieldType.IsValueType)
                settingsField.SetValue(ssao, settings);
        }

        private static void SetField(object target, Type t, string name, object value)
        {
            var f = t.GetField(name, BindingFlags.Public | BindingFlags.Instance);
            if (f != null) f.SetValue(target, value);
        }

        // ---------------------------------------------------------------
        // 2. ポストプロセス（VolumeProfile）
        // ---------------------------------------------------------------

        private static void SetupPostProcessing()
        {
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(PostPath);
            if (profile == null)
            {
                Debug.LogError($"[SetupRenderingEnhancements] {PostPath} が読み込めません。");
                return;
            }

            ConfigureColorAdjustments(profile);
            ConfigureVignette(profile);

            EditorUtility.SetDirty(profile);
        }

        private static void ConfigureColorAdjustments(VolumeProfile profile)
        {
            var type = Type.GetType(
                "UnityEngine.Rendering.Universal.ColorAdjustments, Unity.RenderPipelines.Universal.Runtime");
            if (type == null) return;

            if (!profile.TryGet(type, out VolumeComponent comp))
                comp = profile.Add(type, true);

            // contrast +8, saturation +8
            SetVolumeFloat(comp, "contrast", 8f);
            SetVolumeFloat(comp, "saturation", 8f);
            EditorUtility.SetDirty(comp);
        }

        private static void ConfigureVignette(VolumeProfile profile)
        {
            var type = Type.GetType(
                "UnityEngine.Rendering.Universal.Vignette, Unity.RenderPipelines.Universal.Runtime");
            if (type == null) return;

            if (!profile.TryGet(type, out VolumeComponent comp))
                comp = profile.Add(type, true);

            SetVolumeFloat(comp, "intensity", 0.18f);
            SetVolumeFloat(comp, "smoothness", 0.4f);
            EditorUtility.SetDirty(comp);
        }

        /// <summary>
        /// VolumeComponent の VolumeParameter&lt;float&gt; フィールドへ値を設定し overrideState を立てる。
        /// </summary>
        private static void SetVolumeFloat(VolumeComponent comp, string fieldName, float value)
        {
            var field = comp.GetType().GetField(fieldName,
                BindingFlags.Public | BindingFlags.Instance);
            if (field == null) return;

            var param = field.GetValue(comp);
            if (param == null) return;
            var pType = param.GetType();

            var valueProp = pType.GetProperty("value",
                BindingFlags.Public | BindingFlags.Instance);
            if (valueProp != null && valueProp.CanWrite)
                valueProp.SetValue(param, value);

            var overrideField = pType.GetField("overrideState",
                BindingFlags.Public | BindingFlags.Instance);
            if (overrideField != null)
                overrideField.SetValue(param, true);
        }
    }
}
