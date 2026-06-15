using NUnit.Framework;
using UnityEngine;
using Enigma.UI;

namespace Enigma.Tests
{
    /// <summary>
    /// GradientBaker の純粋なピクセル生成ロジックを検証する。
    /// 端点色・中間値・配列長・行レイアウト（行 0 = 下端）を確認する。
    /// </summary>
    public sealed class GradientBakerTests
    {
        private const float Eps = 0.01f;

        [Test]
        public void Hex_ParsesRgb()
        {
            var c = GradientBaker.Hex("#C8AA6E");
            Assert.That(c.r, Is.EqualTo(200f / 255f).Within(Eps));
            Assert.That(c.g, Is.EqualTo(170f / 255f).Within(Eps));
            Assert.That(c.b, Is.EqualTo(110f / 255f).Within(Eps));
            Assert.That(c.a, Is.EqualTo(1f).Within(Eps), "alpha 省略時は 1");
        }

        [Test]
        public void Hex_ParsesRgba()
        {
            var c = GradientBaker.Hex("#00000000");
            Assert.That(c.a, Is.EqualTo(0f).Within(Eps), "末尾 00 で alpha 0");
        }

        [Test]
        public void VerticalGradient_HasCorrectLength()
        {
            var px = GradientBaker.VerticalGradient(8, 256, Color.white, Color.black);
            Assert.That(px.Length, Is.EqualTo(8 * 256), "配列長 = width*height");
        }

        [Test]
        public void VerticalGradient_TopRowIsTopColor_BottomRowIsBottomColor()
        {
            var top = GradientBaker.Hex("#131A2A");
            var bottom = GradientBaker.Hex("#0A0E16");
            int w = 8, h = 256;
            var px = GradientBaker.VerticalGradient(w, h, top, bottom);

            // 行 0 = 下端 → bottom 色
            Assert.That(px[0].r, Is.EqualTo(bottom.r).Within(Eps), "最下行は bottomColor");
            // 行 h-1 = 上端 → top 色
            Assert.That(px[(h - 1) * w].r, Is.EqualTo(top.r).Within(Eps), "最上行は topColor");
        }

        [Test]
        public void VerticalGradient_MidRow_IsBlend()
        {
            var px = GradientBaker.VerticalGradient(1, 3, Color.white, Color.black);
            // h=3, 中央 (y=1) は t=0.5 → グレー
            Assert.That(px[1].r, Is.EqualTo(0.5f).Within(Eps), "中間行は中間色");
        }

        [Test]
        public void HpFillGradient_ShineOnTopRows()
        {
            var top = GradientBaker.Hex("#34D567");
            var bottom = GradientBaker.Hex("#15803D");
            var shine = GradientBaker.Hex("#A8F0BF");
            int w = 8, h = 32, shineRows = 2;
            var px = GradientBaker.HpFillGradient(w, h, top, bottom, shine, shineRows);

            // 最上段 (行 h-1, h-2) が shine
            Assert.That(px[(h - 1) * w].r, Is.EqualTo(shine.r).Within(Eps), "最上行 shine");
            Assert.That(px[(h - 2) * w].r, Is.EqualTo(shine.r).Within(Eps), "上から2行目 shine");
            // 最下行は bottom のまま
            Assert.That(px[0].r, Is.EqualTo(bottom.r).Within(Eps), "最下行は bottom");
        }

        [Test]
        public void HorizontalCenterGlow_EdgesAreTransparent_CenterOpaque()
        {
            var center = GradientBaker.Hex("#C8AA6E");
            int w = 256, h = 8;
            var px = GradientBaker.HorizontalCenterGlow(w, h, center);

            Assert.That(px[0].a, Is.EqualTo(0f).Within(Eps), "左端 alpha 0");
            Assert.That(px[w - 1].a, Is.EqualTo(0f).Within(Eps), "右端 alpha 0");
            // 中央付近 alpha は 1 近傍
            int mid = w / 2;
            Assert.That(px[mid].a, Is.GreaterThan(0.95f), "中央 alpha ほぼ 1");
        }

        [Test]
        public void RadialGlow_CenterOpaque_CornerTransparent()
        {
            var center = new Color(0.78f, 0.66f, 0.43f, 0.5f);
            int size = 128;
            var px = GradientBaker.RadialGlow(size, center);

            int c = size / 2;
            Assert.That(px[c * size + c].a, Is.EqualTo(0.5f).Within(0.02f), "中心 alpha = 中心色 alpha");
            Assert.That(px[0].a, Is.EqualTo(0f).Within(Eps), "角(最遠) alpha 0");
        }

