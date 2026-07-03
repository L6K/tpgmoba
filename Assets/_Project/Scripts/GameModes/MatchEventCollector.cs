using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Enigma.Combat;
using Enigma.Core;
using Enigma.Character;

namespace Enigma.GameModes
{
    /// <summary>
    /// 全 HealthComponent の死亡を監視し、種別(チャンピオン/ミニオン/タワー/タイタン/ボス)を
    /// 判定して GameServices.MatchEvents に記録する。将来の ML-Agents 学習で報酬信号として使う。
    /// </summary>
    public sealed class MatchEventCollector : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoSpawn()
        {
            if (FindObjectOfType<MatchEventCollector>() != null) return;
            var go = new GameObject("MatchEventCollector");
            go.AddComponent<MatchEventCollector>();
        }

        private readonly HashSet<HealthComponent> _subscribed = new();
        // CentralObjectiveDirector.BossHealth は死亡時(および Dormant 中)に null を返すため、
        // 生存・出現中に観測できたボス参照をここに記憶し、死亡通知時にそれを参照する。
        // ボスの GameObject は Dormant 中もアクティブ(コンポーネントのみ無効化)なので、
        // 初回購読時だけの判定では Dormant 中に購読した場合にボスと認識できない。
        private readonly HashSet<HealthComponent> _bossHcs = new();

        private void Start()
        {
            StartCoroutine(ScanLoop());
        }

        private IEnumerator ScanLoop()
        {
            while (true)
            {
                yield return new WaitForSeconds(1f);
                SubscribeNew();
            }
        }

        private void SubscribeNew()
        {
            // 毎スキャンで「今生きて出現中のボス」を再確認する(Dormant 中は null のため、
            // Active 中のスキャンで一度でも一致すればボスとして記憶される)。
            var boss = CentralObjectiveDirector.Instance != null
                ? CentralObjectiveDirector.Instance.BossHealth
                : null;

            var all = Object.FindObjectsByType<HealthComponent>(FindObjectsSortMode.None);
            foreach (var hc in all)
            {
                if (hc == boss) _bossHcs.Add(hc);
                if (_subscribed.Add(hc))
                    hc.Model.Died += () => OnVictimDied(hc);
            }
        }

        private void OnVictimDied(HealthComponent victim)
        {
            var log = GameServices.MatchEvents;
            if (log == null || victim == null) return;

            float time = Time.time;
            var victimTag = victim.GetComponentInParent<TeamTag>();
            int victimTeam = victimTag != null ? (int)victimTag.Team : (int)TeamId.Neutral;
            var lastAttacker = victim.LastAttacker;
            var attackerTag = lastAttacker != null ? lastAttacker.GetComponentInParent<TeamTag>() : null;

            bool isBoss = _bossHcs.Contains(victim);
            string name = victim.name;

            if (isBoss)
            {
                int killerTeam = attackerTag != null ? (int)attackerTag.Team : victimTeam;
                log.Log(new MatchEvent(time, MatchEventType.CoreCaptured, killerTeam, name));
                return;
            }

            bool isTitan = name.StartsWith("Titan_");
            if (isTitan)
            {
                int destroyerTeam = attackerTag != null ? (int)attackerTag.Team : OpposingTeam(victimTeam);
                log.Log(new MatchEvent(time, MatchEventType.TitanDestroyed, destroyerTeam, name));
                log.Log(new MatchEvent(time, MatchEventType.MatchEnd, destroyerTeam, name));
                return;
            }

            bool isTower = name.StartsWith("Tower_");
            if (isTower)
            {
                int destroyerTeam = attackerTag != null ? (int)attackerTag.Team : OpposingTeam(victimTeam);
                log.Log(new MatchEvent(time, MatchEventType.TowerDestroyed, destroyerTeam, name));
                return;
            }

            bool isChampion = victim.GetComponent<EnemyChampionAI>() != null || victim.GetComponent<PlayerController>() != null;
            if (isChampion)
            {
                log.Log(new MatchEvent(time, MatchEventType.ChampionDeath, victimTeam, name));
                if (lastAttacker != null && attackerTag != null)
                    log.Log(new MatchEvent(time, MatchEventType.ChampionKill, (int)attackerTag.Team, lastAttacker.name));
                return;
            }

            bool isMinion = victim.GetComponent<Enigma.Minion.MinionAI>() != null;
            if (isMinion)
            {
                bool killerIsChampion = lastAttacker != null
                    && (lastAttacker.GetComponent<EnemyChampionAI>() != null || lastAttacker.GetComponent<PlayerController>() != null);
                if (killerIsChampion && attackerTag != null)
                    log.Log(new MatchEvent(time, MatchEventType.MinionKill, (int)attackerTag.Team, lastAttacker.name));
                // killer 不明/非チャンピオンなら記録しない
            }
        }

        private static int OpposingTeam(int victimTeamInt)
        {
            var victimTeam = (TeamId)victimTeamInt;
            if (victimTeam == TeamId.Blue) return (int)TeamId.Red;
            if (victimTeam == TeamId.Red) return (int)TeamId.Blue;
            return victimTeamInt; // タワー/タイタンは通常 Blue/Red 所属なのでここに来ない想定
        }
    }
}
