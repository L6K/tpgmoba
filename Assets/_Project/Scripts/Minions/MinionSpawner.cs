using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Enigma.Combat;

namespace Enigma.Minion
{
    public sealed class MinionSpawner : MonoBehaviour
    {
        [SerializeField] private MinionAI    _minionPrefab;
        [SerializeField] private TeamId      _team;
        [SerializeField] private Transform[] _waypoints;
        [SerializeField] private float       _waveInterval    = 25f;
        [SerializeField] private Material    _teamMaterial;

        // チーム間でウェーブ到着をずらすための初期遅延オフセット(ビルダーが赤側へ設定)。
        // 完全対称だと前線がレーン中央で永久に均衡しタワーまで届かないため、
        // 半周期ずらして前線を振動させ、自然な押し込み(ブレイクスルー)を生む
        [SerializeField] private float       _initialDelayOffset = 0f;

        private const float InitialDelay   = 5f;
        private const int   WaveSize       = 3;
        private const float SpawnStagger   = 0.8f;
        private const int   MaxMinionCount = 60;

        private void Start()
        {
            StartCoroutine(SpawnLoop());
        }

        private IEnumerator SpawnLoop()
        {
            yield return new WaitForSeconds(InitialDelay + _initialDelayOffset);

            while (true)
            {
                // ウェーブ開始時のみ全体ミニオン数チェック（FindObjectsByType はコスト高のため頻度を抑える）
                var existing = Object.FindObjectsByType<MinionAI>(
                    FindObjectsInactive.Exclude, FindObjectsSortMode.None);

                if (existing.Length < MaxMinionCount)
                {
                    yield return StartCoroutine(SpawnWave());
                }

                yield return new WaitForSeconds(_waveInterval);
            }
        }

        private IEnumerator SpawnWave()
        {
            var waypointPositions = BuildWaypointList();

            for (int i = 0; i < WaveSize; i++)
            {
                SpawnMinion(waypointPositions);
                yield return new WaitForSeconds(SpawnStagger);
            }
        }

        private void SpawnMinion(IReadOnlyList<Vector3> waypointPositions)
        {
            if (_minionPrefab == null) return;

            var spawnPos = transform.position;
            // ウェイポイントが存在すれば先頭を開始地点にする
            if (waypointPositions.Count > 0)
                spawnPos = waypointPositions[0];

            var instance = Object.Instantiate(_minionPrefab, spawnPos, Quaternion.identity);
            instance.Initialize(_team, waypointPositions, _teamMaterial);

            // 膠着防止: 試合経過に応じて HP と攻撃力を底上げする。
            // System.DateTime は使わず Time.timeSinceLevelLoad を試合経過時間とする。
            instance.ApplyTimeScaling(MinionScaling.MultiplierAt(Time.timeSinceLevelLoad));
        }

        private List<Vector3> BuildWaypointList()
        {
            var list = new List<Vector3>();
            if (_waypoints == null) return list;

            foreach (var wp in _waypoints)
            {
                if (wp != null) list.Add(wp.position);
            }
            return list;
        }
    }
}
