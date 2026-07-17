using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using Enigma.Character;
using Enigma.Combat;
using Enigma.Core;
using Enigma.GameMode;
using Debug = UnityEngine.Debug;

namespace Enigma.Learning
{
    /// <summary>
    /// 自動バランス調整パイプラインの実行基盤。
    /// Temp/balance_sim_request.json が存在するときのみ AetherRift_Map ロード時に自動生成され、
    /// 3v3（Blue Top/Bot/Jungle vs Red Top/Bot/Jungle）でロースターをシャッフルしながら指定試合数を
    /// 連続実行し、1試合ごとの統計を Temp/balance_runs/*.jsonl に追記する。
    /// トリガファイルが無ければ何もしないため、通常プレイ経路には副作用がない。
    /// </summary>
    public sealed class BalanceSimRunner : MonoBehaviour
    {
        private const string SceneName = "AetherRift_Map";
        private const string ResultSceneName = "Result";
        private const string RequestPath = "Temp/balance_sim_request.json";
        private const string DonePath = "Temp/balance_sim_done.json";
        private const string RunsDir = "Temp/balance_runs";

        // A1 凍結値（試合尺）とは無関係の、シム打ち切り用のセーフガード。25分 = 1500秒。
        private const float TimeoutSeconds = 25f * 60f;

        // 進捗ログの間隔（試合数）。
        private const int ProgressLogInterval = 10;

        [Serializable]
        private class SimRequest
        {
            public int matches = 1;
            public float timeScale = 1f;
            public bool mirror = false;
        }

        private SimRequest _request;
        private string _batchPath;
        private DateTime _startedAt;

        private int _matchIndex; // 0-based。次に走らせる試合の番号
        private BalanceMatchStats _currentStats;
        private float _matchStartTime;
        private bool _matchResolved;
        private bool _subscribedEvents;
        private bool _rosterCaptured;
        private string[] _capturedBlue = Array.Empty<string>();
        private string[] _capturedRed = Array.Empty<string>();

        // ミラー実験用: ペア(通常/入れ替え)は同一 rosterSeed を共有する。
        // ペアの1試合目は matchIndex を seed に使い、2試合目(mirrored)はペア相手と同じ seed を使い回す。
        private int _currentRosterSeed;
        private bool _currentMirrored;

        // GameObject 名（例: "BlueBot_Top"）→ CharId の解決テーブル。試合開始ごとに再構築する。
        private readonly Dictionary<string, string> _nameToCharId = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoSpawn()
        {
            if (SceneManager.GetActiveScene().name != SceneName) return;
            if (!File.Exists(RequestPath)) return;
            if (UnityEngine.Object.FindFirstObjectByType<BalanceSimRunner>() != null) return;

            var go = new GameObject("BalanceSimRunner");
            var runner = go.AddComponent<BalanceSimRunner>();
            UnityEngine.Object.DontDestroyOnLoad(go);
            runner.BeginFromRequestFile();
        }

        private void BeginFromRequestFile()
        {
            string json = File.ReadAllText(RequestPath);
            _request = JsonUtility.FromJson<SimRequest>(json);
            if (_request == null) _request = new SimRequest();
            if (_request.matches <= 0) _request.matches = 1;
            if (_request.timeScale <= 0f) _request.timeScale = 1f;

            // gitHash はバッチごとに取り直す(エディタセッション内キャッシュだと
            // 途中コミット後のバッチに古いハッシュが記録される — 2026-07-12 実測)
            MatchStatsRecording.ResetGitHashCache();

            _startedAt = DateTime.Now;
            Directory.CreateDirectory(RunsDir);
            _batchPath = $"{RunsDir}/batch_{_startedAt:yyyyMMdd_HHmm}.jsonl";

            SceneManager.sceneLoaded += OnSceneLoaded;

            // AutoSpawn はすでに AetherRift_Map ロード後に呼ばれているため、初回はここで直接セットアップする。
            SetUpMatch();
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name == SceneName)
            {
                SetUpMatch();
            }
            else if (scene.name == ResultSceneName)
            {
                // Result へ遷移してしまった場合の保険。MatchEnd を取りこぼしていても
                // ここで確実に試合を打ち切り、次試合（または終了）へ進める。
                if (!_matchResolved) FinishCurrentMatch(DetermineWinnerFallback(), "timeout");
                ProceedToNextMatchOrFinish();
            }
        }

