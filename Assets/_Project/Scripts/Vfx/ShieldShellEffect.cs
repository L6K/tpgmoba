using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using Enigma.Ability;

namespace Enigma.Vfx
{
    // シールド付与時の演出: キャラを覆う縦長の殻をフェードアウトさせる
    public static class ShieldShellEffect
    {
        public static void Spawn(GameObject owner, Color color, float lifeSeconds = 0.5f)
        {
            if (owner == null) return;

            var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.name = "ShieldShell";
            sphere.transform.SetParent(owner.transform, worldPositionStays: false);

            var collider = sphere.GetComponent<Collider>();
            if (collider != null) Object.Destroy(collider);

            sphere.transform.localPosition = new Vector3(0f, 0.2f, 0f);
            sphere.transform.localScale = new Vector3(2.2f, 2.8f, 2.2f);

            var renderer = sphere.GetComponent<MeshRenderer>();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.material = SkillVfx.GetTelegraphMaterial(color);

            var fade = sphere.AddComponent<ShieldShellFade>();
            fade.Begin(renderer.material, color, lifeSeconds);
        }

        private sealed class ShieldShellFade : MonoBehaviour
        {
            private const float StartAlpha = 0.45f;

            private Material _material;
            private Color _color;
            private float _lifeSeconds;
            private float _elapsed;

            public void Begin(Material material, Color color, float lifeSeconds)
            {
                _material = material;
                _color = color;
                _lifeSeconds = lifeSeconds;

                var c = _color;
                c.a = StartAlpha;
                if (_material != null) _material.color = c;
            }

            private void Update()
            {
                _elapsed += Time.deltaTime;
                float t = _lifeSeconds > 0f ? Mathf.Clamp01(_elapsed / _lifeSeconds) : 1f;

                if (_material != null)
                {
                    var c = _color;
                    c.a = Mathf.Lerp(StartAlpha, 0f, t);
                    _material.color = c;
                }

                if (t >= 1f) Destroy(gameObject);
            }
        }
    }
}
