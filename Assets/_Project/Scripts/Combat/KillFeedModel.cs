using System;
using System.Collections.Generic;

namespace Enigma.Combat
{
    // キルフィード1行分の不変データ。表示色はチームから決まるので生の TeamId のまま保持する。
    public readonly struct KillFeedEntry
    {
        public readonly string KillerName;
        public readonly string VictimName;
        public readonly TeamId KillerTeam;
        public readonly TeamId VictimTeam;

        public KillFeedEntry(string killerName, string victimName, TeamId killerTeam, TeamId victimTeam)
        {
            KillerName = killerName;
            VictimName = victimName;
            KillerTeam = killerTeam;
            VictimTeam = victimTeam;
        }
    }

    // キルフィードのロジック層（plain C#・テスト可能）。最新 MaxEntries 件のみ保持し、
    // 先頭が最新になるよう積む。変更通知は Changed で行い、UI 層が購読して再構築する。
    public sealed class KillFeedModel
    {
        public const int MaxEntries = 5;

        private readonly List<KillFeedEntry> _entries = new List<KillFeedEntry>();

        // 先頭が最新。読み取り専用ビューを公開してUI側からの変更を防ぐ
        public IReadOnlyList<KillFeedEntry> Entries => _entries;

        public event Action Changed;

        public void AddEntry(string killerName, string victimName, TeamId killerTeam, TeamId victimTeam)
        {
            _entries.Insert(0, new KillFeedEntry(killerName, victimName, killerTeam, victimTeam));

            // 上限超過分は末尾（最古）から落とす
            while (_entries.Count > MaxEntries)
                _entries.RemoveAt(_entries.Count - 1);

            Changed?.Invoke();
        }
    }
}
