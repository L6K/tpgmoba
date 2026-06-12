using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Enigma.UI;

namespace Enigma.EditorTools
{
    /// <summary>
    /// HUD 用テクスチャ（ミニマップ地形・ポートレート・各種グラデーション）を
    /// シーンからベイクして Assets/_Project/UI/Textures/ に PNG 出力する。
    /// 呼び出し元が AetherRift_Map.unity を開いた状態で Execute() を実行する想定。
    /// </summary>
    public static class BakeUiAssets
    {
        private const string OutputDir = "Assets/_Project/UI/Textures";

        // デザイン言語
        private const string ColPanelTop    = "#131A2A";
        private const string ColPanelBottom = "#0A0E16";
        private const string ColMapBg       = "#0A0E1A";
        private const string ColHpTop       = "#34D567";
        private const string ColHpBottom    = "#15803D";
        private const string ColHpShine     = "#A8F0BF";
        private const string ColXpTop       = "#A06BF0";
        private const string ColXpBottom    = "#6D3BBF";
        private const string ColGold        = "#C8AA6E";

        public static void Execute()
        {
            EnsureOutputDir();

            BakeMinimapBg();
            BakePortrait();
            BakeGradients();
            BakeGroundNoise();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[BakeUiAssets] 完了: " + OutputDir);
        }

        // ---------------------------------------------------------------
        // 1a. ミニマップ地形
        // ---------------------------------------------------------------

        private static void BakeMinimapBg()
        {
            const int Size = 1024;
            // WorldBounds ±75 と一致。カメラは真上から見下ろす。
            const float OrthoSize = 75f;

            var camGo = new GameObject("__BakeMinimapCam");
            var hidden = new List<GameObject>();
            RenderTexture rt = null;
            Texture2D tex = null;
            RenderTexture prevActive = RenderTexture.active;
            bool prevFog = RenderSettings.fog;

            try
            {
                // 真上 120m からの撮影は距離フォグで全体が霞むため、ベイク中のみ無効化
                RenderSettings.fog = false;

                // 映り込み除去: HealthBar / Telegraph を一時非表示
                HideObjects(hidden, go =>
                    go.name == "HealthBar" || go.name.Contains("Telegraph"));

                var cam = camGo.AddComponent<Camera>();
                camGo.transform.position = new Vector3(0f, 120f, 0f);
                // rotation(90,0,0): forward=-Y(見下ろし), up=+Z, right=+X
                // → 画像 上=+Z, 右=+X（ミニマップ UI と一致）
                camGo.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                cam.orthographic = true;
                cam.orthographicSize = OrthoSize;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = Opaque(GradientBaker.Hex(ColMapBg));
                cam.nearClipPlane = 0.1f;
                cam.farClipPlane = 400f;
                cam.allowHDR = false;
                cam.allowMSAA = false;

                var urp = camGo.AddComponent<UniversalAdditionalCameraData>();
                urp.renderPostProcessing = false;
                urp.antialiasing = AntialiasingMode.None;

                rt = new RenderTexture(Size, Size, 24, RenderTextureFormat.ARGB32)
                {
                    antiAliasing = 1
                };
                cam.targetTexture = rt;
                cam.Render();

                RenderTexture.active = rt;
                tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
                tex.ReadPixels(new Rect(0, 0, Size, Size), 0, 0);
                tex.Apply();

                WritePng(tex, "MinimapBg.png");
            }
            finally
            {
                RenderSettings.fog = prevFog;
                RenderTexture.active = prevActive;
                RestoreObjects(hidden);
                if (rt != null)
                {
                    rt.Release();
                    Object.DestroyImmediate(rt);
                }
                if (tex != null) Object.DestroyImmediate(tex);
                Object.DestroyImmediate(camGo);
            }

            ImportTexture("MinimapBg.png", 1024, hasAlpha: false);
        }

        // ---------------------------------------------------------------
        // 1b. ポートレート
        // ---------------------------------------------------------------

