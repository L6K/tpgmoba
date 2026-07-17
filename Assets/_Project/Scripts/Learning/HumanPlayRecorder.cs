using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using Enigma.Character;
using Enigma.Core;

namespace Enigma.Learning
{
    /// <summary>
    /// 通常の人間プレイセッションを BalanceSimRunner と同一スキーマの JSONL で記録する。
    /// バランス測定が全て Bot シム由来のデータに偏っている（外部レビュー指摘）ことへの対処として、
    /// AetherRift_Map をトリガファイル無しで(=通常プレイとして)起動したときに自動生成される。
    /// BalanceSimRunner と異なり、ロースター操作・シーン自動リロード・タイムアウト打ち切りは
    /// 一切行わない（観測に徹し、通常プレイの挙動を変えない）。試合終了ごとに1行追記し、
    /// 以後は次のプレイ（シーン再ロード）を待つ。
    /// </summary>
    public sealed class HumanPlayRecorder : MonoBehaviour
    {
        private const string SceneName = "AetherRift_Map";
        private const string ResultSceneName = "Result";
        private const string SimRequestPath = "Temp/balance_sim_request.json";
        private const string SessionsDir = "MLTraining/human_sessions";

        // MatchEventCollector 上で Bot（EnemyChampionAI）として解決できない actor（=人間プレイヤー）
        // をまとめる固定キー。ピック済みチャンピオン別に分けると集計が「人間プレイかどうか」の
        // 比較に使いにくくなるため、単一キーにする（スコープ最小化の判断）。
        private const string PlayerChampionKey = "player";

        private string _sessionPath;
        private int _matchIndex; // 0-based。このエディタセッション内で記録した試合数
        private BalanceMatchStats _currentStats;
        private float _matchStartTime;
        private bool _matchResolved;
        private bool _subscribedEvents;
        private bool _rosterCaptured;
        private string[] _capturedBlue = Array.Empty<string>();
        private string[] _capturedRed = Array.Empty<string>();

        // GameObject 名 → CharId の解決テーブル。試合開始ごとに再構築する。
        private readonly Dictionary<string, string> _nameToCharId = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoSpawn()
        {
            if (SceneManager.GetActiveScene().name != SceneName) return;
            if (File.Exists(SimRequestPath)) return; // シム実行中は記録しない(BalanceSimRunner と相互排他)
            if (UnityEngine.Object.FindFirstObjectByType<HumanPlayRecorder>() != null) return;

            var go = new GameObject("HumanPlayRecorder");
            var recorder = go.AddComponent<HumanPlayRecorder>();
            UnityEngine.Object.DontDestroyOnLoad(go);
            recorder.Begin();
        }

        private void Begin()
        {
            Directory.CreateDirectory(SessionsDir);
            // Temp ではなく MLTraining に出す(Temp は Unity 再起動で消えるため、人間の記録は残す必要がある)。
            _sessionPath = $"{SessionsDir}/session_{DateTime.Now:yyyyMMdd_HHmm}.jsonl";

            SceneManager.sceneLoaded += OnSceneLoaded;

            // AutoSpawn はすでに AetherRift_Map ロード後に呼ばれているため、初回はここで直接セットアップする。
            SetUpMatch();
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name == SceneName)
            {
                // 連戦(スコアボードから再戦 等でシーンが再ロードされた場合): 次の1試合を記録する。
                SetUpMatch();
            }
            else if (scene.name == ResultSceneName)
            {
                // MatchEnd イベントを取りこぼして Result へ遷移した場合の保険。
                // シムと違い次試合へは進めない(=待機するだけ)ため、ここでは記録の締めのみ行う。
                if (!_matchResolved) FinishCurrentMatch("unknown");
            }
        }

