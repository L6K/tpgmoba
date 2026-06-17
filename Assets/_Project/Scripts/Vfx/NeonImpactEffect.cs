using System.Collections.Generic;
using UnityEngine;

namespace Enigma.Vfx
{
    /// <summary>
    /// Runtime-built neon impact effect: shock rings, vertical slash arcs, sparks, and a short core pulse.
    /// Drop it into a scene or prefab, assign VFX materials, then call Play().
    /// </summary>
    public sealed class NeonImpactEffect : MonoBehaviour
    {
        [Header("Materials")]
        [SerializeField] private Material ringMaterial;
        [SerializeField] private Material slashMaterial;
        [SerializeField] private Material sparkMaterial;
        [SerializeField] private Material coreMaterial;

        [Header("Timing")]
        [SerializeField] private float duration = 0.72f;
        [SerializeField] private bool playOnEnable = true;
        [SerializeField] private bool destroyOnComplete;

        [Header("Shape")]
        [SerializeField] private Color primary = new Color(0.1f, 0.9f, 1f, 1f);
        [SerializeField] private Color secondary = new Color(1f, 0.2f, 0.85f, 1f);
        [SerializeField] private float radius = 2.4f;
        [SerializeField] private float height = 1.4f;
        [SerializeField] private int sparks = 18;

        private readonly List<LineRenderer> _rings = new List<LineRenderer>();
        private readonly List<LineRenderer> _slashes = new List<LineRenderer>();
        private readonly List<Spark> _sparks = new List<Spark>();
        private Transform _core;
        private Renderer _coreRenderer;
        private float _age;
        private bool _playing;

        public void Play()
        {
            EnsureBuilt();
            _age = 0f;
            _playing = true;
            gameObject.SetActive(true);
            SetVisible(true);
        }

        public void Stop()
        {
            _playing = false;
            SetVisible(false);
        }

        /// <summary>再生前にキャラ別の色と自動破棄を設定する（Spawn から呼ぶ）。</summary>
        public void Configure(Color primaryColor, Color secondaryColor, bool autoDestroy)
        {
            primary = primaryColor;
            secondary = secondaryColor;
            destroyOnComplete = autoDestroy;
        }

        private static NeonImpactEffect _prefabCache;
        private static bool _prefabLoaded;

        /// <summary>
        /// Resources のネオン着弾プレハブを指定位置に生成し、キャラ色で再生する。
        /// 自動破棄つき。プレハブが無ければ何もしない（null を返す）。
        /// </summary>
        public static NeonImpactEffect Spawn(Vector3 position, Color primaryColor, Color secondaryColor)
        {
            if (!_prefabLoaded)
            {
                _prefabCache = Resources.Load<NeonImpactEffect>("Vfx/NeonImpactEffect");
                _prefabLoaded = true;
            }
            if (_prefabCache == null) return null;

            var inst = Instantiate(_prefabCache, position, Quaternion.identity);
            inst.Configure(primaryColor, secondaryColor, autoDestroy: true);
            inst.Play();
            return inst;
        }

        private void OnEnable()
        {
            EnsureBuilt();
            if (playOnEnable)
                Play();
        }

        private void Update()
        {
            if (!_playing)
                return;

            _age += Time.deltaTime;
            float t = duration <= 0f ? 1f : Mathf.Clamp01(_age / duration);
            ApplyFrame(t);

            if (_age >= duration)
            {
                _playing = false;
                if (destroyOnComplete)
                    Destroy(gameObject);
                else
                    SetVisible(false);
            }
        }

        private void EnsureBuilt()
        {
            if (_rings.Count > 0)
                return;

            BuildRings();
            BuildSlashes();
            BuildSparks();
            BuildCore();
            ApplyFrame(0f);
        }

