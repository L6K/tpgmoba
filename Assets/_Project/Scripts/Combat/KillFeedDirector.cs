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

        // マルチキル/ストリーク/シャットダウン判定（純ロジック）。チャンピオン名をIDに使う。
        private readonly MultiKillStreakModel _streaks = new MultiKillStreakModel();
        private const int ShutdownBonusGold = 150;

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

            // マルチキル/ストリーク/シャットダウン判定。キラー不在(環境死)は死亡リセットのみ。
            float now = Time.time;
            KillResult kr = default;
            if (killer != null) kr = _streaks.RegisterKill(killerName, victimName, now);
            else                _streaks.RegisterDeath(victimName, now);

            // プレイヤーが関与したキルはアナウンス + キラー時はゴールド音を鳴らす
            bool playerIsVictim = victim.CompareTag("Player");
            bool playerIsKiller = killer != null && killer.CompareTag("Player");

            if (playerIsKiller)
            {
                GameSfx.PlayUi("gold", 0.8f);

                // レリック「キル時加速」: 所持していれば移動加速を付与する。
                var relics = killer.GetComponentInParent<Enigma.Data.PlayerRelicEffects>();
                if (relics != null && relics.MoveSpeedOnKill > 0f)
                    StatusEffectController.GetOrAdd(killer)
                        .ApplyHaste(relics.MoveSpeedOnKill, Enigma.Data.PlayerRelicEffects.MoveSpeedOnKillDuration);

                // シャットダウン報酬: 連続キル中の敵を倒したらボーナスゴールド。
                if (kr.IsShutdown)
                {
                    var wallet = killer.GetComponentInParent<PlayerWallet>();
                    wallet?.Wallet.Add(ShutdownBonusGold);
                }

                // マルチキル > シャットダウン > ストリーク > 通常キル の優先で1つだけ大きく出す。
                string special = BuildSpecial(kr);
                if (special != null)
                    _hud?.AnnounceSpecial(special, AnnounceGold);
                else
                    _hud?.AnnounceKill(killed: false);
            }
            else if (playerIsVictim)
            {
                _hud?.AnnounceKill(killed: true);
            }
        }

        private static readonly Color AnnounceGold = new Color(0xEB / 255f, 0xC8 / 255f, 0x5A / 255f);

        // KillResult から最も派手な1行を選ぶ。該当なしは null（通常キルアナウンスへ）。
        private static string BuildSpecial(KillResult kr)
        {
            switch (kr.MultiKill)
            {
                case MultiKill.Double: return "ダブルキル!";
                case MultiKill.Triple: return "トリプルキル!";
                case MultiKill.Quadra: return "クアドラキル!";
                case MultiKill.Penta:  return "ペンタキル!";
            }
            if (kr.IsShutdown) return "シャットダウン!";
            // ストリークはちょうど段階のしきい値(3/5/7/9/11連続)に乗った時だけ通知。
            if (kr.StreakCount == 3)  return "連続キル!";
            if (kr.StreakCount == 5)  return "無双!";
            if (kr.StreakCount == 7)  return "止まらない!";
            if (kr.StreakCount == 9)  return "支配!";
            if (kr.StreakCount == 11) return "ゴッドライク!";
            return null;
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
