using UnityEngine;

namespace Enigma.UI
{
    /// <summary>
    /// UI 用グラデーション・装飾テクスチャのピクセル配列を生成する純粋関数群。
    /// UnityEngine.Color のみ参照し、Texture2D / AssetDatabase には依存しないため
    /// EditMode テストで端点色・中間値・配列長を直接検証できる。
    /// </summary>
    /// <remarks>
    /// 返す配列は Texture2D.SetPixels 互換のレイアウト（row-major, 行 0 = テクスチャ下端）。
    /// 縦グラデは「上端色 (topColor)」をテクスチャ上端（= 行 height-1）に置く。
    /// </remarks>
    public static class GradientBaker
    {
        /// <summary>16進カラー（"#RRGGBB" or "#RRGGBBAA" / 先頭 # 省略可）を Color へ。</summary>
        public static Color Hex(string hex)
        {
            if (string.IsNullOrEmpty(hex)) return Color.magenta;
            if (hex[0] == '#') hex = hex.Substring(1);

            byte r = ParseByte(hex, 0);
            byte g = ParseByte(hex, 2);
            byte b = ParseByte(hex, 4);
            byte a = hex.Length >= 8 ? ParseByte(hex, 6) : (byte)255;

            return new Color(r / 255f, g / 255f, b / 255f, a / 255f);
        }

        private static byte ParseByte(string hex, int offset)
        {
            int hi = HexDigit(hex[offset]);
            int lo = HexDigit(hex[offset + 1]);
            return (byte)((hi << 4) | lo);
        }

        private static int HexDigit(char c)
        {
            if (c >= '0' && c <= '9') return c - '0';
            if (c >= 'a' && c <= 'f') return c - 'a' + 10;
            if (c >= 'A' && c <= 'F') return c - 'A' + 10;
            return 0;
        }

        /// <summary>
        /// 縦グラデーション。topColor を上端、bottomColor を下端に置く。
        /// 配列は row-major（行 0 = 下端）なので、行 0 が bottomColor、行 height-1 が topColor。
        /// </summary>
        public static Color[] VerticalGradient(int width, int height, Color topColor, Color bottomColor)
        {
            var px = new Color[width * height];
            for (int y = 0; y < height; y++)
            {
                // t=0 を最下行 (bottomColor)、t=1 を最上行 (topColor) に。
                float t = height <= 1 ? 1f : (float)y / (height - 1);
                Color row = Color.Lerp(bottomColor, topColor, t);
                int rowStart = y * width;
                for (int x = 0; x < width; x++)
                    px[rowStart + x] = row;
            }
            return px;
        }

        /// <summary>
        /// HP バー塗り。上端 top → 下端 bottom の縦グラデに、最上段 shineRows 行を shine 色で上書き。
        /// </summary>
        public static Color[] HpFillGradient(int width, int height, Color top, Color bottom, Color shine, int shineRows)
        {
            var px = VerticalGradient(width, height, top, bottom);
            // 行 0 = 下端なので、最上段は行 height-1 から下方向へ shineRows 行。
            for (int r = 0; r < shineRows && r < height; r++)
            {
                int y = height - 1 - r;
                int rowStart = y * width;
                for (int x = 0; x < width; x++)
                    px[rowStart + x] = shine;
            }
            return px;
        }

        /// <summary>
        /// 横グラデ装飾。両端 alpha 0、中央 centerColor（centerColor の alpha を保持）。
        /// 左右対称の三角プロファイルで alpha を補間する。
        /// </summary>
        public static Color[] HorizontalCenterGlow(int width, int height, Color centerColor)
        {
            var px = new Color[width * height];
            float half = (width - 1) * 0.5f;
            for (int x = 0; x < width; x++)
            {
                // 中央で 1、両端で 0。
                float d = half <= 0f ? 1f : 1f - Mathf.Abs(x - half) / half;
                d = Mathf.Clamp01(d);
                var c = new Color(centerColor.r, centerColor.g, centerColor.b, centerColor.a * d);
                for (int y = 0; y < height; y++)
                    px[y * width + x] = c;
            }
            return px;
        }