        private void SetUpMatch()
        {
            _matchResolved = false;
            _subscribedEvents = false;
            _matchStartTime = Time.time;
            _nameToCharId.Clear();
            _rosterCaptured = false;

            if (!GameServices.IsInitialized) GameServices.Initialize();

            // MatchEventCollector は RuntimeInitializeOnLoadMethod でセッション開始時に1回しか
            // 生成されない(DontDestroyOnLoad でもない)ため、シーン再ロード後の試合ではここで補充する
            // (BalanceSimRunner が踏んだのと同じ既知の穴 — 2試合目以降イベント欠落を防ぐ)。
            if (UnityEngine.Object.FindFirstObjectByType<Enigma.GameModes.MatchEventCollector>() == null)
                new GameObject("MatchEventCollector").AddComponent<Enigma.GameModes.MatchEventCollector>();

            SubscribeMatchEvents();

            _currentStats = new BalanceMatchStats(_matchIndex, _matchIndex);
            _currentStats.SetGitHash(MatchStatsRecording.GetGitHash());
            _currentStats.SetHuman(true);
        }

        private void SubscribeMatchEvents()
        {
            if (_subscribedEvents) return;
            var log = GameServices.MatchEvents;
            if (log == null) return;
            log.EventLogged += OnMatchEventLogged;
            _subscribedEvents = true;
        }

        private void UnsubscribeMatchEvents()
        {
            if (!_subscribedEvents) return;
            var log = GameServices.MatchEvents;
            if (log != null) log.EventLogged -= OnMatchEventLogged;
            _subscribedEvents = false;
        }

        private void OnMatchEventLogged(MatchEvent e)
        {
            if (_matchResolved) return;

            if (e.Type == MatchEventType.MatchEnd)
            {
                FinishCurrentMatch(MatchStatsRecording.TeamName(e.Team));
                return;
            }

            MatchStatsRecording.Apply(_currentStats, e, ResolveCharId, Time.time - _matchStartTime);
        }

        // GameObject 名から charId を解決する。Bot は EnemyChampionAI.CharId、
        // 人間プレイヤー(PlayerController)は固定キー "player" に解決する。
        // どちらでもない(未知の名前)場合は null を返し、呼び側で無視させる。
        private string ResolveCharId(string actorName)
        {
            if (string.IsNullOrEmpty(actorName)) return null;
            if (_nameToCharId.TryGetValue(actorName, out var cached)) return cached;

            var go = GameObject.Find(actorName);
            string charId = null;
            if (go != null)
            {
                var ai = go.GetComponent<EnemyChampionAI>();
                if (ai != null) charId = ai.CharId;
                else if (go.GetComponent<PlayerController>() != null) charId = PlayerChampionKey;
            }
            _nameToCharId[actorName] = charId;
            return charId;
        }

        private void Update()
        {
            if (_matchResolved || _currentStats == null) return;

            // ロースターは Bootstrap.Start()(=sceneLoaded より後)で割当されるため、
            // セットアップ時ではなく試合中に一度だけ遅延キャプチャする(BalanceSimRunner と同じ理由)。
            if (!_rosterCaptured)
            {
                var rosters = MatchStatsRecording.CollectBotRosters();
                if (rosters.blue.Length > 0 || rosters.red.Length > 0)
                {
                    _capturedBlue = rosters.blue;
                    _capturedRed = rosters.red;
                    _rosterCaptured = true;
                }
            }
        }

        private void FinishCurrentMatch(string winnerTeam)
        {
            if (_matchResolved) return;
            _matchResolved = true;

            _currentStats.SetWinner(winnerTeam);
            _currentStats.SetDuration(Time.time - _matchStartTime);

            if (!_rosterCaptured)
            {
                var rosters = MatchStatsRecording.CollectBotRosters();
                _capturedBlue = rosters.blue;
                _capturedRed = rosters.red;
            }
            _currentStats.SetRosters(_capturedBlue, _capturedRed);

            File.AppendAllText(_sessionPath, _currentStats.ToJsonLine() + "\n");
            UnsubscribeMatchEvents();

            _matchIndex++;
            Debug.Log($"[HumanPlayRecorder] recorded match {_matchIndex}, output={_sessionPath}");
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            UnsubscribeMatchEvents();
        }
    }
}
