namespace Enigma.Combat
{
    /// <summary>
    /// 死亡リキャップ用の攻撃者名整形（純粋関数）。
    /// GameObject 名の "(Clone)" 接尾辞を除去し、アンダースコアを空白へ置換して読みやすくする。
    /// </summary>
    public static class DeathRecapSourceName
    {
        public const string Unknown = "環境・不明";

        public static string Clean(string rawName)
        {
            if (string.IsNullOrEmpty(rawName)) return Unknown;

            string s = rawName;

            // Instantiate 由来の "(Clone)" を除去。
            int clone = s.IndexOf("(Clone)", System.StringComparison.Ordinal);
            if (clone >= 0) s = s.Substring(0, clone);

            s = s.Replace('_', ' ').Trim();

            return s.Length == 0 ? Unknown : s;
        }
    }
}
