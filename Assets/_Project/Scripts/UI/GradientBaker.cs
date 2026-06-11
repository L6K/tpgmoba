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
    }
}