        /// <summary>
        /// 放射グロー。中心 centerColor（alpha 含む）→ 外周 alpha 0。半径方向に線形減衰。
        /// </summary>
        public static Color[] RadialGlow(int size, Color centerColor)
        {
            var px = new Color[size * size];
            float c = (size - 1) * 0.5f;
            float maxR = c <= 0f ? 1f : c;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - c;
                    float dy = y - c;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01(1f - r / maxR);
                    px[y * size + x] = new Color(centerColor.r, centerColor.g, centerColor.b, centerColor.a * a);
                }
            }
            return px;
        }

        /// <summary>
        /// タイル可能なバリューノイズテクスチャ。色 a〜b を雲状ノイズで補間する。
        /// Mathf.PerlinNoise はタイル不可なので、格子点を size 周期でラップしてハッシュし、
        /// smoothstep 補間で滑らかにつなぐ。これにより左右端・上下端が連続する。
        /// </summary>
        /// <param name="size">正方テクスチャの一辺（ピクセル）</param>
        /// <param name="a">ノイズ値 0 側の色</param>
        /// <param name="b">ノイズ値 1 側の色</param>
        /// <param name="cells">基本周波数の格子分割数（タイル境界で連続）</param>
        /// <param name="octaves">重ね合わせるオクターブ数（各段で周波数倍・振幅半減）</param>
        /// <param name="seed">決定論的なハッシュシード</param>
        public static Color[] ValueNoiseTexture(
            int size, Color a, Color b, int cells, int octaves, int seed = 0)
        {
            if (size < 1) size = 1;
            if (cells < 1) cells = 1;
            if (octaves < 1) octaves = 1;

            var px = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    // [0,1) の UV。size を周期とすることでテクスチャ端が連続する。
                    float u = (float)x / size;
                    float v = (float)y / size;

                    float sum   = 0f;
                    float amp   = 1f;
                    float ampSum = 0f;
                    int   freq  = cells;
                    for (int o = 0; o < octaves; o++)
                    {
                        sum    += amp * TileableValue(u, v, freq, seed + o * 131);
                        ampSum += amp;
                        amp    *= 0.5f;
                        freq   *= 2;
                    }
                    float n = ampSum > 0f ? sum / ampSum : 0f; // [0,1] に正規化
                    px[y * size + x] = Color.Lerp(a, b, Mathf.Clamp01(n));
                }
            }
            return px;
        }

        /// <summary>
        /// 周期 period の格子上でタイル可能なバリューノイズを 1 サンプル返す（[0,1]）。
        /// u,v は [0,1)。格子座標を period でラップして整数ハッシュし、smoothstep 補間。
        /// </summary>
        private static float TileableValue(float u, float v, int period, int seed)
        {
            float fx = u * period;
            float fy = v * period;
            int x0 = Mathf.FloorToInt(fx);
            int y0 = Mathf.FloorToInt(fy);
            float tx = fx - x0;
            float ty = fy - y0;

            // 格子点を period でラップ → 端と端が同じハッシュ値になりタイル化する
            int x0w = ((x0 % period) + period) % period;
            int y0w = ((y0 % period) + period) % period;
            int x1w = (x0w + 1) % period;
            int y1w = (y0w + 1) % period;

            float c00 = Hash01(x0w, y0w, seed);
            float c10 = Hash01(x1w, y0w, seed);
            float c01 = Hash01(x0w, y1w, seed);
            float c11 = Hash01(x1w, y1w, seed);

            float sx = tx * tx * (3f - 2f * tx); // smoothstep
            float sy = ty * ty * (3f - 2f * ty);
            float top = Mathf.Lerp(c00, c10, sx);
            float bot = Mathf.Lerp(c01, c11, sx);
            return Mathf.Lerp(top, bot, sy);
        }

        /// <summary>整数格子点を決定論的に [0,1] へハッシュする。</summary>
        private static float Hash01(int x, int y, int seed)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263 + seed * 2246822519);
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (h & 0xFFFFFF) / (float)0x1000000; // [0,1)
            }
        }

        /// <summary>
        /// 上向き白三角アイコン（背景透明）。頂点が上端中央、底辺が下端。
        /// </summary>
        public static Color[] UpTriangle(int size, Color fill)
        {
            var px = new Color[size * size]; // 既定 (0,0,0,0)
            float cx = (size - 1) * 0.5f;
            for (int y = 0; y < size; y++)
            {
                // 行 0 = 下端。下端ほど三角は幅広、上端（y=size-1）で幅 0。
                // 上端からの距離 = (size-1 - y)、これに比例して半幅が広がる。
                float fromTop = (size - 1) - y;
                float halfWidth = (fromTop / (size - 1)) * ((size - 1) * 0.5f);
                int rowStart = y * size;
                for (int x = 0; x < size; x++)
                {
                    if (Mathf.Abs(x - cx) <= halfWidth)
                        px[rowStart + x] = fill;
                }
            }
            return px;
        }

        /// <summary>
        /// 草むらタフト用の RGBA テクスチャ。透明背景に細い三角ブレードを 5〜7 本描く。
        /// 各ブレードは根本（下端）が太く先端（上端）で幅 0 に収束し、緑の縦グラデを持つ。
        /// 決定論的（内部ハッシュのみで乱数源を使わない）なのでベイク結果は常に同一。
        /// 返す配列は行 0 = 下端（テクスチャ下端 = 草の根本）。
        /// </summary>
        public static Color[] GrassBladeTexture(int size)
        {
            var px = new Color[size * size]; // 既定 (0,0,0,0) = 透明背景

            // ブレード本数は size から決定論的に 5〜7 本へ。
            int bladeCount = 5 + (Hash01Int(size, 911) % 3); // 5,6,7

            var rootX   = new float[bladeCount];
            var tipX    = new float[bladeCount];
            var topY    = new float[bladeCount];
            var rootHalf = new float[bladeCount];
            for (int b = 0; b < bladeCount; b++)
            {
                // 根本 x はテクスチャ幅にほぼ均等分布させ、ハッシュで微小に揺らす。
                float spread = (b + 0.5f) / bladeCount;
                rootX[b] = (spread + (Hash01(b, 0, 71) - 0.5f) * 0.12f) * (size - 1);
                // 先端は根本から横へ傾ける（風に揺れた葉のシルエット）。
                tipX[b]  = rootX[b] + (Hash01(b, 1, 73) - 0.5f) * size * 0.45f;
                // 葉の高さは上端の 70〜100%。
                topY[b]  = (0.7f + Hash01(b, 2, 79) * 0.3f) * (size - 1);
                // 根本の半幅（先端へ向け 0 に収束）。
                rootHalf[b] = (0.018f + Hash01(b, 3, 83) * 0.02f) * size;
            }

            for (int y = 0; y < size; y++)
            {
                int rowStart = y * size;
                for (int x = 0; x < size; x++)
                {
                    for (int b = 0; b < bladeCount; b++)
                    {
                        if (y > topY[b]) continue; // 葉先より上は描かない
                        float t = topY[b] <= 0f ? 0f : y / topY[b]; // 0=根本, 1=先端
                        // 根本から先端へ x を補間し、半幅は先端で 0 に。
                        float centerX = Mathf.Lerp(rootX[b], tipX[b], t);
                        float half    = rootHalf[b] * (1f - t);
                        if (half <= 0f) continue;
                        if (Mathf.Abs(x - centerX) <= half)
                        {
                            // 縦グラデ: 根本は濃い緑、先端は明るい黄緑。
                            var col = Color.Lerp(
                                new Color(0.16f, 0.38f, 0.14f, 1f),
                                new Color(0.42f, 0.70f, 0.30f, 1f), t);
                            px[rowStart + x] = col;
                        }
                    }
                }
            }
            return px;
        }

        private static int Hash01Int(int a, int seed)
        {
            unchecked
            {
                int h = seed * 73856093 ^ a * 19349663;
                return h & 0x7FFFFFFF;
            }
        }
    }
}