        private void BuildRings()
        {
            for (int i = 0; i < 3; i++)
            {
                LineRenderer ring = CreateLine("ShockRing_" + i, ringMaterial, 96);
                ring.loop = true;
                ring.useWorldSpace = false;
                ring.widthMultiplier = 0.08f + i * 0.035f;
                ring.transform.localRotation = Quaternion.Euler(90f, i * 17f, 0f);
                _rings.Add(ring);
            }
        }

        private void BuildSlashes()
        {
            for (int i = 0; i < 2; i++)
            {
                LineRenderer slash = CreateLine("SlashArc_" + i, slashMaterial, 22);
                slash.loop = false;
                slash.useWorldSpace = false;
                slash.widthMultiplier = 0.12f;
                slash.transform.localRotation = Quaternion.Euler(0f, 35f + i * 110f, 18f - i * 36f);
                _slashes.Add(slash);
            }
        }

        private void BuildSparks()
        {
            int count = Mathf.Max(0, sparks);
            for (int i = 0; i < count; i++)
            {
                LineRenderer spark = CreateLine("Spark_" + i, sparkMaterial, 2);
                spark.loop = false;
                spark.useWorldSpace = false;
                spark.widthMultiplier = 0.045f;

                float angle = i * 137.5f * Mathf.Deg2Rad;
                float up = 0.18f + 0.62f * Hash01(i * 19 + 3);
                Vector3 direction = new Vector3(Mathf.Cos(angle), up, Mathf.Sin(angle)).normalized;
                _sparks.Add(new Spark(spark, direction, 0.75f + Hash01(i * 31 + 7) * 0.9f));
            }
        }

        private void BuildCore()
        {
            GameObject core = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            core.name = "CorePulse";
            core.transform.SetParent(transform, false);
            core.transform.localScale = Vector3.one * 0.35f;
            Destroy(core.GetComponent<Collider>());
            _core = core.transform;
            _coreRenderer = core.GetComponent<Renderer>();
            if (_coreRenderer != null && coreMaterial != null)
                _coreRenderer.sharedMaterial = coreMaterial;
        }

        private LineRenderer CreateLine(string objectName, Material material, int positions)
        {
            var go = new GameObject(objectName);
            go.transform.SetParent(transform, false);
            LineRenderer line = go.AddComponent<LineRenderer>();
            line.positionCount = positions;
            line.material = material;
            line.alignment = LineAlignment.View;
            line.textureMode = LineTextureMode.Stretch;
            line.numCapVertices = 4;
            line.numCornerVertices = 4;
            return line;
        }

        private void ApplyFrame(float t)
        {
            float flash = 1f - Smooth01(t);
            float ringAlpha = Mathf.Clamp01(1f - t * 1.25f);
            float sparkAlpha = Mathf.Clamp01(1f - Mathf.Max(0f, t - 0.12f) / 0.55f);

            ApplyRings(t, ringAlpha);
            ApplySlashes(t, flash);
            ApplySparks(t, sparkAlpha);
            ApplyCore(t, flash);
        }

        private void ApplyRings(float t, float alpha)
        {
            for (int i = 0; i < _rings.Count; i++)
            {
                LineRenderer ring = _rings[i];
                float ringT = Mathf.Clamp01(t * (1.05f + i * 0.18f));
                float scale = Mathf.Lerp(0.2f + i * 0.12f, radius * (1f + i * 0.32f), Smooth01(ringT));
                WriteCircle(ring, scale, i * 0.03f);
                ring.startColor = WithAlpha(Color.Lerp(primary, secondary, i / 2f), alpha * (1f - i * 0.18f));
                ring.endColor = ring.startColor;
                ring.widthMultiplier = Mathf.Lerp(0.14f, 0.025f, ringT);
            }
        }

