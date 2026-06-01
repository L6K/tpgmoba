using UnityEngine;
using UnityEditor;
using TMPro;
using Unity.Collections;
using UnityEngine.TextCore.Text;
using UnityEngine.TextCore.LowLevel;

public class BakeJapaneseFont
{
    const string FontAssetPath = "Assets/_Project/Fonts/YuGothic_JP_SDF.asset";

    // よく使う日本語文字セット（ひらがな・カタカナ・基本漢字・記号）
    const string GlyphSet =
        "あいうえおかきくけこさしすせそたちつてとなにぬねのはひふへほまみむめもやゆよらりるれろわをん" +
        "ぁぃぅぇぉっゃゅょ" +
        "アイウエオカキクケコサシスセソタチツテトナニヌネノハヒフヘホマミムメモヤユヨラリルレロワヲン" +
        "ァィゥェォッャュョ" +
        "ーーー" +
        "山田鈴木田中フレンド設定通知ゲーム所持品ガチャプレイ開始" +
        "ABCDEFGHIJKLMNOPQRSTUVWXYZ" +
        "abcdefghijklmnopqrstuvwxyz" +
        "0123456789 .,!?-:/()[]●○■□★☆";

    public static void Execute()
    {
        var fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
        if (fontAsset == null)
        {
            Debug.LogError("[Enigma] Font asset not found: " + FontAssetPath);
            return;
        }

        // 文字のユニコードリストを作成
        var unicodes = new System.Collections.Generic.List<uint>();
        var seen = new System.Collections.Generic.HashSet<uint>();
        foreach (char c in GlyphSet)
        {
            uint u = (uint)c;
            if (seen.Add(u)) unicodes.Add(u);
        }

        // TryAddCharacters でグリフをアトラスに追加
        fontAsset.TryAddCharacters(unicodes.ToArray());

        EditorUtility.SetDirty(fontAsset);
        AssetDatabase.SaveAssets();
        Debug.Log($"[Enigma] Baked {unicodes.Count} glyphs into {FontAssetPath}");
    }
}