        private static void BakePortrait()
        {
            const int Size = 256;

            var player = GameObject.FindWithTag("Player");
            if (player == null)
            {
                Debug.LogWarning("[BakeUiAssets] Player が見つからないためポートレートをスキップ");
                return;
            }

            Transform model = FindChildByName(player.transform, "UnityChanModel");
            if (model == null)
            {
                Debug.LogWarning("[BakeUiAssets] UnityChanModel が無いためポートレートをスキップ");
                return;
            }

            var camGo = new GameObject("__BakePortraitCam");
            RenderTexture rt = null;
            Texture2D tex = null;
            RenderTexture prevActive = RenderTexture.active;
            var hidden = new List<GameObject>();

            try
            {
                // スポーンがベースポケット壁の至近にあるため、壁が写り込まないよう一時非表示
                HideObjects(hidden, go => go.name == "OuterBoundary" || go.name == "HealthBar");

                Vector3 playerPos = player.transform.position;
                Vector3 f = player.transform.forward;

                var cam = camGo.AddComponent<Camera>();
                camGo.transform.position = playerPos + f * 1.1f + Vector3.up * 0.42f;
                camGo.transform.LookAt(playerPos + Vector3.up * 0.40f);
                cam.fieldOfView = 22f;
                cam.orthographic = false;
                cam.clearFlags = CameraClearFlags.SolidColor;
                // alpha 0 背景（切り抜き用）
                cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
                cam.nearClipPlane = 0.05f;
                cam.farClipPlane = 10f;
                cam.allowHDR = false;
                cam.allowMSAA = false;

                var urp = camGo.AddComponent<UniversalAdditionalCameraData>();
                urp.renderPostProcessing = false;
                urp.antialiasing = AntialiasingMode.None;

                rt = new RenderTexture(Size, Size, 24, RenderTextureFormat.ARGB32)
                {
                    antiAliasing = 1
                };
                cam.targetTexture = rt;
                cam.Render();

                RenderTexture.active = rt;
                tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
                tex.ReadPixels(new Rect(0, 0, Size, Size), 0, 0);
                tex.Apply();

                WritePng(tex, "PortraitZeph.png");
            }
            finally
            {
                RestoreObjects(hidden);
                RenderTexture.active = prevActive;
                if (rt != null)
                {
                    rt.Release();
                    Object.DestroyImmediate(rt);
                }
                if (tex != null) Object.DestroyImmediate(tex);
                Object.DestroyImmediate(camGo);
            }

            ImportTexture("PortraitZeph.png", 256, hasAlpha: true);
        }

        // ---------------------------------------------------------------
        // 1c. 手続きグラデーション群
        // ---------------------------------------------------------------

        private static void BakeGradients()
        {
            // PanelGradient 8x256 縦
            WriteGenerated("PanelGradient.png", 8, 256, false,
                GradientBaker.VerticalGradient(8, 256,
                    GradientBaker.Hex(ColPanelTop), GradientBaker.Hex(ColPanelBottom)));

            // HpFillGradient 8x32 縦 + 最上 2px シャイン
            WriteGenerated("HpFillGradient.png", 8, 32, false,
                GradientBaker.HpFillGradient(8, 32,
                    GradientBaker.Hex(ColHpTop), GradientBaker.Hex(ColHpBottom),
                    GradientBaker.Hex(ColHpShine), 2));

            // XpFillGradient 8x16 縦
            WriteGenerated("XpFillGradient.png", 8, 16, false,
                GradientBaker.VerticalGradient(8, 16,
                    GradientBaker.Hex(ColXpTop), GradientBaker.Hex(ColXpBottom)));

            // GoldTrim 256x8 横（両端 alpha0 → 中央金）
            WriteGenerated("GoldTrim.png", 256, 8, true,
                GradientBaker.HorizontalCenterGlow(256, 8, Opaque(GradientBaker.Hex(ColGold))));

            // RadialGlow 128x128（中心 alpha0.5 → 外周0）
            var glowCenter = GradientBaker.Hex(ColGold);
            glowCenter.a = 0.5f;
            WriteGenerated("RadialGlow.png", 128, 128, true,
                GradientBaker.RadialGlow(128, glowCenter));

            // MinimapArrow 32x32 上向き白三角（透明背景）
            WriteGenerated("MinimapArrow.png", 32, 32, true,
                GradientBaker.UpTriangle(32, Color.white));

            // GrassBlade 128x128 草むらタフト用リーフカード（透明背景、wrap=Clamp）
            WriteGenerated("GrassBlade.png", 128, 128, true,
                GradientBaker.GrassBladeTexture(128));
        }

