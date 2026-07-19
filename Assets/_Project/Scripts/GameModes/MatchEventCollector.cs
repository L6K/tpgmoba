using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Enigma.Combat;
using Enigma.Core;
using Enigma.Character;
using Enigma.Objective;

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
            var all = Object.FindObjectsByType<HealthComponent>(FindObjectsSortMode.None);
            foreach (var hc in all)
            {
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
            var victimPos = victim.transform.position;

            // NeutralBossController はボスの GameObject にのみ付与される([RequireComponent(HealthComponent)])ため、
            // 生死やスキャンタイミングに依存せず確実にボスを判定できる。
            // (旧実装は 1Hz ポーリングで「今アクティブなボス」と一致した時だけ記憶する方式だったため、
            //  ボスがポーリング間隔1秒未満で撃破されると一度も一致せず CoreCaptured が欠落していた)
            bool isBoss = victim.GetComponent<NeutralBossController>() != null;
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
                log.Log(new MatchEvent(time, MatchEventType.ChampionDeath, victimTeam, name, victimPos.x, victimPos.z));
                if (lastAttacker != null && attackerTag != null)
                    log.Log(new MatchEvent(time, MatchEventType.ChampionKill, (int)attackerTag.Team, lastAttacker.name, victimPos.x, victimPos.z));
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
