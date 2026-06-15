using System.Collections.Generic;
using UnityEngine;
using Enigma.Audio;
using Enigma.Character;
using Enigma.UI;

namespace Enigma.Combat
{
    // シーン内の全チャンピオン（Player + EnemyChampionAI ホスト）の死亡を監視し、
    // KillFeedModel へキル情報を積む司令塔（Humble Object）。LastAttacker からキラーを解決する。
    // 表示は GameHudController に委譲する。
    public sealed class KillFeedDirector : MonoBehaviour
    {
        // ビルダーが SerializedObject で結線する。未設定なら Start で自動解決する
        [SerializeField] private GameHudController _hud;

        private readonly KillFeedModel _model = new KillFeedModel();

        // Died 解除のため購読した (health, handler) を保持する
        private readonly List<(HealthComponent health, System.Action handler)> _subscriptions
            = new List<(HealthComponent, System.Action)>();

        public KillFeedModel Model => _model;

        private void Start()
        {
            if (_hud == null)
                _hud = Object.FindFirstObjectByType<GameHudController>();

            if (_hud != null)
                _hud.BindKillFeed(_model);

            SubscribeChampions();
        }

        private void OnDestroy()
        {
            foreach (var (health, handler) in _subscriptions)
                if (health != null && health.Model != null)
                    health.Model.Died -= handler;
            _subscriptions.Clear();
        }

        // Player（タグ）と EnemyChampionAI ホストのみを購読対象とする。ミニオン/タワーは除外。
        private void SubscribeChampions()
        {
            var healths = Object.FindObjectsByType<HealthComponent>(FindObjectsSortMode.None);
            foreach (var h in healths)
            {
                bool isPlayer = h.CompareTag("Player");
                bool isChampionAi = h.GetComponent<EnemyChampionAI>() != null;
                if (!isPlayer && !isChampionAi) continue;

                var victim = h;
                System.Action handler = () => OnChampionDied(victim);
                h.Model.Died += handler;
                _subscriptions.Add((h, handler));
            }
        }

        private void OnChampionDied(HealthComponent victim)
        {
            var killer = victim.LastAttacker;

            string killerName = killer != null ? ResolveName(killer) : "???";
            string victimName = ResolveName(victim.gameObject);
            TeamId killerTeam = killer != null ? ResolveTeam(killer) : TeamId.Neutral;
            TeamId victimTeam = ResolveTeam(victim.gameObject);

            _model.AddEntry(killerName, victimName, killerTeam, victimTeam);

            // プレイヤーが関与したキルはアナウンス + キラー時はゴールド音を鳴らす
            bool playerIsVictim = victim.CompareTag("Player");
            bool playerIsKiller = killer != null && killer.CompareTag("Player");

            if (playerIsKiller)
            {
                _hud?.AnnounceKill(killed: false);
                GameSfx.PlayUi("gold", 0.8f);
            }
            else if (playerIsVictim)
            {
                _hud?.AnnounceKill(killed: true);
            }
        }

        // 表示名は GameObject 名。プレイヤーは「あなた」に置換する。
        // 弾オーナー等で攻撃者がプレイヤー本体の場合もタグで拾う
        private static string ResolveName(GameObject go)
        {
            if (go.CompareTag("Player")) return "あなた";
            return go.name;
        }

        private static TeamId ResolveTeam(GameObject go)
        {
            var tag = go.GetComponentInParent<TeamTag>();
            return tag != null ? tag.Team : TeamId.Neutral;
        }
    }
}
