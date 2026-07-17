using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using Enigma.Character;
using Enigma.Combat;
using Enigma.Core;
using Debug = UnityEngine.Debug;

namespace Enigma.Learning
{
    /// <summary>
    /// MatchEvent → BalanceMatchStats への変換と、付随する小道具（チーム名解決、ロースター収集、
    /// gitHash 取得）を BalanceSimRunner（Botシム）と HumanPlayRecorder（人間プレイ記録）で共有する。
    /// 試合終了（MatchEnd）時の遷移方針は呼び側ごとに異なる（シムは次試合へ進む/人間記録は待機する）ため、
    /// MatchEnd の処理自体は呼び側に残し、ここでは変換しない。
    /// </summary>
    public static class MatchStatsRecording
    {
        /// <summary>MatchEnd 以外のイベントを BalanceMatchStats へ反映する。挙動は元 BalanceSimRunner.OnMatchEventLogged と同一。</summary>
        public static void Apply(BalanceMatchStats stats, MatchEvent e, Func<string, string> resolveCharId, float elapsedSeconds)
        {
            switch (e.Type)
            {
                case MatchEventType.ChampionKill:
                    stats.RecordKill(resolveCharId(e.ActorName));
                    // 偏り調査用: 先制キル/キルタイミングの Blue-Red 比較に使う時刻付きログ。
                    stats.RecordChampionKillAt(elapsedSeconds, TeamName(e.Team));
                    break;
                case MatchEventType.ChampionDeath:
                    stats.RecordDeath(resolveCharId(e.ActorName));
                    break;
                case MatchEventType.MinionKill:
                    stats.RecordCs(resolveCharId(e.ActorName));
                    break;
                case MatchEventType.TowerDestroyed:
                    stats.RecordTowerDestroyed(TeamName(e.Team));
                    // 偏り調査用: OT(900s)前後のタワーレース非対称を時系列で特定するための時刻付きログ。
                    stats.RecordTowerDestroyedAt(elapsedSeconds, TeamName(e.Team));
                    break;
                case MatchEventType.CoreCaptured:
                    stats.RecordCoreCaptured(TeamName(e.Team));
                    break;
            }
        }

        public static string TeamName(int team)
        {
            var t = (TeamId)team;
            if (t == TeamId.Blue) return "Blue";
            if (t == TeamId.Red) return "Red";
            return "Neutral";
        }

        /// <summary>現在アクティブな Bot（EnemyChampionAI）を TeamTag で Blue/Red に振り分けて収集する。元 BalanceSimRunner.CollectRosters と同一。</summary>
        public static (string[] blue, string[] red) CollectBotRosters()
        {
            var blue = new List<string>();
            var red = new List<string>();

            var bots = UnityEngine.Object.FindObjectsByType<EnemyChampionAI>(FindObjectsSortMode.None);
            foreach (var bot in bots)
            {
                if (bot == null || !bot.gameObject.activeInHierarchy) continue;
                var tag = bot.GetComponent<TeamTag>();
                if (tag == null) continue;
                if (tag.Team == TeamId.Blue) blue.Add(bot.CharId);
                else if (tag.Team == TeamId.Red) red.Add(bot.CharId);
            }

            return (blue.ToArray(), red.ToArray());
        }

        // 結果への実装バージョン記録用。git rev-parse --short HEAD をエディタ起動中に1回だけ実行して
        // キャッシュする(呼び側が gitHash 取り直しを必要とするタイミングで ResetGitHashCache を呼ぶ)。
        // 失敗時は "unknown" にフォールバックする。元 BalanceSimRunner.GetGitHash と同一。
        private static string s_gitHashCache;
        private static bool s_gitHashResolved;

        public static void ResetGitHashCache() => s_gitHashResolved = false;

        public static string GetGitHash()
        {
            if (s_gitHashResolved) return s_gitHashCache;
            s_gitHashResolved = true;

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "git",
                    Arguments = "rev-parse --short HEAD",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WorkingDirectory = Application.dataPath,
                };
                using var proc = Process.Start(psi);
                string output = proc.StandardOutput.ReadToEnd().Trim();
                proc.WaitForExit(5000);
                s_gitHashCache = string.IsNullOrEmpty(output) ? "unknown" : output;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[MatchStatsRecording] git rev-parse failed, using \"unknown\": {e.Message}");
                s_gitHashCache = "unknown";
            }

            return s_gitHashCache;
        }
    }
}