        // ---------------------------------------------------------------
        // 1d. 地面の色むらノイズ（タイル可能、wrap=Repeat）
        // ---------------------------------------------------------------

        private static void BakeGroundNoise()
        {
            const int Size = 512;

            // 草の明暗: ベース 1.0 に対し ±数% の倍率むら（マテリアル _BaseColor へ乗算される想定）
            WriteRepeatTexture("GroundNoise.png", Size,
                GradientBaker.ValueNoiseTexture(Size,
                    new Color(0.93f, 0.97f, 0.90f), new Color(1.06f, 1.04f, 0.97f),
                    cells: 8, octaves: 2, seed: 1001));

            // レーン土の明暗
            WriteRepeatTexture("LaneNoise.png", Size,
                GradientBaker.ValueNoiseTexture(Size,
                    new Color(0.94f, 0.92f, 0.90f), new Color(1.05f, 1.03f, 1.0f),
                    cells: 8, octaves: 2, seed: 2002));
        }

        private static void WriteRepeatTexture(string fileName, int size, Color[] pixels)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.SetPixels(pixels);
            tex.Apply();
            try
            {
                WritePng(tex, fileName);
            }
            finally
            {
                Object.DestroyImmediate(tex);
            }
            ImportTexture(fileName, NextPow2AtLeast(size), hasAlpha: false, wrap: TextureWrapMode.Repeat);
        }

        // ---------------------------------------------------------------
        // ヘルパー
        // ---------------------------------------------------------------

        private static void WriteGenerated(string fileName, int width, int height, bool hasAlpha, Color[] pixels)
        {
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            tex.SetPixels(pixels);
            tex.Apply();
            try
            {
                WritePng(tex, fileName);
            }
            finally
            {
                Object.DestroyImmediate(tex);
            }
            int max = Mathf.Max(width, height);
            ImportTexture(fileName, NextPow2AtLeast(max), hasAlpha);
        }

        private static void WritePng(Texture2D tex, string fileName)
        {
            byte[] png = tex.EncodeToPNG();
            string sysPath = Path.Combine(Directory.GetCurrentDirectory(), OutputDir, fileName);
            File.WriteAllBytes(sysPath, png);
        }

        private static void ImportTexture(string fileName, int maxSize, bool hasAlpha,
            TextureWrapMode wrap = TextureWrapMode.Clamp)
        {
            string assetPath = OutputDir + "/" + fileName;
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);

            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null) return;

            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = true;
            // タイルテクスチャ（Repeat）はミップを有効にして遠景のモアレを抑える
            importer.mipmapEnabled = wrap == TextureWrapMode.Repeat;
            importer.alphaIsTransparency = hasAlpha;
            importer.alphaSource = hasAlpha
                ? TextureImporterAlphaSource.FromInput
                : TextureImporterAlphaSource.None;
            importer.wrapMode = wrap;
            importer.filterMode = FilterMode.Bilinear;
            importer.maxTextureSize = maxSize;
            // UI から URL 参照するスプライト用途も想定し圧縮は無効寄りに。
            importer.textureCompression = TextureImporterCompression.Uncompressed;

            importer.SaveAndReimport();
        }

        private static void EnsureOutputDir()
        {
            string sysDir = Path.Combine(Directory.GetCurrentDirectory(), OutputDir);
            if (!Directory.Exists(sysDir))
            {
                Directory.CreateDirectory(sysDir);
                AssetDatabase.Refresh();
            }
        }

        private static void HideObjects(List<GameObject> hidden, System.Func<GameObject, bool> predicate)
        {
            var all = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            foreach (var go in all)
            {
                if (predicate(go) && go.activeSelf)
                {
                    go.SetActive(false);
                    hidden.Add(go);
                }
            }
        }

        private static void RestoreObjects(List<GameObject> hidden)
        {
            foreach (var go in hidden)
                if (go != null) go.SetActive(true);
        }

        private static Transform FindChildByName(Transform root, string name)
        {
            if (root.name == name) return root;
            foreach (Transform child in root)
            {
                var found = FindChildByName(child, name);
                if (found != null) return found;
            }
            return null;
        }

        private static Color Opaque(Color c) => new Color(c.r, c.g, c.b, 1f);

        private static int NextPow2AtLeast(int v)
        {
            int p = 32;
            while (p < v) p <<= 1;
            return p;
        }
    }
}
