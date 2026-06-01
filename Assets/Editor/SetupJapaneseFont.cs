using UnityEngine;
using UnityEditor;
using TMPro;
using UnityEngine.TextCore.LowLevel;
using System.IO;

public class SetupJapaneseFont
{
    const string FontDestPath = "Assets/_Project/Fonts";
    const string FontAssetName = "YuGothic_JP_SDF";

    static readonly string[] FontFileCandidates = {
        @"C:\Windows\Fonts\YuGothR.ttc",
        @"C:\Windows\Fonts\YuGothM.ttc",
        @"C:\Windows\Fonts\yugothic.ttf",
        @"C:\Windows\Fonts\meiryo.ttc",
        @"C:\Windows\Fonts\msgothic.ttc",
    };

    // よく使う日本語文字セット
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
        if (!AssetDatabase.IsValidFolder("Assets/_Project"))
            AssetDatabase.CreateFolder("Assets", "_Project");
        if (!AssetDatabase.IsValidFolder(FontDestPath))
            AssetDatabase.CreateFolder("Assets/_Project", "Fonts");

        string assetPath = $"{FontDestPath}/{FontAssetName}.asset";

        // 既存を削除して作り直す
        if (AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath) != null)
            AssetDatabase.DeleteAsset(assetPath);

        // フォントファイルを探す
        string srcFont = null;
        foreach (var c in FontFileCandidates)
            if (File.Exists(c)) { srcFont = c; break; }

        if (srcFont == null) { Debug.LogError("[Enigma] No Japanese font found."); return; }

        string ext = Path.GetExtension(srcFont);
        string destFontPath = $"{FontDestPath}/YuGothic{ext}";
        string destFontAbsPath = Path.Combine(Application.dataPath, "..", destFontPath).Replace('/', '\\');

        if (!File.Exists(destFontAbsPath))
        {
            File.Copy(srcFont, destFontAbsPath);
            AssetDatabase.ImportAsset(destFontPath);
        }
        AssetDatabase.Refresh();

        var font = AssetDatabase.LoadAssetAtPath<Font>(destFontPath);
        if (font == null) { Debug.LogError("[Enigma] Failed to load font: " + destFontPath); return; }

        // TMP Font Asset 作成
        var fontAsset = TMP_FontAsset.CreateFontAsset(font, 40, 5, GlyphRenderMode.SDFAA, 2048, 2048);
        fontAsset.name = FontAssetName;
        AssetDatabase.CreateAsset(fontAsset, assetPath);

        // アトラステクスチャをサブアセットとして登録
        if (fontAsset.atlasTextures != null)
        {
            foreach (var tex in fontAsset.atlasTextures)
            {
                if (tex != null)
                {
                    tex.name = FontAssetName + "_Atlas";
                    AssetDatabase.AddObjectToAsset(tex, assetPath);
                }
            }
        }

        // 日本語グリフを事前ベイク
        var unicodes = new System.Collections.Generic.List<uint>();
        var seen = new System.Collections.Generic.HashSet<uint>();
        foreach (char c in GlyphSet)
            if (seen.Add((uint)c)) unicodes.Add((uint)c);

        fontAsset.TryAddCharacters(unicodes.ToArray());

        EditorUtility.SetDirty(fontAsset);
        AssetDatabase.SaveAssets();
        Debug.Log($"[Enigma] Font asset created with {unicodes.Count} pre-baked glyphs: {assetPath}");
    }
}
