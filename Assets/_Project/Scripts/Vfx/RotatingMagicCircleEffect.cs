using System.Collections.Generic;
using UnityEngine;

namespace Enigma.Vfx
{
    /// <summary>
    /// Red rotating magic circle effect built from runtime line renderers.
    /// Assign existing VFX materials, place on the floor, then call Play().
    /// </summary>
    public sealed class RotatingMagicCircleEffect : MonoBehaviour
    {
        [Header("Materials")]
        [SerializeField] private Material ringMaterial;
        [SerializeField] private Material glyphMaterial;
        [SerializeField] private Material sparkMaterial;

        [Header("Playback")]
        [SerializeField] private bool playOnEnable = true;
        [SerializeField] private bool loop = true;
        [SerializeField] private float fadeInSeconds = 0.45f;
        [SerializeField] private float fadeOutSeconds = 0.35f;
        [SerializeField] private float lifeSeconds = 4.5f;

        [Header("Look")]
        [SerializeField] private Color coreColor = new Color(1f, 0.08f, 0.02f, 1f);
        [SerializeField] private Color edgeColor = new Color(1f, 0.32f, 0.08f, 1f);
        [SerializeField] private float radius = 2.8f;
        [SerializeField] private float rotationSpeed = 18f;
        [SerializeField] private float pulseSpeed = 1.25f;
        [SerializeField] private int runeCount = 12;
        [SerializeField] private int emberCount = 20;

        private readonly List<LineRenderer> _rings = new List<LineRenderer>();
        private readonly List<LineRenderer> _glyphs = new List<LineRenderer>();
        private readonly List<Ember> _embers = new List<Ember>();
        private readonly List<Material> _runtimeMaterials = new List<Material>();

        private Transform _outerLayer;
        private Transform _innerLayer;
        private Transform _runeLayer;
        private Transform _emberLayer;
        private float _age;
        private bool _playing;

        public void Play()
        {
            EnsureBuilt();
            _age = 0f;
            _playing = true;
            SetVisible(true);
            ApplyFrame(0f, 0f);
        }

        public void Stop()
        {
            _playing = false;
            SetVisible(false);
        }

        private bool _destroyOnComplete;
        private static RotatingMagicCircleEffect _prefabCache;
        private static bool _prefabLoaded;

        /// <summary>再生前に色・半径・ループ/自動破棄を設定する（Spawn から呼ぶ）。</summary>
        public void Configure(Color core, Color edge, float circleRadius, bool isLoop, bool autoDestroy)
        {
            coreColor = core;
            edgeColor = edge;
            if (circleRadius > 0f) radius = circleRadius;
            loop = isLoop;
            _destroyOnComplete = autoDestroy;
        }

        /// <summary>Resources の回転魔法陣を指定位置に生成し、指定色・寿命で一度だけ再生する。</summary>
        public static RotatingMagicCircleEffect Spawn(Vector3 position, Color core, Color edge, float circleRadius, float life)
        {
            if (!_prefabLoaded)
            {
                _prefabCache = Resources.Load<RotatingMagicCircleEffect>("Vfx/RotatingMagicCircleEffect");
                _prefabLoaded = true;
            }
            if (_prefabCache == null) return null;

            var inst = Instantiate(_prefabCache, position, Quaternion.identity);
            if (life > 0f) inst.lifeSeconds = life;
            inst.Configure(core, edge, circleRadius, isLoop: false, autoDestroy: true);
            inst.Play();
            return inst;
        }

        private void OnDestroy()
        {
            for (int i = 0; i < _runtimeMaterials.Count; i++)
            {
                if (_runtimeMaterials[i] != null)
                    Destroy(_runtimeMaterials[i]);
            }
            _runtimeMaterials.Clear();
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
            float cycle = lifeSeconds <= 0f ? 0f : _age / lifeSeconds;
            float normalized = loop ? cycle - Mathf.Floor(cycle) : Mathf.Clamp01(cycle);
            float alpha = AlphaAt(_age, normalized);
            ApplyFrame(normalized, alpha);

            if (!loop && _age >= lifeSeconds + fadeOutSeconds)
            {
                if (_destroyOnComplete) Destroy(gameObject);
                else Stop();
            }
        }

