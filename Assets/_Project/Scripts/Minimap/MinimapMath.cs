using UnityEngine;

namespace Enigma.Minimap
{
    /// <summary>
    /// ワールド座標 ↔ ミニマップパネル座標の純粋変換関数群。
    /// MonoBehaviour に依存しないため EditMode テストで直接検証できる。
    /// </summary>
    public static class MinimapMath
    {
        /// <summary>
        /// ワールド座標をミニマップパネル内ピクセル座標に変換する。
        /// </summary>
        /// <param name="worldPos">ワールド座標（Y は無視）</param>
        /// <param name="worldBounds">
        /// マップ範囲を Rect で表す。x = xMin, y = zMin, width = xRange, height = zRange。
        /// 標準値: Rect(-100, -70, 200, 140)
        /// </param>
        /// <param name="panelSize">ミニマップパネルのピクセルサイズ（例: Vector2(220, 154)）</param>
        /// <returns>
        /// パネル内ピクセル座標（左上原点）。
        /// 北(+Z) が上 = z が大きいほど y が小さい。範囲外はパネル境界にクランプ。
        /// </returns>
        public static Vector2 WorldToMap(Vector3 worldPos, Rect worldBounds, Vector2 panelSize)
        {
            // X 軸: xMin → 0、xMax → panelSize.x
            float tx = Mathf.InverseLerp(worldBounds.xMin, worldBounds.xMax, worldPos.x);

            // Z 軸: zMax(北) → 0、zMin(南) → panelSize.y（左上原点なので反転）
            float tz = Mathf.InverseLerp(worldBounds.yMax, worldBounds.yMin, worldPos.z);

            float px = Mathf.Clamp(tx * panelSize.x, 0f, panelSize.x);
            float py = Mathf.Clamp(tz * panelSize.y, 0f, panelSize.y);

            return new Vector2(px, py);
        }
    }
}