        // 試合番号を seed に、ロースターをシャッフルして 2v2 のシムモードをセットアップする。
        private void SetUpMatch()
        {
            _matchResolved = false;
            _subscribedEvents = false;
            _matchStartTime = Time.time;
            _nameToCharId.Clear();

            if (!GameServices.IsInitialized) GameServices.Initialize();
            GameServices.MatchEvents?.Clear();

            Time.timeScale = _request.timeScale;

            // ミラー実験: 試合をペア(通常/入れ替え)で実行する。ペア内の2試合は同一 rosterSeed を
            // 共有する — 奇数番目(ペア先頭)は新しい seed(=ペアインデックス)を採番、偶数番目
            // (ペア相方)は直前の seed を使い回し、Blue/Red 入れ替え版を割り当てる。
            if (_request.mirror)
            {
                bool isSecondOfPair = _matchIndex % 2 == 1;
                if (isSecondOfPair)
                {
                    _currentMirrored = true; // rosterSeed は前試合のものを維持
                }
                else
                {
                    _currentRosterSeed = _matchIndex / 2;
                    _currentMirrored = false;
                }
            }
            else
            {
                _currentRosterSeed = _matchIndex;
                _currentMirrored = false;
            }

            // シムモードのロースール割当は rosterSeed を seed にする（Bootstrap.Start() 実行前に設定必須）。
            BotChampionBootstrap.SetSimSeed(_currentRosterSeed);
            BotChampionBootstrap.SetSimMirrored(_currentMirrored);

            var player = GameObject.Find("Player");
            if (player != null) player.SetActive(false);

            // 3v3 シム化: BlueBot_Jungle は通常プレイでは非アクティブのため、GameObject.Find
            // では見つからない(非アクティブオブジェクトを対象にしない)。FindObjectsByType に
            // FindObjectsInactive.Include を渡して名前一致で探し、Bootstrap.Start() より前
            // （sceneLoaded 時点）に有効化することで割当に間に合わせる。
            // RedBot_Jungle は通常どおりアクティブのままにする(3v3 化に伴い無効化は不要)。
            var allBots = UnityEngine.Object.FindObjectsByType<EnemyChampionAI>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var bot in allBots)
            {
                if (bot != null && bot.name == "BlueBot_Jungle")
                {
                    bot.gameObject.SetActive(true);
                    break;
                }
            }

            // OrbitCamera は _target(Player) 非アクティブ時も null 参照はしないが、
            // 無駄な追従計算を避けるため明示的に無効化する。
            var orbitCamera = UnityEngine.Object.FindFirstObjectByType<OrbitCamera>();
            if (orbitCamera != null) orbitCamera.enabled = false;

            // MatchEventCollector は RuntimeInitializeOnLoadMethod でセッション開始時に1回しか
            // 生成されない(DontDestroyOnLoad でもない)ため、シーン再ロード後の試合ではここで補充する。
            // 不在のままだと2試合目以降イベントが一切記録されない(スモークで実測)。
            if (UnityEngine.Object.FindFirstObjectByType<Enigma.GameModes.MatchEventCollector>() == null)
                new GameObject("MatchEventCollector").AddComponent<Enigma.GameModes.MatchEventCollector>();

            SubscribeMatchEvents();

