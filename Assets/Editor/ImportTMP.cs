using UnityEditor;
using UnityEngine;

public class ImportTMP
{
    public static void Execute()
    {
        // TMP Essential Resources のパスを検索してインポート
        string[] guids = AssetDatabase.FindAssets("TMP Essential Resources");
        if (guids.Length > 0)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[0]);
            AssetDatabase.ImportPackage(path, false);
            Debug.Log("[Enigma] TMP Essential Resources imported from: " + path);
        }
        else
        {
            // PackageCache から直接探す
            string packagePath = "Packages/com.unity.textmeshpro/Package Resources/TMP Essential Resources.unitypackage";
            AssetDatabase.ImportPackage(packagePath, false);
            Debug.Log("[Enigma] TMP Essential Resources imported from package.");
        }
    }
}