        private void EnsureBuilt()
        {
            if (_outerLayer != null)
                return;

            _outerLayer = CreateLayer("OuterCounterSeal");
            _innerLayer = CreateLayer("InnerSigil");
            _runeLayer = CreateLayer("RuneLayer");
            _emberLayer = CreateLayer("RedEmbers");

            BuildRings();
            BuildGlyphs();
            BuildRunes();
            BuildEmbers();
        }

        private Transform CreateLayer(string layerName)
        {
            var go = new GameObject(layerName);
            go.transform.SetParent(transform, false);
            return go.transform;
        }

        private void BuildRings()
        {
            for (int i = 0; i < 5; i++)
            {
                LineRenderer ring = CreateLine("Ring_" + i, ringMaterial, 144, _outerLayer);
                ring.loop = true;
                ring.widthMultiplier = 0.09f + i * 0.018f;
                _rings.Add(ring);
            }

            for (int i = 0; i < 3; i++)
            {
                LineRenderer ring = CreateLine("InnerRing_" + i, ringMaterial, 96, _innerLayer);
                ring.loop = true;
                ring.widthMultiplier = 0.085f;
                _rings.Add(ring);
            }
        }

        private void BuildGlyphs()
        {
            for (int i = 0; i < 8; i++)
            {
                LineRenderer spoke = CreateLine("RadialGlyph_" + i, glyphMaterial, 2, _innerLayer);
                spoke.widthMultiplier = 0.08f;
                _glyphs.Add(spoke);
            }

            for (int i = 0; i < 6; i++)
            {
                LineRenderer arc = CreateLine("BrokenArc_" + i, glyphMaterial, 18, _outerLayer);
                arc.widthMultiplier = 0.095f;
                _glyphs.Add(arc);
            }

            LineRenderer star = CreateLine("CenterStar", glyphMaterial, 13, _innerLayer);
            star.loop = true;
            star.widthMultiplier = 0.09f;
            _glyphs.Add(star);
        }

        private void BuildRunes()
        {
            int count = Mathf.Max(3, runeCount);
            for (int i = 0; i < count; i++)
            {
                LineRenderer rune = CreateLine("RuneDiamond_" + i, glyphMaterial, 5, _runeLayer);
                rune.loop = false;
                rune.widthMultiplier = 0.075f;
                _glyphs.Add(rune);
            }
        }

        private void BuildEmbers()
        {
            int count = Mathf.Max(0, emberCount);
            for (int i = 0; i < count; i++)
            {
                LineRenderer ember = CreateLine("RisingEmber_" + i, sparkMaterial, 2, _emberLayer);
                ember.widthMultiplier = 0.075f;
                float angle = i * 137.5f * Mathf.Deg2Rad;
                float distance = radius * Mathf.Lerp(0.18f, 0.95f, Hash01(i * 37 + 11));
                float height = Mathf.Lerp(0.35f, 1.25f, Hash01(i * 53 + 7));
                float phase = Hash01(i * 97 + 5);
                _embers.Add(new Ember(ember, angle, distance, height, phase));
            }
        }

        private LineRenderer CreateLine(string objectName, Material material, int positions, Transform parent)
        {
            var go = new GameObject(objectName);
            go.transform.SetParent(parent, false);
            LineRenderer line = go.AddComponent<LineRenderer>();
            line.positionCount = positions;
            if (material != null)
            {
                var instance = new Material(material);
                line.material = instance;
                _runtimeMaterials.Add(instance);
            }
            line.useWorldSpace = false;
            line.alignment = LineAlignment.TransformZ;
            line.textureMode = LineTextureMode.Stretch;
            line.numCapVertices = 4;
            line.numCornerVertices = 4;
            return line;
        }