            _currentStats = new BalanceMatchStats(_matchIndex, _matchIndex);
            _currentStats.SetGitHash(MatchStatsRecording.GetGitHash());
            _currentStats.SetRosterInfo(_currentRosterSeed, _currentMirrored);
            _rosterCaptured = false;
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
                // 決着種別(外部レビュー指摘対応): OT減衰開始後の決着は自然決着と区別する。
                float elapsed = Time.time - _matchStartTime;
                string outcome = elapsed >= OvertimeDecayLogic.DefaultOvertimeStartSeconds ? "ot_decay" : "natural";
                FinishCurrentMatch(MatchStatsRecording.TeamName(e.Team), outcome);
                ProceedToNextMatchOrFinish();
                return;
            }

            MatchStatsRecording.Apply(_currentStats, e, ResolveCharId, Time.time - _matchStartTime);
        }

        // GameObject 名（例: "BlueBot_Top"）から EnemyChampionAI.CharId を解決する。
        // 見つからなければ（プレイヤー由来の名前や未知の名前）null を返し、呼び側で無視させる。
        private string ResolveCharId(string actorName)
        {
            if (string.IsNullOrEmpty(actorName)) return null;
            if (_nameToCharId.TryGetValue(actorName, out var cached)) return cached;

            var go = GameObject.Find(actorName);
            var ai = go != null ? go.GetComponent<EnemyChampionAI>() : null;
            string charId = ai != null ? ai.CharId : null;
            _nameToCharId[actorName] = charId;
            return charId;
        }

        private void Update()
        {
            if (_matchResolved || _currentStats == null) return;

            // ロースターは Bootstrap.Start()(=sceneLoaded より後)で割当されるため、
            // セットアップ時ではなく試合中に一度だけ遅延キャプチャする。Result シーンへ
            // 遷移してしまうフォールバック経路でも編成を残せるようにするため。
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

            float elapsed = Time.time - _matchStartTime;
            if (elapsed > TimeoutSeconds)
            {
                FinishCurrentMatch("timeout", "timeout");
                ProceedToNextMatchOrFinish();
            }
        }

        // MatchEnd 到達時は Update での二重打ち切りを避けるため、
        // 打ち切り後すぐ次試合へ進める（OnSceneLoaded 側の Result 検知とも整合させる）。
        private void FinishCurrentMatch(string winnerTeam, string outcome)
        {
            if (_matchResolved) return;
            _matchResolved = true;

            _currentStats.SetWinner(winnerTeam);
            _currentStats.SetOutcome(outcome);
            _currentStats.SetDuration(Time.time - _matchStartTime);

            if (!_rosterCaptured)
            {
                var rosters = MatchStatsRecording.CollectBotRosters();
                _capturedBlue = rosters.blue;
                _capturedRed = rosters.red;
            }
            _currentStats.SetRosters(_capturedBlue, _capturedRed);

            File.AppendAllText(_batchPath, _currentStats.ToJsonLine() + "\n");

            UnsubscribeMatchEvents();

            _matchIndex++;
            if (_matchIndex % ProgressLogInterval == 0 || _matchIndex >= _request.matches)
                Debug.Log($"[BalanceSimRunner] completed {_matchIndex}/{_request.matches} matches");
        }

        // MatchEnd/timeout どちらでも取りこぼした場合の保険（Result シーン到達時のフォールバック勝者判定）。
        // Result への遷移自体が MatchFlowController の Victory/Defeat（プレイヤー=青視点）由来のため、
        // ここでは判定材料が無ければ timeout 扱いにする。
        private string DetermineWinnerFallback() => "timeout";

        private void ProceedToNextMatchOrFinish()
        {
            if (_matchIndex >= _request.matches)
            {
                FinishSim();
                return;
            }

            SceneManager.LoadScene(SceneName);
        }

        private void FinishSim()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            UnsubscribeMatchEvents();
            BotChampionBootstrap.ClearSimOverride();

            if (File.Exists(RequestPath)) File.Delete(RequestPath);

            double elapsedRealSeconds = (DateTime.Now - _startedAt).TotalSeconds;
            string doneJson = "{"
                + "\"matches\":" + _matchIndex + ","
                + "\"elapsedRealSeconds\":" + elapsedRealSeconds.ToString("F1") + ","
                + "\"outputPath\":\"" + _batchPath.Replace("\\", "/") + "\""
                + "}";
            File.WriteAllText(DonePath, doneJson);

            Time.timeScale = 1f;
            Debug.Log($"[BalanceSimRunner] sim finished: {_matchIndex} matches, output={_batchPath}");

            Destroy(gameObject);
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            UnsubscribeMatchEvents();
        }
    }
}
