using UnityEngine;
using UnityEditor;
using TMPro;
using UnityEngine.TextCore.LowLevel;
using System.Collections.Generic;

public class SetupNotoFont
{
    const string FontPath = "Assets/_Project/Fonts/NotoSansJP-Regular.ttf";
    const string AssetPath = "Assets/_Project/Fonts/NotoSansJP_SDF.asset";

    static readonly string GlyphSet =
        "あいうえおかきくけこさしすせそたちつてとなにぬねのはひふへほまみむめもやゆよらりるれろわをんー" +
        "ぁぃぅぇぉっゃゅょ" +
        "アイウエオカキクケコサシスセソタチツテトナニヌネノハヒフヘホマミムメモヤユヨラリルレロワヲン" +
        "ァィゥェォッャュョ" +
        "山田鈴木田中フレンド設定通知ゲーム所持品ガチャプレイ開始" +
        "ABCDEFGHIJKLMNOPQRSTUVWXYZ" +
        "abcdefghijklmnopqrstuvwxyz" +
        "0123456789 .,!?-:/()[]●○";

    public static void Execute()
    {
        AssetDatabase.ImportAsset(FontPath, ImportAssetOptions.ForceUpdate);
        AssetDatabase.Refresh();

        var font = AssetDatabase.LoadAssetAtPath<Font>(FontPath);
        if (font == null) { Debug.LogError("[Enigma] TTF not found: " + FontPath); return; }
        Debug.Log("[Enigma] Loaded font: " + font.name);

        if (AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetPath) != null)
            AssetDatabase.DeleteAsset(AssetPath);

        var fontAsset = TMP_FontAsset.CreateFontAsset(font, 40, 5, GlyphRenderMode.SDFAA, 2048, 2048);
        fontAsset.name = "NotoSansJP_SDF";
        AssetDatabase.CreateAsset(fontAsset, AssetPath);

        foreach (var tex in fontAsset.atlasTextures)
        {
            if (tex != null)
            {
                tex.name = "NotoSansJP_Atlas";
                AssetDatabase.AddObjectToAsset(tex, AssetPath);
            }
        }
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        // グリフをベイク
        var unicodes = new List<uint>();
        var seen = new HashSet<uint>();
        foreach (char c in GlyphSet)
            if (seen.Add((uint)c)) unicodes.Add((uint)c);

        uint[] missing;
        fontAsset.TryAddCharacters(unicodes.ToArray(), out missing);

        EditorUtility.SetDirty(fontAsset);
        AssetDatabase.SaveAssets();

        int baked = unicodes.Count - (missing?.Length ?? 0);
        Debug.Log($"[Enigma] NotoSansJP TTF → SDF: {baked}/{unicodes.Count} glyphs baked. Missing: {missing?.Length ?? 0}");
    }
}
