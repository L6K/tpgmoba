using System.Collections.Generic;
using Enigma.Combat;
using UnityEngine;

namespace Enigma.Character
{
    /// <summary>
    /// タイタンに付与する Humble Object。自チームのタワー生存状態を定期収集し、
    /// レーン開通(1レーン分のタワー全滅)を <see cref="TitanExposureLogic"/> で判定して
    /// 露出ゲート(<see cref="TitanDamageGate"/>)を開閉する。露出するまでは全攻撃者
    /// (プレイヤー/Bot/ミニオン/AoE)のダメージが 0 化される。
    ///
    /// チーム方向: このタイタンは「自チーム(TeamTag 一致)のタワー」に守られており、
    /// 自チームの1レーン分のタワーが全滅したときに露出する。
    /// (EnemyChampionAI では敵タイタンの露出を「敵チーム側タワー」の全滅で判定しており、
    ///  = そのタイタンと同チームのタワー全滅、というセマンティクスを踏襲する。)
    /// </summary>
    [RequireComponent(typeof(HealthComponent))]
    public sealed class TitanGuard : MonoBehaviour
    {
        // 露出判定の再評価間隔。タワー集合はシーン中静的なので毎フレーム走査は不要。
        private const float EvaluateInterval = 0.5f;

        private HealthComponent _health;
        private TitanDamageGate _gate;
        private TeamTag _teamTag;

        // 自チームタワーの HealthComponent と、同じ並びの所属レーンID(一度だけ収集)。
        private readonly List<HealthComponent> _ownTowers = new();
        private readonly List<int> _ownTowerLaneId = new();
        private readonly List<(bool isAlive, int laneId)> _exposureBuffer = new();
        private bool _towersCollected;

        private float _nextEvaluateTime;

        private void Awake()
        {
            _health = GetComponent<HealthComponent>();
            _gate = new TitanDamageGate();
            _health.DamageGate = _gate; // 初期は閉(露出前=無敵)
        }

        private void Update()
        {
            if (Time.time < _nextEvaluateTime) return;
            _nextEvaluateTime = Time.time + EvaluateInterval;

            if (!_towersCollected) CollectOwnTowers();

            bool exposed = EvaluateExposed();
            if (_gate.SetExposed(exposed))
            {
                var team = _teamTag != null ? _teamTag.Team : TeamId.Neutral;
                Debug.Log(exposed
                    ? $"[TitanGuard] {team} タイタンが露出しました（レーン開通・ダメージ受付開始）"
                    : $"[TitanGuard] {team} タイタンが再び保護されました（ダメージ遮断）");
            }
        }

        // 自チーム(TeamTag 一致)の Tower_ プレフィックス HealthComponent を一度だけ収集する。
        private void CollectOwnTowers()
        {
            _towersCollected = true;
            _ownTowers.Clear();
            _ownTowerLaneId.Clear();

            _teamTag = GetComponentInParent<TeamTag>();
            TeamId myTeam = _teamTag != null ? _teamTag.Team : TeamId.Neutral;

            var allHealth = Object.FindObjectsByType<HealthComponent>(FindObjectsSortMode.None);
            for (int i = 0; i < allHealth.Length; i++)
            {
                var hc = allHealth[i];
                if (hc == null || !hc.name.StartsWith("Tower_")) continue;

                var tag = hc.GetComponentInParent<TeamTag>();
                if (tag == null || tag.Team != myTeam) continue;

                _ownTowers.Add(hc);
                _ownTowerLaneId.Add(TowerLaneId(hc.name));
            }
        }

        // 自チームタワーの生死+レーンIDから TitanExposureLogic でレーン開通を判定する。
        private bool EvaluateExposed()
        {
            _exposureBuffer.Clear();
            for (int i = 0; i < _ownTowers.Count; i++)
            {
                var hc = _ownTowers[i];
                bool isAlive = hc != null && !hc.Model.IsDead;
                _exposureBuffer.Add((isAlive, _ownTowerLaneId[i]));
            }
            return TitanExposureLogic.IsTitanExposed(_exposureBuffer);
        }

        // タワー名からレーンIDを判定する(命名規則: Tower_[B|R][Top|Bot][Inner|Outer])。
        // EnemyChampionAI.TowerLaneId と同一のセマンティクス(未知命名は負値=単独レーン扱い)。
        private static int TowerLaneId(string towerName)
        {
            if (towerName.Contains("Top")) return 0;
            if (towerName.Contains("Bot")) return 1;
            return -(Mathf.Abs(towerName.GetHashCode()) + 1);
        }
    }
}
