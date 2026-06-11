using System.Collections.Generic;
using UnityEngine;

namespace Enigma.Ability
{
    /// <summary>
    /// キャスト演出用の軽量バースト/トレイルを生成する static ヘルパー。
    /// マテリアルは色ごとに static キャッシュし、ランタイムの無駄な生成を避ける。
    /// </summary>
    public static class SkillVfx
    {
        // 色ごとの透過 Unlit マテリアルを使い回す（GC・生成コスト削減）
        private static readonly Dictionary<Color, Material> _matCache = new();

        /// <summary>
        /// 指定位置に球状のバーストを生成し、拡大フェードして自壊させる。
        /// </summary>
        public static void SpawnBurst(Vector3 pos, Color color, float startScale, float endScale, float life)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "SkillBurst";

            // 当たり判定は不要なので除去（演出専用）
            var col = go.GetComponent<Collider>();
            if (col != null) Object.Destroy(col);

            go.transform.position   = pos;
            go.transform.localScale = Vector3.one * startScale;

            var renderer = go.GetComponent<MeshRenderer>();
            renderer.shadowCastingMode    = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows       = false;
            renderer.sharedMaterial       = GetTransparentMaterial(color);

            var fade = go.AddComponent<VfxFade>();
            fade.Begin(color, startScale, endScale, life);
        }

        /// <summary>飛翔体などに付ける細いトレイルを生成する。マテリアルはバーストと共用。</summary>
        public static TrailRenderer AddTrail(GameObject target, Color color, float startWidth, float time)
        {
            var trail = target.AddComponent<TrailRenderer>();
            trail.time           = time;
            trail.startWidth     = startWidth;
            trail.endWidth       = 0f;
            trail.numCapVertices = 2;
            trail.material       = GetTransparentMaterial(color);
            trail.startColor     = color;
            // 末端を透明に落として尾を自然に消す
            var tail = color;
            tail.a = 0f;
            trail.endColor = tail;
            return trail;
        }

        // URP/Unlit を透過モードに寄せたマテリアルを取得（色ごとにキャッシュ）
        private static Material GetTransparentMaterial(Color color)
        {
            if (_matCache.TryGetValue(color, out var cached) && cached != null)
                return cached;

            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            var mat    = new Material(shader);

            // URP Unlit を Transparent 相当に設定（Surface=Transparent, Blend=Alpha）
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", 0f);
            mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_ZWrite", 0f);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.renderQueue = 3000;
            mat.SetOverrideTag("RenderType", "Transparent");

            mat.color = color;
            if (mat.HasProperty("_BaseColor"))
                mat.SetColor("_BaseColor", color);

            _matCache[color] = mat;
            return mat;
        }
    }

    /// <summary>バースト球を拡大しつつフェードアウトさせ、寿命終了で自壊する補助 MonoBehaviour。</summary>
    public sealed class VfxFade : MonoBehaviour
    {
        private Color   _color;
        private float   _startScale;
        private float   _endScale;
        private float   _life;
        private float   _elapsed;
        private MaterialPropertyBlock _mpb;
        private MeshRenderer _renderer;

        public void Begin(Color color, float startScale, float endScale, float life)
        {
            _color      = color;
            _startScale = startScale;
            _endScale   = endScale;
            _life       = life > 0f ? life : 0.25f;
            _elapsed    = 0f;
            _renderer   = GetComponent<MeshRenderer>();
            _mpb        = new MaterialPropertyBlock();
        }

        private void Update()
        {
            _elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(_elapsed / _life);

            transform.localScale = Vector3.one * Mathf.Lerp(_startScale, _endScale, t);

            // アルファを 1→0 に落とす。共有マテリアルを汚さないよう MPB で個別制御
            if (_renderer != null && _mpb != null)
            {
                var c = _color;
                c.a = Mathf.Lerp(_color.a, 0f, t);
                _renderer.GetPropertyBlock(_mpb);
                _mpb.SetColor("_BaseColor", c);
                _mpb.SetColor("_Color", c);
                _renderer.SetPropertyBlock(_mpb);
            }

            if (t >= 1f)
                Destroy(gameObject);
        }
    }
}
