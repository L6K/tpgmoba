using System.Collections.Generic;
using UnityEngine;
using Enigma.Data;

namespace Enigma.Map
{
    public sealed class ToppleWavePlanner
    {
        private readonly IRandomSource _random;

        public ToppleWavePlanner(IRandomSource random)
        {
            _random = random;
        }

        // 各木の倒れ開始遅延(秒)。震源からの XZ 平面距離 / 波速 + ジッタ。
        // waveSpeed は 0.01 以下なら 0.01 にクランプ(0除算・Infinity 防止)。
        // jitter = _random.Next(1000) / 999f * Mathf.Max(0f, maxJitterSeconds)
        public float[] PlanDelays(
            Vector3 epicenter,
            IReadOnlyList<Vector3> treePositions,
            float waveSpeed,
            float maxJitterSeconds)
        {
            float clampedSpeed = Mathf.Max(0.01f, waveSpeed);
            float clampedMaxJitter = Mathf.Max(0f, maxJitterSeconds);

            var delays = new float[treePositions.Count];
            for (int i = 0; i < treePositions.Count; i++)
            {
                var diff = treePositions[i] - epicenter;
                float xzDist = Mathf.Sqrt(diff.x * diff.x + diff.z * diff.z);
                float jitter = _random.Next(1000) / 999f * clampedMaxJitter;
                delays[i] = xzDist / clampedSpeed + jitter;
            }
            return delays;
        }

        // 木を震源から外向きに倒すための回転軸(ワールド水平軸)。
        // d = treePosition - epicenter を y=0 で正規化。d がほぼゼロ(sqrMagnitude < 1e-6)なら d = Vector3.forward。
        // 戻り値 = Vector3.Cross(Vector3.up, d).normalized
        public static Vector3 ToppleAxis(Vector3 epicenter, Vector3 treePosition)
        {
            var diff = treePosition - epicenter;
            diff.y = 0f;

            if (diff.sqrMagnitude < 1e-6f)
                diff = Vector3.forward;
            else
                diff = diff.normalized;

            return Vector3.Cross(Vector3.up, diff).normalized;
        }
    }
}
