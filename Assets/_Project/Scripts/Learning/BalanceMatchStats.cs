using System.Collections.Generic;
using System.Text;

namespace Enigma.Learning
{
    /// <summary>
    /// 1試合分のバランスシム統計を蓄積し、JSONL 1行分の文字列を組み立てる純粋ロジック。
    /// JsonUtility はネストした辞書（perChampion）をシリアライズできないため、
    /// 手書き文字列組み立てで JSON を生成する（analyze_balance.py 側でパースする）。
    /// </summary>
    public sealed class BalanceMatchStats
    {
        private sealed class ChampionTally
        {
            public int Kills;
            public int Deaths;
            public int Cs;
        }

        // タワー撃破の時刻付きイベント(偏り調査用。試合開始からの経過秒+破壊側チーム名)。
        private readonly struct TimedTeamEvent
        {
            public readonly float Time;
            public readonly string Team;

            public TimedTeamEvent(float time, string team)
            {
                Time = time;
                Team = team;
            }
        }

        // チャンピオンキルの時刻+位置付きイベント(偏り調査用。マップ上のどこでキルが起きたかを見るために
        // 倒された側の座標(X,Z)を持つ)。
        private readonly struct KillTeamEvent
        {
            public readonly float Time;
            public readonly string Team;
            public readonly float X;
            public readonly float Z;

            public KillTeamEvent(float time, string team, float x, float z)
            {
                Time = time;
                Team = team;
                X = x;
                Z = z;
            }
        }

        public int MatchId { get; }
        public int Seed { get; }

        private string[] _blueRoster = System.Array.Empty<string>();
        private string[] _redRoster = System.Array.Empty<string>();
        private readonly Dictionary<string, ChampionTally> _perChampion = new();
        // 挿入順を保持して JSON 出力を安定させる（Dictionary の列挙順は非保証のため）。
        private readonly List<string> _championOrder = new();

        private readonly List<TimedTeamEvent> _towerEvents = new();
        private readonly List<KillTeamEvent> _killEvents = new();

        public float DurationSec { get; private set; }
        public string WinnerTeam { get; private set; } = "timeout";
        public string FirstTowerTeam { get; private set; } = "";
        public int CoreCapturesBlue { get; private set; }
        public int CoreCapturesRed { get; private set; }

        // 計測ガードレール第1弾: 結果に実装バージョンとミラー実験のペア照合キーを記録する。
        public string GitHash { get; private set; } = "unknown";
        public int RosterSeed { get; private set; }
        public bool Mirrored { get; private set; }

        // 人間プレイセッション記録用: Bot シム由来のデータと区別するためのフラグ（既定 false = シム由来）。
        public bool Human { get; private set; }

        // 決着種別(外部レビュー指摘対応): natural（試合時間<900秒の MatchEnd）/ot_decay（OT減衰下の MatchEnd）/
        // timeout（タイムアウト・フォールバック経路）/unknown（未設定=取りこぼし保険経路）。
        // 明示的にセットされなかったことが分かるよう、既定は natural ではなく unknown にする。
        public string Outcome { get; private set; } = "unknown";

        public BalanceMatchStats(int matchId, int seed)
        {
            MatchId = matchId;
            Seed = seed;
            RosterSeed = seed;
        }

        /// <summary>結果への実装バージョン記録用。gitHash は呼び側(Runner)が1回だけ取得してキャッシュしたものを渡す。</summary>
        public void SetGitHash(string gitHash)
        {
            GitHash = string.IsNullOrEmpty(gitHash) ? "unknown" : gitHash;
        }

        /// <summary>ミラー実験用: 実際にロースター割当に使った seed とミラー有無を記録する（ペアの照合キー）。</summary>
        public void SetRosterInfo(int rosterSeed, bool mirrored)
        {
            RosterSeed = rosterSeed;
            Mirrored = mirrored;
        }

        /// <summary>人間プレイセッション記録用: HumanPlayRecorder が呼ぶ。Bot シムは呼ばず既定 false のままにする。</summary>
        public void SetHuman(bool human)
        {
            Human = human;
        }

        /// <summary>決着種別を記録する。呼び側(Runner/Recorder)が natural/ot_decay/timeout/unknown を判定して渡す。</summary>
        public void SetOutcome(string outcome)
        {
            Outcome = string.IsNullOrEmpty(outcome) ? "unknown" : outcome;
        }

        public void SetRosters(string[] blueRoster, string[] redRoster)
        {
            _blueRoster = blueRoster ?? System.Array.Empty<string>();
            _redRoster = redRoster ?? System.Array.Empty<string>();
        }

        public void SetDuration(float durationSec) => DurationSec = durationSec;

        public void SetWinner(string winnerTeam) => WinnerTeam = winnerTeam;

        public void RecordKill(string charId)
        {
            if (string.IsNullOrEmpty(charId)) return;
            GetOrAdd(charId).Kills++;
        }

        public void RecordDeath(string charId)
        {
            if (string.IsNullOrEmpty(charId)) return;
            GetOrAdd(charId).Deaths++;
        }

        public void RecordCs(string charId)
        {
            if (string.IsNullOrEmpty(charId)) return;
            GetOrAdd(charId).Cs++;
        }

