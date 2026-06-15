using UnityEngine;

namespace Enigma.Character
{
    /// <summary>
    /// マップ境界に対する場外判定・救出地点算出の純粋関数ユーティリティ。
    /// 衝突チューブの真の境界半径（レーン外周 51.8、ベースポケット外周 11.4）と整合させる。
    /// </summary>
    public static class OutOfBoundsLogic
    {
        // 衝突チューブの外殻半径と一致させる（BuildAetherRiftMap の TubeLaneOuterR / TubePocketInnerR）
        private const float LaneOuterRadius   = 51.8f;
        // ベースポケット拡張（14.4→17.4）に追従。拡張ベース内が場外判定されないようにする
        private const float PocketInnerRadius = 17.4f;
        private const float BaseOffsetX       = 56f;
        // 救出先の半径（レーンアーク中央 R=45 付近）
        private const float LaneRescueRadius  = 45f;

        /// <summary>
        /// XZ 中心距離が境界外、かつ両ベースポケット内でもない場合に場外とみなす。
        /// ポケット内（壁の内側で守られている）はプレイ可能領域なので場外ではない。
        /// </summary>
        public static bool IsOutOfBounds(float x, float z)
        {
            float distCenter = Mathf.Sqrt(x * x + z * z);
            if (distCenter <= LaneOuterRadius) return false;

            // 両ベース中心 (±56, 0) からの距離がポケット半径以内ならプレイ領域
            float dBlue = Mathf.Sqrt((x + BaseOffsetX) * (x + BaseOffsetX) + z * z);
            float dRed  = Mathf.Sqrt((x - BaseOffsetX) * (x - BaseOffsetX) + z * z);
            if (dBlue <= PocketInnerRadius || dRed <= PocketInnerRadius) return false;

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