        private void ApplyFrame(float t, float alpha)
        {
            float pulse = 0.75f + 0.25f * Mathf.Sin((_age * pulseSpeed) * Mathf.PI * 2f);
            float glow = Mathf.Clamp01(alpha * (1.2f + 0.45f * pulse));

            _outerLayer.localRotation = Quaternion.Euler(0f, rotationSpeed * _age, 0f);
            _innerLayer.localRotation = Quaternion.Euler(0f, -rotationSpeed * 0.62f * _age, 0f);
            _runeLayer.localRotation = Quaternion.Euler(0f, rotationSpeed * 0.34f * _age, 0f);
            _emberLayer.localRotation = Quaternion.Euler(0f, rotationSpeed * 0.18f * _age, 0f);

            ApplyRings(glow);
            ApplyGlyphs(glow);
            ApplyRunes(glow);
            ApplyEmbers(t, glow);
        }

        private void ApplyRings(float alpha)
        {
            for (int i = 0; i < 5; i++)
            {
                float r = radius * (0.55f + i * 0.115f);
                WriteCircle(_rings[i], r, i * 0.18f);
                SetLineColor(_rings[i], Color.Lerp(coreColor, edgeColor, i / 4f), alpha * (1f - i * 0.08f));
            }

            for (int i = 0; i < 3; i++)
            {
                float r = radius * (0.18f + i * 0.105f);
                WriteCircle(_rings[5 + i], r, i * 0.11f);
                SetLineColor(_rings[5 + i], coreColor, alpha);
            }
        }

        private void ApplyGlyphs(float alpha)
        {
            for (int i = 0; i < 8; i++)
            {
                float angle = i / 8f * Mathf.PI * 2f;
                float inner = radius * 0.25f;
                float outer = radius * 0.82f;
                Vector3 a = new Vector3(Mathf.Cos(angle) * inner, 0f, Mathf.Sin(angle) * inner);
                Vector3 b = new Vector3(Mathf.Cos(angle) * outer, 0f, Mathf.Sin(angle) * outer);
                _glyphs[i].SetPosition(0, a);
                _glyphs[i].SetPosition(1, b);
                SetLineColor(_glyphs[i], edgeColor, alpha * 0.75f);
            }

            for (int i = 0; i < 6; i++)
            {
                float start = i * 60f + 8f;
                float end = start + 34f;
                WriteArc(_glyphs[8 + i], radius * 0.92f, start, end);
                SetLineColor(_glyphs[8 + i], edgeColor, alpha);
            }

            WriteStar(_glyphs[14], radius * 0.18f, radius * 0.34f);
            SetLineColor(_glyphs[14], coreColor, alpha);
        }

        private void ApplyRunes(float alpha)
        {
            int firstRune = 15;
            int count = Mathf.Max(3, runeCount);
            for (int i = 0; i < count; i++)
            {
                float angle = i / (float)count * Mathf.PI * 2f;
                WriteDiamond(_glyphs[firstRune + i], angle, radius * 1.03f, radius * 0.055f);
                SetLineColor(_glyphs[firstRune + i], i % 2 == 0 ? coreColor : edgeColor, alpha * 0.9f);
            }
        }

        private void ApplyEmbers(float t, float alpha)
        {
            for (int i = 0; i < _embers.Count; i++)
            {
                Ember ember = _embers[i];
                float rise = Frac(t + ember.Phase);
                float angle = ember.Angle + Mathf.Sin(_age * 0.8f + i) * 0.12f;
                Vector3 basePos = new Vector3(Mathf.Cos(angle) * ember.Distance, 0f, Mathf.Sin(angle) * ember.Distance);
                Vector3 head = basePos + Vector3.up * (ember.Height * rise);
                Vector3 tail = basePos + Vector3.up * Mathf.Max(0f, ember.Height * rise - 0.28f);
                ember.Line.SetPosition(0, tail);
                ember.Line.SetPosition(1, head);
                SetLineColor(ember.Line, edgeColor, alpha * (1f - rise));
            }
        }

