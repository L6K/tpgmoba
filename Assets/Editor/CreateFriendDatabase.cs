using UnityEngine;
using UnityEditor;
using Enigma.UI;

public class CreateFriendDatabase
{
    public static void Execute()
    {
        string dir = "Assets/_Project/Data";
        if (!AssetDatabase.IsValidFolder(dir))
            AssetDatabase.CreateFolder("Assets/_Project", "Data");

        // ダミーフレンドデータ
        var dummyFriends = new (string name, int level, FriendStatus status)[]
        {
            ("山田タロウ",   24, FriendStatus.Online),
            ("鈴木ハナコ",   31, FriendStatus.InGame),
            ("田中ケンジ",   18, FriendStatus.InQueue),
            ("佐藤アキラ",   45, FriendStatus.Online),
            ("中村ユキ",     12, FriendStatus.Offline),
            ("伊藤マサル",   38, FriendStatus.Offline),
            ("渡辺サクラ",   27, FriendStatus.Online),
        };

        var createdFriends = new FriendData[dummyFriends.Length];

        for (int i = 0; i < dummyFriends.Length; i++)
        {
            var (name, level, status) = dummyFriends[i];
            string path = $"{dir}/Friend_{name}.asset";

            // 既存があれば更新、なければ作成
            var existing = AssetDatabase.LoadAssetAtPath<FriendData>(path);
            if (existing != null)
            {
                existing.DisplayName = name;
                existing.Level       = level;
                existing.Status      = status;
                EditorUtility.SetDirty(existing);
                createdFriends[i] = existing;
            }
            else
            {
                var data = ScriptableObject.CreateInstance<FriendData>();
                data.DisplayName = name;
                data.Level       = level;
                data.Status      = status;
                AssetDatabase.CreateAsset(data, path);
                createdFriends[i] = data;
            }
        }

        // FriendDatabase を作成
        string dbPath = $"{dir}/FriendDatabase.asset";
        var db = AssetDatabase.LoadAssetAtPath<FriendDatabase>(dbPath)
                 ?? ScriptableObject.CreateInstance<FriendDatabase>();

        // SerializedObject 経由で friends リストを設定
        var so = new SerializedObject(db);
        var friendsProp = so.FindProperty("_friends");
        friendsProp.arraySize = createdFriends.Length;
        for (int i = 0; i < createdFriends.Length; i++)
            friendsProp.GetArrayElementAtIndex(i).objectReferenceValue = createdFriends[i];
        so.ApplyModifiedProperties();

        if (!AssetDatabase.Contains(db))
            AssetDatabase.CreateAsset(db, dbPath);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[Enigma] FriendDatabase created with {createdFriends.Length} friends: {dbPath}");
    }
}
