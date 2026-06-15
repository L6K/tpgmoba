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

        private readonly List<HealthComponent> _structures = new();
        private float _tickTimer;

        private void Start()
        {
            // タワー(TowerAttack 持ち)+タイタン(名前参照)を一度だけ収集する
            foreach (var t in FindObjectsByType<TowerAttack>(FindObjectsSortMode.None))
            {
                var hc = t.GetComponent<HealthComponent>();
                if (hc != null) _structures.Add(hc);
            }
            foreach (var name in new[] { "Titan_Blue", "Titan_Red" })
            {
                var hc = GameObject.Find(name)?.GetComponent<HealthComponent>();
                if (hc != null) _structures.Add(hc);
            }
        }

        private void Update()
        {
            _tickTimer += Time.deltaTime;
            if (_tickTimer < 1f) return;
            _tickTimer -= 1f;

            float elapsed = Time.timeSinceLevelLoad;
            foreach (var hc in _structures)
            {
                if (hc == null || hc.Model.IsDead) continue;
                float dmg = OvertimeDecayLogic.DamagePerSecond(hc.Model.MaxHp, elapsed, _overtimeStartSeconds);
                if (dmg > 0f) hc.TakeDamage(dmg);
            }
        }
    }
}