        private void WriteCircle(LineRenderer line, float r, float offset)
        {
            int count = line.positionCount;
            for (int i = 0; i < count; i++)
            {
                float p = i / (float)count;
                float a = p * Mathf.PI * 2f;
                float wobble = 1f + Mathf.Sin(a * 12f + offset * 13f) * 0.01f;
                line.SetPosition(i, new Vector3(Mathf.Cos(a) * r * wobble, 0f, Mathf.Sin(a) * r * wobble));
            }
        }

        private static void WriteArc(LineRenderer line, float r, float startDeg, float endDeg)
        {
            int count = line.positionCount;
            for (int i = 0; i < count; i++)
            {
                float p = count <= 1 ? 0f : i / (float)(count - 1);
                float a = Mathf.Lerp(startDeg, endDeg, p) * Mathf.Deg2Rad;
                line.SetPosition(i, new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r));
            }
        }

        private static void WriteStar(LineRenderer line, float inner, float outer)
        {
            int count = line.positionCount;
            for (int i = 0; i < count; i++)
            {
                float a = i / (float)(count - 1) * Mathf.PI * 2f;
                float r = i % 2 == 0 ? outer : inner;
                line.SetPosition(i, new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r));
            }
        }

        private static void WriteDiamond(LineRenderer line, float angle, float distance, float size)
        {
            Vector3 center = new Vector3(Mathf.Cos(angle) * distance, 0f, Mathf.Sin(angle) * distance);
            Vector3 radial = center.normalized;
            Vector3 tangent = new Vector3(-radial.z, 0f, radial.x);

            line.SetPosition(0, center + radial * size);
            line.SetPosition(1, center + tangent * size * 0.65f);
            line.SetPosition(2, center - radial * size);
            line.SetPosition(3, center - tangent * size * 0.65f);
            line.SetPosition(4, center + radial * size);
        }

        private void SetVisible(bool visible)
        {
            for (int i = 0; i < _rings.Count; i++) _rings[i].enabled = visible;
            for (int i = 0; i < _glyphs.Count; i++) _glyphs[i].enabled = visible;
            for (int i = 0; i < _embers.Count; i++) _embers[i].Line.enabled = visible;
        }

        private static void SetLineColor(LineRenderer line, Color color, float alpha)
        {
            color.a = Mathf.Clamp01(alpha);
            line.startColor = color;
            line.endColor = color;

            Material material = line.material;
            if (material == null)
                return;

            Color emission = color * 6.0f;
            emission.a = color.a;
            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color"))
                material.SetColor("_Color", color);
            if (material.HasProperty("_EmissionColor"))
                material.SetColor("_EmissionColor", emission);
        }

        private float AlphaAt(float age, float cycle)
        {
            float fadeIn = fadeInSeconds <= 0f ? 1f : Mathf.Clamp01(age / fadeInSeconds);
            if (loop)
                return fadeIn;

            if (lifeSeconds <= 0f)
                return fadeIn;

            float fadeOutStart = lifeSeconds;
            if (age <= fadeOutStart)
                return fadeIn;

            float fadeOut = fadeOutSeconds <= 0f ? 0f : Mathf.Clamp01((age - fadeOutStart) / fadeOutSeconds);
            return Mathf.Clamp01(1f - fadeOut);
        }

        private static float Frac(float value)
        {
            return value - Mathf.Floor(value);
        }

        private static float Hash01(int seed)
        {
            uint x = (uint)seed;
            x ^= x << 13;
            x ^= x >> 17;
            x ^= x << 5;
            return (x & 0xffff) / 65535f;
        }

        private readonly struct Ember
        {
            public readonly LineRenderer Line;
            public readonly float Angle;
            public readonly float Distance;
            public readonly float Height;
            public readonly float Phase;

            public Ember(LineRenderer line, float angle, float distance, float height, float phase)
            {
                Line = line;
                Angle = angle;
                Distance = distance;
                Height = height;
                Phase = phase;
            }
        }
    }
}