        [Test]
        public void ValueNoiseTexture_HasCorrectLength()
        {
            var px = GradientBaker.ValueNoiseTexture(
                64, Color.black, Color.white, 8, 2, seed: 1);
            Assert.That(px.Length, Is.EqualTo(64 * 64), "配列長 = size*size");
        }

        [Test]
        public void ValueNoiseTexture_IsTileable_EdgesMatch()
        {
            int size = 64;
            var px = GradientBaker.ValueNoiseTexture(
                size, Color.black, Color.white, 8, 2, seed: 7);

            // タイル可能なら 左端列と「右端の1つ外（= 左端へラップ）」が連続する。
            // 実装は周期 size で格子をラップするため、x=0 と x=size の値は一致する。
            // テクスチャ上では x=size-1 → x=0 へ折り返しても破綻が小さいことを確認する。
            for (int y = 0; y < size; y++)
            {
                // 左端と右端の差が大きすぎない（高周波の段差が無い）ことを確認
                float left  = px[y * size + 0].r;
                float right = px[y * size + (size - 1)].r;
                Assert.That(Mathf.Abs(left - right), Is.LessThan(0.5f),
                    $"行 {y}: 左端と右端のノイズ値が大きく不連続（タイル不可）");
            }
        }

        [Test]
        public void ValueNoiseTexture_ValuesWithinColorRange()
        {
            var a = new Color(0.2f, 0.3f, 0.4f);
            var b = new Color(0.8f, 0.9f, 1.0f);
            var px = GradientBaker.ValueNoiseTexture(32, a, b, 4, 2, seed: 3);

            float min = Mathf.Min(a.r, b.r);
            float max = Mathf.Max(a.r, b.r);
            foreach (var c in px)
            {
                Assert.That(c.r, Is.GreaterThanOrEqualTo(min - Eps));
                Assert.That(c.r, Is.LessThanOrEqualTo(max + Eps));
            }
        }

        [Test]
        public void ValueNoiseTexture_IsDeterministic()
        {
            var p1 = GradientBaker.ValueNoiseTexture(16, Color.black, Color.white, 4, 2, seed: 5);
            var p2 = GradientBaker.ValueNoiseTexture(16, Color.black, Color.white, 4, 2, seed: 5);
            for (int i = 0; i < p1.Length; i++)
                Assert.That(p1[i].r, Is.EqualTo(p2[i].r).Within(1e-6f), $"index {i} が非決定論的");
        }

        [Test]
        public void UpTriangle_ApexNarrow_BaseWide()
        {
            int size = 32;
            var px = GradientBaker.UpTriangle(size, Color.white);
            Assert.That(px.Length, Is.EqualTo(size * size));

            // 最下行 (y=0) は中央付近が広く塗られる
            int baseFilled = 0;
            for (int x = 0; x < size; x++) if (px[x].a > 0.5f) baseFilled++;
            // 最上行 (y=size-1) はほぼ頂点のみ
            int apexFilled = 0;
            for (int x = 0; x < size; x++) if (px[(size - 1) * size + x].a > 0.5f) apexFilled++;

            Assert.That(baseFilled, Is.GreaterThan(apexFilled), "底辺(下端)は頂点(上端)より幅広");
        }

        [Test]
        public void GrassBladeTexture_HasCorrectLength()
        {
            var px = GradientBaker.GrassBladeTexture(128);
            Assert.That(px.Length, Is.EqualTo(128 * 128), "配列長 = size*size");
        }

        [Test]
        public void GrassBladeTexture_HasTransparentAndOpaquePixels()
        {
            var px = GradientBaker.GrassBladeTexture(128);
            bool hasZeroAlpha    = false;
            bool hasNonZeroAlpha = false;
            foreach (var c in px)
            {
                if (c.a <= 0.001f) hasZeroAlpha = true;
                else hasNonZeroAlpha = true;
                if (hasZeroAlpha && hasNonZeroAlpha) break;
            }
            Assert.That(hasZeroAlpha,    Is.True, "透明背景(alpha 0)の画素が存在する");
            Assert.That(hasNonZeroAlpha, Is.True, "草の葉(alpha 非0)の画素が存在する");
        }
    }
}