        private void ApplySlashes(float t, float alpha)
        {
            for (int i = 0; i < _slashes.Count; i++)
            {
                LineRenderer slash = _slashes[i];
                float open = Mathf.Clamp01(t * 1.6f);
                float arcRadius = Mathf.Lerp(0.35f, radius * 0.82f, Smooth01(open));
                WriteArc(slash, arcRadius, height * (1f - t * 0.25f), i == 0 ? -95f : 95f, i == 0 ? 105f : -105f);
                slash.startColor = WithAlpha(i == 0 ? primary : secondary, alpha);
                slash.endColor = WithAlpha(Color.white, alpha * 0.25f);
                slash.widthMultiplier = Mathf.Lerp(0.18f, 0.035f, t);
            }
        }

        private void ApplySparks(float t, float alpha)
        {
            for (int i = 0; i < _sparks.Count; i++)
            {
                Spark spark = _sparks[i];
                float travel = spark.Distance * Mathf.Sin(Mathf.Clamp01(t) * Mathf.PI * 0.5f);
                Vector3 head = spark.Direction * travel;
                Vector3 tail = head - spark.Direction * Mathf.Lerp(0.12f, 0.55f, t);
                spark.Line.SetPosition(0, tail);
                spark.Line.SetPosition(1, head);
                spark.Line.startColor = WithAlpha(primary, alpha);
                spark.Line.endColor = WithAlpha(secondary, 0f);
                spark.Line.widthMultiplier = Mathf.Lerp(0.07f, 0.01f, t);
            }
        }

        private void ApplyCore(float t, float alpha)
        {
            if (_core == null)
                return;

            float s = Mathf.Lerp(0.25f, 1.25f, Smooth01(t));
            _core.localScale = new Vector3(s, s * 0.18f, s);
            if (_coreRenderer != null)
                _coreRenderer.material.color = WithAlpha(Color.Lerp(Color.white, primary, t), alpha * 0.7f);
        }

        private void WriteCircle(LineRenderer line, float r, float wobble)
        {
            int count = line.positionCount;
            for (int i = 0; i < count; i++)
            {
                float a = i / (float)count * Mathf.PI * 2f;
                float noise = 1f + Mathf.Sin(a * 5f + wobble * 40f) * 0.025f;
                line.SetPosition(i, new Vector3(Mathf.Cos(a) * r * noise, 0f, Mathf.Sin(a) * r * noise));
            }
        }

        private void WriteArc(LineRenderer line, float r, float y, float startDeg, float endDeg)
        {
            int count = line.positionCount;
            for (int i = 0; i < count; i++)
            {
                float p = count <= 1 ? 0f : i / (float)(count - 1);
                float a = Mathf.Lerp(startDeg, endDeg, p) * Mathf.Deg2Rad;
                float lift = Mathf.Sin(p * Mathf.PI) * y;
                line.SetPosition(i, new Vector3(Mathf.Cos(a) * r, lift, Mathf.Sin(a) * r));
            }
        }

        private void SetVisible(bool visible)
        {
            for (int i = 0; i < _rings.Count; i++) _rings[i].enabled = visible;
            for (int i = 0; i < _slashes.Count; i++) _slashes[i].enabled = visible;
            for (int i = 0; i < _sparks.Count; i++) _sparks[i].Line.enabled = visible;
            if (_coreRenderer != null) _coreRenderer.enabled = visible;
        }

        private static float Smooth01(float value)
        {
            float t = Mathf.Clamp01(value);
            return t * t * (3f - 2f * t);
        }

        private static Color WithAlpha(Color color, float alpha)
        {
            color.a = Mathf.Clamp01(alpha);
            return color;
        }

        private static float Hash01(int seed)
        {
            uint x = (uint)seed;
            x ^= x << 13;
            x ^= x >> 17;
            x ^= x << 5;
            return (x & 0xffff) / 65535f;
        }

        private readonly struct Spark
        {
            public readonly LineRenderer Line;
            public readonly Vector3 Direction;
            public readonly float Distance;

            public Spark(LineRenderer line, Vector3 direction, float distance)
            {
                Line = line;
                Direction = direction;
                Distance = distance;
            }
        }
    }
}