        public void RecordTowerDestroyed(string team)
        {
            // 最初の1本のみ記録する（firstTower とマッチ勝敗の相関分析に使う）。
            if (string.IsNullOrEmpty(FirstTowerTeam)) FirstTowerTeam = team;
        }

        /// <summary>タワー撃破イベントを経過秒付きで記録する(偏り調査用の時系列ログ)。既存の RecordTowerDestroyed と並存する。</summary>
        public void RecordTowerDestroyedAt(float elapsedSec, string team)
        {
            _towerEvents.Add(new TimedTeamEvent(elapsedSec, team));
        }

        /// <summary>チャンピオンキルイベントを経過秒+位置付きで記録する(偏り調査用の時系列ログ)。
        /// x/z は倒された側の座標。位置不明な呼び出し元との互換のため既定 0。</summary>
        public void RecordChampionKillAt(float elapsedSec, string team, float x = 0f, float z = 0f)
        {
            _killEvents.Add(new KillTeamEvent(elapsedSec, team, x, z));
        }

        public void RecordCoreCaptured(string team)
        {
            if (team == "Blue") CoreCapturesBlue++;
            else if (team == "Red") CoreCapturesRed++;
        }

        private ChampionTally GetOrAdd(string charId)
        {
            if (!_perChampion.TryGetValue(charId, out var tally))
            {
                tally = new ChampionTally();
                _perChampion[charId] = tally;
                _championOrder.Add(charId);
            }
            return tally;
        }

        /// <summary>JSONL の1行分の JSON 文字列を組み立てる（末尾改行なし）。</summary>
        public string ToJsonLine()
        {
            var sb = new StringBuilder();
            sb.Append('{');

            sb.Append("\"matchId\":").Append(MatchId).Append(',');
            sb.Append("\"seed\":").Append(Seed).Append(',');
            sb.Append("\"durationSec\":").Append(DurationSec.ToString("F1")).Append(',');
            sb.Append("\"winnerTeam\":\"").Append(Escape(WinnerTeam)).Append("\",");
            sb.Append("\"gitHash\":\"").Append(Escape(GitHash)).Append("\",");
            sb.Append("\"rosterSeed\":").Append(RosterSeed).Append(',');
            sb.Append("\"mirrored\":").Append(Mirrored ? "true" : "false").Append(',');
            sb.Append("\"human\":").Append(Human ? "true" : "false").Append(',');
            sb.Append("\"outcome\":\"").Append(Escape(Outcome)).Append("\",");

            sb.Append("\"blueRoster\":[").Append(JoinQuoted(_blueRoster)).Append("],");
            sb.Append("\"redRoster\":[").Append(JoinQuoted(_redRoster)).Append("],");

            sb.Append("\"perChampion\":{");
            for (int i = 0; i < _championOrder.Count; i++)
            {
                if (i > 0) sb.Append(',');
                string id = _championOrder[i];
                var t = _perChampion[id];
                sb.Append('"').Append(Escape(id)).Append("\":{");
                sb.Append("\"kills\":").Append(t.Kills).Append(',');
                sb.Append("\"deaths\":").Append(t.Deaths).Append(',');
                sb.Append("\"cs\":").Append(t.Cs);
                sb.Append('}');
            }
            sb.Append("},");

            sb.Append("\"firstTowerTeam\":\"").Append(Escape(FirstTowerTeam)).Append("\",");
            sb.Append("\"coreCapturesBlue\":").Append(CoreCapturesBlue).Append(',');
            sb.Append("\"coreCapturesRed\":").Append(CoreCapturesRed).Append(',');

            sb.Append("\"towerEvents\":[").Append(JoinTimedEvents(_towerEvents)).Append("],");
            sb.Append("\"killEvents\":[").Append(JoinKillEvents(_killEvents)).Append(']');

            sb.Append('}');
            return sb.ToString();
        }

        private static string JoinTimedEvents(List<TimedTeamEvent> events)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < events.Count; i++)
            {
                if (i > 0) sb.Append(',');
                var e = events[i];
                sb.Append("{\"t\":").Append(e.Time.ToString("F1"))
                  .Append(",\"team\":\"").Append(Escape(e.Team)).Append("\"}");
            }
            return sb.ToString();
        }

        private static string JoinKillEvents(List<KillTeamEvent> events)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < events.Count; i++)
            {
                if (i > 0) sb.Append(',');
                var e = events[i];
                sb.Append("{\"t\":").Append(e.Time.ToString("F1"))
                  .Append(",\"team\":\"").Append(Escape(e.Team)).Append('"')
                  .Append(",\"x\":").Append(e.X.ToString("F1"))
                  .Append(",\"z\":").Append(e.Z.ToString("F1"))
                  .Append('}');
            }
            return sb.ToString();
        }

        private static string JoinQuoted(string[] values)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < values.Length; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append('"').Append(Escape(values[i])).Append('"');
            }
            return sb.ToString();
        }

        // JSON 文字列値中のダブルクォート/バックスラッシュのみ最小限エスケープする
        // （CharId・チーム名はすべて英数字のみが渡ってくる前提のため、これで十分）。
        private static string Escape(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }
    }
}
