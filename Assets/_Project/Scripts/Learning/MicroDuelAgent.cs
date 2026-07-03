using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using Enigma.Character;

namespace Enigma.Learning
{
    // 1v1 ミクロ戦闘の self-play 学習 Agent。同 GO の ArenaFighter を操作する。
    // 観測・行動の契約は Confluence 05 B1 節と一致させること。実戦投入時の MLBotPolicy はこの観測写像を再現する。
    public sealed class MicroDuelAgent : Agent
    {
        [SerializeField] private ArenaFighter _fighter;
        [SerializeField] private ArenaFighter _enemyFighter;
        [SerializeField] private Vector3 _spawnPos;
        [SerializeField] private Vector3 _arenaCenter;
        [SerializeField] private float _arenaRadius = 20f;

        private float _prevMyHpRatio;
        private float _prevEnemyHpRatio;
        private bool _outcomeResolved;

        private const float StepTimePenalty = -0.0005f;

        public override void OnEpisodeBegin()
        {
            // 敵側のリセットは敵 Agent が自分で行う（二重リセット防止のため自分の分のみ）
            _fighter.ResetFighter(_spawnPos);

            _prevMyHpRatio = HpRatio(_fighter);
            _prevEnemyHpRatio = HpRatio(_enemyFighter);
            _outcomeResolved = false;
        }

        public override void CollectObservations(VectorSensor sensor)
        {
            Vector3 myPos = _fighter.transform.position;
            Vector3 enemyPos = _enemyFighter.transform.position;

            Vector2 myFlat = new Vector2(myPos.x, myPos.z);
            Vector2 enemyFlat = new Vector2(enemyPos.x, enemyPos.z);
            Vector2 centerFlat = new Vector2(_arenaCenter.x, _arenaCenter.z);

            Vector2 toEnemy = enemyFlat - myFlat;
            float horizontalDist = toEnemy.magnitude;
            Vector2 toEnemyDir = horizontalDist > 0.0001f ? toEnemy / horizontalDist : Vector2.zero;

            Vector2 myOffset = (myFlat - centerFlat) / Mathf.Max(_arenaRadius, 0.0001f);
            Vector2 enemyOffset = (enemyFlat - centerFlat) / Mathf.Max(_arenaRadius, 0.0001f);

            // 1. 自HP率
            sensor.AddObservation(HpRatio(_fighter));
            // 2. 敵HP率
            sensor.AddObservation(HpRatio(_enemyFighter));
            // 3. AttackReady(0/1)
            sensor.AddObservation(_fighter.AttackReady ? 1f : 0f);
            // 4. 敵との水平距離/_arenaRadius (clamp 0-2)
            sensor.AddObservation(Mathf.Clamp(horizontalDist / Mathf.Max(_arenaRadius, 0.0001f), 0f, 2f));
            // 5-6. 敵方向の正規化ベクトル(x,z)
            sensor.AddObservation(toEnemyDir.x);
            sensor.AddObservation(toEnemyDir.y);
            // 7-8. 自位置の対アリーナ中心オフセット/(x,z)/_arenaRadius
            sensor.AddObservation(myOffset.x);
            sensor.AddObservation(myOffset.y);
            // 9-10. 敵位置の同オフセット(x,z)/_arenaRadius
            sensor.AddObservation(enemyOffset.x);
            sensor.AddObservation(enemyOffset.y);
            // 11. 攻撃CD残り/_attackCooldown(0-1)
            sensor.AddObservation(_fighter.CooldownRemaining01);
        }

        public override void OnActionReceived(ActionBuffers actions)
        {
            float moveX = actions.ContinuousActions[0];
            float moveZ = actions.ContinuousActions[1];
            _fighter.ApplyMove(new Vector2(moveX, moveZ));

            float myHpRatio = HpRatio(_fighter);
            float enemyHpRatio = HpRatio(_enemyFighter);

            // 与ダメ - 被ダメの差分を毎ステップ報酬にする
            float reward = (_prevEnemyHpRatio - enemyHpRatio) - (_prevMyHpRatio - myHpRatio);
            AddReward(reward);
            AddReward(StepTimePenalty);

            _prevMyHpRatio = myHpRatio;
            _prevEnemyHpRatio = enemyHpRatio;
        }

        private void Update()
        {
            if (_outcomeResolved) return;

            if (_enemyFighter.Health.Model.IsDead)
            {
                _outcomeResolved = true;
                AddReward(1f);
                EndEpisode();
                return;
            }

            if (_fighter.Health.Model.IsDead)
            {
                _outcomeResolved = true;
                AddReward(-1f);
                EndEpisode();
            }
        }

        public override void Heuristic(in ActionBuffers actionsOut)
        {
            var continuousActions = actionsOut.ContinuousActions;

            Vector3 myPos = _fighter.transform.position;
            Vector3 enemyPos = _enemyFighter.transform.position;

            var ctx = new MicroContext(
                myX: myPos.x, myZ: myPos.z,
                myHpRatio: HpRatio(_fighter),
                attackRange: 12f,
                attackReady: _fighter.AttackReady,
                isMelee: false,
                targetX: enemyPos.x, targetZ: enemyPos.z,
                targetHpRatio: HpRatio(_enemyFighter),
                hasThreat: true,
                threatX: enemyPos.x, threatZ: enemyPos.z);

            var decision = CombatMicroModel.Decide(in ctx);

            continuousActions[0] = decision.MoveX;
            continuousActions[1] = decision.MoveZ;
        }

        private static float HpRatio(ArenaFighter fighter)
        {
            var model = fighter.Health.Model;
            return model.MaxHp > 0f ? Mathf.Clamp01(model.CurrentHp / model.MaxHp) : 0f;
        }
    }
}
