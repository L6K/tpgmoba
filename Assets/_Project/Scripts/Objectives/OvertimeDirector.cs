using System.Collections.Generic;
using UnityEngine;
using Enigma.Combat;

namespace Enigma.Objective
{
    // オーバータイム進行役(Humble Object)。一定時間経過後、全タワー/タイタンへ
    // OvertimeDecayLogic の減衰を毎秒適用し、試合が必ず決着するようにする。
    public sealed class OvertimeDirector : MonoBehaviour
    {
        [SerializeField] private float _overtimeStartSeconds = OvertimeDecayLogic.DefaultOvertimeStartSeconds;

        private readonly List<HealthComponent> _towers = new();
        private HealthComponent _titanBlue;
        private HealthComponent _titanRed;
        private float _tickTimer;

        private void Start()
        {
            // タワー(TowerAttack 持ち)を収集する
            foreach (var t in FindObjectsByType<TowerAttack>(FindObjectsSortMode.None))
            {
                var hc = t.GetComponent<HealthComponent>();
                if (hc != null) _towers.Add(hc);
            }
            // タイタンは同時死タイブレークの対象になるため個別に保持する
            _titanBlue = GameObject.Find("Titan_Blue")?.GetComponent<HealthComponent>();
            _titanRed = GameObject.Find("Titan_Red")?.GetComponent<HealthComponent>();
        }

        private void Update()
        {
            _tickTimer += Time.deltaTime;
            if (_tickTimer < 1f) return;
            _tickTimer -= 1f;

            float elapsed = Time.timeSinceLevelLoad;

            foreach (var hc in _towers)
            {
                if (hc == null || hc.Model.IsDead) continue;
                float dmg = OvertimeDecayLogic.DamagePerSecond(hc.Model.MaxHp, elapsed, _overtimeStartSeconds);
                if (dmg > 0f) hc.TakeDamage(dmg);
            }

            ApplyTitanDecay(elapsed);
        }

        // タイタン2体は「この tick の減衰で両方とも致死になる」場合のみタイブレークを介する。
        // 片方だけ致死/どちらも非致死なら従来どおり両方へ通常減衰を適用する。
        private void ApplyTitanDecay(float elapsed)
        {
            bool blueAlive = _titanBlue != null && !_titanBlue.Model.IsDead;
            bool redAlive = _titanRed != null && !_titanRed.Model.IsDead;
            if (!blueAlive && !redAlive) return;

            float blueDmg = blueAlive
                ? OvertimeDecayLogic.DamagePerSecond(_titanBlue.Model.MaxHp, elapsed, _overtimeStartSeconds)
                : 0f;
            float redDmg = redAlive
                ? OvertimeDecayLogic.DamagePerSecond(_titanRed.Model.MaxHp, elapsed, _overtimeStartSeconds)
                : 0f;

            bool blueWouldDie = blueAlive && blueDmg > 0f && _titanBlue.Model.CurrentHp <= blueDmg;
            bool redWouldDie = redAlive && redDmg > 0f && _titanRed.Model.CurrentHp <= redDmg;

            if (blueWouldDie && redWouldDie)
            {
                // 両方とも致死: 敗者チームのタイタンだけに減衰を適用し、勝者タイタンは
                // このtickでは温存する(MatchEnd の帰属を正しくするため)。
                // コインは Random で引く。frameCount 偶奇だと同一タイムラインの連続シム試合で
                // 毎回同じ側に倒れる(バッチで Red 連勝を実測)。Random 状態は試合ごとの戦闘で
                // 進むため実質的に試合ごとに変わる。
                int loserTeam = OvertimeTieBreakLogic.PickLoserTeam(
                    CountAliveTowers(TeamId.Blue), CountAliveTowers(TeamId.Red),
                    SumStructureHp(TeamId.Blue), SumStructureHp(TeamId.Red),
                    UnityEngine.Random.value < 0.5f);

                if (loserTeam == (int)TeamId.Blue) _titanBlue.TakeDamage(blueDmg);
                else _titanRed.TakeDamage(redDmg);
                return;
            }

            if (blueAlive && blueDmg > 0f) _titanBlue.TakeDamage(blueDmg);
            if (redAlive && redDmg > 0f) _titanRed.TakeDamage(redDmg);
        }

        private int CountAliveTowers(TeamId team)
        {
            int count = 0;
            foreach (var hc in _towers)
            {
                if (hc == null || hc.Model.IsDead) continue;
                var tag = hc.GetComponent<TeamTag>();
                if (tag != null && tag.Team == team) count++;
            }
            return count;
        }

        private float SumStructureHp(TeamId team)
        {
            float sum = 0f;
            foreach (var hc in _towers)
            {
                if (hc == null || hc.Model.IsDead) continue;
                var tag = hc.GetComponent<TeamTag>();
                if (tag != null && tag.Team == team) sum += hc.Model.CurrentHp;
            }

            var titan = team == TeamId.Blue ? _titanBlue : _titanRed;
            if (titan != null && !titan.Model.IsDead) sum += titan.Model.CurrentHp;

            return sum;
        }
    }
}
