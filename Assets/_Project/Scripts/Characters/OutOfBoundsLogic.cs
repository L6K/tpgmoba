using UnityEngine;

namespace Enigma.Character
{
    /// <summary>
    /// マップ境界に対する場外判定・救出地点算出の純粋関数ユーティリティ。
    /// マップは目形(アーモンド)で、上下2つの大円(中心(0,0,∓EyeB)・半径 EyeR)の AND 内側が場内。
    /// BuildAetherRiftMap.CreateOuterBoundary の EyeR / EyeB と一致させる。
    /// </summary>
    public static class OutOfBoundsLogic
    {
        // 目形(アーモンド)を定義する2円。BuildAetherRiftMap の CreateOuterBoundary と一致。
        private const float EyeR = 85f;
        private const float EyeB = 35f;
        // 救出先の半径（レーンアーク中央 R=45 付近、目形内側に確実に収まる）
        private const float LaneRescueRadius = 45f;

        /// <summary>
        /// 目形の内側 = 上まぶた円(中心(0,0,-EyeB))内 ∧ 下まぶた円(中心(0,0,+EyeB))内 のとき場内。
        /// </summary>
        public static bool IsOutOfBounds(float x, float z)
        {
            float r2 = EyeR * EyeR;
            float dUpper = x * x + (z + EyeB) * (z + EyeB);
            float dLower = x * x + (z - EyeB) * (z - EyeB);
            if (dUpper <= r2 && dLower <= r2) return false;
            return true;
        }

        /// <summary>
        /// 同一角度方向の半径 45 地点（レーンアーク上）を返す。
        /// 原点上（角度未定義）の場合は +x 方向へ退避させる。
        /// </summary>
        public static (float x, float z) NearestLanePoint(float x, float z)
        {
            float dist = Mathf.Sqrt(x * x + z * z);
            if (dist < 1e-4f) return (LaneRescueRadius, 0f);

            float nx = x / dist;
            float nz = z / dist;
            return (nx * LaneRescueRadius, nz * LaneRescueRadius);
        }
    }
}
