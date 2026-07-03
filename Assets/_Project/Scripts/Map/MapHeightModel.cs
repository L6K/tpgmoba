using System;

namespace Enigma.Map
{
    /// <summary>
    /// マップ地形の高さを座標から純粋計算する。UnityEngine 非依存(EditMode/実行時どちらからも
    /// 同一の値を返すことを保証するため System.Math のみを使用)。
    /// 地形は「中央クレーター」「川のトレンチ」「基地プラトー」「ジャングル高台ブロブ」の4領域からなり、
    /// 重ならないよう各領域の半径・x範囲は設計時点で計算済み(クレーター r&lt;22、川は22≤r≤54、
    /// プラトーは|x|≥86、高台ブロブは対角パス上の4点(19.4,±41.7)/(-19.4,±41.7)周辺のみ)。
    /// </summary>
    public static class MapHeightModel
    {
        private const float CraterFloorR = 14f;
        private const float CraterRimR   = 22f;
        private const float CraterDepth  = 2.5f;

        private const float RiverHalfWidth = 9f;
        private const float RiverInnerR    = 22f;
        private const float RiverOuterR    = 54f;
        private const float RiverFalloff   = 3f;
        private const float RiverDepth     = 1.2f;

        private const float PlateauInnerX = 86f;
        private const float PlateauOuterX = 92f;
        private const float PlateauHeight = 2.5f;
        private const float RampHalfZ     = 10f;

        // ジャングル高台ブロブ4個(スライスM-B)。中心は対角パス上 Polar(65°,46) 等で、
        // 川縁(|x|<=9)から6m以上・壁r38帯とは半径分離済みのため他領域と重ならない。
        private static readonly (float x, float z)[] PlateauBlobCenters =
        {
            (19.4f,  41.7f),
            (-19.4f, 41.7f),
            (-19.4f, -41.7f),
            (19.4f,  -41.7f),
        };
        private const float BlobFlatR    = 4f;
        private const float BlobFalloffR = 9f;
        private const float BlobHeight   = 1.5f;

        public static float Height(float x, float z)
        {
            float r = (float)Math.Sqrt(x * x + z * z);
            float ax = Math.Abs(x);

            // クレーター: 原点からの半径のみで決まる。他領域より優先(基地プラトーとは
            // ax>=86 かつ r<22 の重なりが生じ得ないよう設計済み)。
            if (r < CraterRimR)
                return CraterHeight(r);

            // 基地プラトー: クレーター・川と x 範囲が重ならない(|x|>=86 は r>=86>54 で川域外)。
            if (ax >= PlateauInnerX)
                return PlateauHeight_(x, z);

            // 川: クレーター外側かつ半径帯 [22,54] 上で x が中央帯にある場合のみ。
            if (ax <= RiverHalfWidth && r >= RiverInnerR && r <= RiverOuterR)
                return RiverHeight(ax, r);

            // ジャングル高台ブロブ: 上記いずれの領域にも該当しない場合のみ判定(設計上重ならない)。
            float blob = BlobHeight_(x, z);
            if (blob != 0f)
                return blob;

            return 0f;
        }

        private static float BlobHeight_(float x, float z)
        {
            foreach (var (bx, bz) in PlateauBlobCenters)
            {
                float dx = x - bx;
                float dz = z - bz;
                float d  = (float)Math.Sqrt(dx * dx + dz * dz);
                if (d <= BlobFlatR) return BlobHeight;
                if (d < BlobFalloffR)
                    return BlobHeight * Smooth01((BlobFalloffR - d) / (BlobFalloffR - BlobFlatR));
            }
            return 0f;
        }

        private static float CraterHeight(float r)
        {
            if (r <= CraterFloorR) return -CraterDepth;
            float t = (r - CraterFloorR) / (CraterRimR - CraterFloorR);
            return -CraterDepth + CraterDepth * Smooth01(t);
        }

        private static float RiverHeight(float ax, float r)
        {
            float fromCenter = (RiverHalfWidth - ax) / RiverFalloff;
            float fromOuter  = (RiverOuterR - r) / RiverFalloff;
            float fromInner  = (r - RiverInnerR) / RiverFalloff;
            float f = Min3(fromCenter, fromOuter, fromInner);
            f = Clamp01(f);
            return -RiverDepth * f;
        }

        private static float PlateauHeight_(float x, float z)
        {
            float ax = Math.Abs(x);
            if (ax >= PlateauOuterX) return PlateauHeight;

            // ランプ(86〜92)は正面ゲート幅(|z|<=10)のみ登れる。それ以外は崖(=登攀不可、降りは許容)。
            if (Math.Abs(z) <= RampHalfZ)
                return PlateauHeight * (ax - PlateauInnerX) / (PlateauOuterX - PlateauInnerX);

            return 0f;
        }

        // 3t^2 - 2t^3 の Hermite 平滑化。範囲外は端値へクランプする。
        private static float Smooth01(float t)
        {
            t = Clamp01(t);
            return t * t * (3f - 2f * t);
        }

        private static float Clamp01(float v)
        {
            if (v < 0f) return 0f;
            if (v > 1f) return 1f;
            return v;
        }

        private static float Min3(float a, float b, float c)
        {
            float m = a < b ? a : b;
            return m < c ? m : c;
        }
    }
}
