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

        // リング円の分割数（滑らかさと頂点数の妥協点）
        private const int RingSegments = 48;

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

        /// <summary>
        /// 地面すれすれの衝撃波リング。LineRenderer の円を fromRadius→toRadius へ拡大しつつ
        /// アルファフェードして自壊させる。起爆・着弾のヒット感に使う。
        /// </summary>
        public static void SpawnRing(Vector3 pos, Color color, float fromRadius, float toRadius, float life)
        {
            var go = new GameObject("SkillRing");
            go.transform.position = pos;
            // ローカル Z をワールド上向きへ回し、TransformZ 整列のリボンを地面に寝かせる
            go.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);

            var line = go.AddComponent<LineRenderer>();
            line.useWorldSpace            = false; // ローカル円を描き、GO スケールで拡縮
            line.loop                     = true;
            line.positionCount            = RingSegments;
            line.startWidth               = 0.08f;
            line.endWidth                 = 0.08f;
            line.numCapVertices           = 0;
            line.shadowCastingMode        = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows           = false;
            line.sharedMaterial           = GetTransparentMaterial(color);
            // 地面に水平に寝かせたいので GO の向きに従わせる（View だとカメラ向きで立ってしまう）
            line.alignment                = LineAlignment.TransformZ;

            // 単位円(半径1)をローカル XY 平面に敷く。GO を -90°X 回転済みなので
            // ワールドでは水平な円になり、半径制御はスケール(X,Y)へ寄せる
            for (int i = 0; i < RingSegments; i++)
            {
                float a = (float)i / RingSegments * Mathf.PI * 2f;
                line.SetPosition(i, new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f));
            }

            var fade = go.AddComponent<VfxAnim>();
            fade.BeginRing(line, color, fromRadius, toRadius, life);
        }

        /// <summary>
        /// 着弾光柱。上方向に伸びた半透明シリンダーが縦スケールを保ったまま
        /// アルファフェード+わずかに縮径して自壊する。
        /// </summary>
        public static void SpawnPillar(Vector3 pos, Color color, float radius, float height, float life)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = "SkillPillar";

            var col = go.GetComponent<Collider>();
            if (col != null) Object.Destroy(col);

            // Cylinder メッシュは高さ2 → scaleY=height/2、半径0.5 → scaleXZ=radius*2。
            // 柱の底を pos に合わせるため中心を height/2 だけ持ち上げる
            go.transform.position   = pos + Vector3.up * (height * 0.5f);
            go.transform.localScale = new Vector3(radius * 2f, height * 0.5f, radius * 2f);

            var renderer = go.GetComponent<MeshRenderer>();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows    = false;
            renderer.sharedMaterial    = GetTransparentMaterial(color);

            var fade = go.AddComponent<VfxAnim>();
            fade.BeginPillar(renderer, color, life);
        }

        /// <summary>
        /// 2点間の閃光ビーム。LineRenderer の2点を結び、幅を life で 0 へ、
        /// アルファフェードして自壊する。対象指定ヒットの一閃に使う。
        /// </summary>
        public static void SpawnBeam(Vector3 from, Vector3 to, Color color, float width, float life)
        {
            var go = new GameObject("SkillBeam");

            var line = go.AddComponent<LineRenderer>();
            line.useWorldSpace     = true;
            line.positionCount     = 2;
            line.SetPosition(0, from);
            line.SetPosition(1, to);
            line.startWidth        = width;
            line.endWidth          = width;
            line.numCapVertices    = 2;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows    = false;
            line.sharedMaterial    = GetTransparentMaterial(color);

            var fade = go.AddComponent<VfxAnim>();
            fade.BeginBeam(line, color, width, life);
        }

        // ── CastVisuals 系ヘルパー（SkillCaster / EnemyChampionAI から共通利用） ──

        /// <summary>
        /// 方向弾の発射演出。弾に発光コア(小球)の子を付け、白コア小+スロット色大の二段バーストを出す。
        /// </summary>
        public static void FireDirectionalVisuals(GameObject proj, Vector3 muzzlePos, Vector3 dir, Color color)
        {
            var nd = dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector3.forward;

            // 弾本体: 白い芯 + 色付き外殻(大きめ紡錘) の二重発光コア + 動的ライト + 太く長いトレイル
            AttachGlowCore(proj, dir, Color.white, 0.22f, 1.1f);
            AttachGlowCore(proj, dir, color,       0.55f, 1.9f);
            AttachLight(proj, color, 5f, 3.5f);
            AddTrail(proj, color, 0.45f, 0.4f);

            // マズル: 白閃光 + 特大の色バースト + 衝撃リング + 砲口前方の追い火
            SpawnBurst(muzzlePos, Color.white, 0.3f, 1.3f, 0.16f);
            SpawnBurst(muzzlePos, color,       0.55f, 2.6f, 0.32f);
            SpawnRing(muzzlePos, color, 0.3f, 2.4f, 0.3f);
            SpawnBurst(muzzlePos + nd * 0.8f, color, 0.4f, 1.5f, 0.22f);
        }

        /// <summary>対象(弾など)に短命の動的ポイントライトを付与して発光感を出す。</summary>
        public static void AttachLight(GameObject target, Color color, float range, float intensity)
        {
            if (target == null) return;
            var go = new GameObject("VfxLight");
            go.transform.SetParent(target.transform, false);
            go.transform.localPosition = Vector3.zero;
            var l = go.AddComponent<Light>();
            l.type      = LightType.Point;
            l.color     = color;
            l.range     = range;
            l.intensity = intensity;
            l.shadows   = LightShadows.None;
        }

        /// <summary>
        /// 対象指定ヒットの演出。胸元→対象へビーム一閃 + 対象位置にバースト+小リング。
        /// </summary>
        public static void TargetedHitVisuals(Vector3 from, Vector3 to, Color color)
        {
            SpawnBeam(from, to, color, 0.18f, 0.2f);
            SpawnBurst(to, color, 0.3f, 1.4f, 0.25f);
            SpawnRing(to, color, 0.3f, 1.6f, 0.3f);
        }

        /// <summary>
        /// 発光コア(細長い小球)を弾の子として付与し、進行方向へ向ける。コライダーは付かない。
        /// </summary>
        public static void AttachGlowCore(GameObject target, Vector3 dir, Color color, float thickness, float length)
        {
            if (target == null) return;

            var core = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            core.name = "GlowCore";

            var col = core.GetComponent<Collider>();
            if (col != null) Object.Destroy(col);

            core.transform.SetParent(target.transform, false);
            core.transform.localPosition = Vector3.zero;
            // Z(進行方向)へ伸ばした紡錘形にし、弾の向きへ合わせる
            core.transform.localScale = new Vector3(thickness, thickness, length);
            if (dir.sqrMagnitude > 0.0001f)
                core.transform.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);

            var renderer = core.GetComponent<MeshRenderer>();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows    = false;
            renderer.sharedMaterial    = GetTransparentMaterial(color);
        }

        /// <summary>飛翔体などに付ける細いトレイルを生成する。マテリアルはバーストと共用。</summary>
        public static TrailRenderer AddTrail(GameObject target, Color color, float startWidth, float time)
        {
            // 既に TrailRenderer を持つプレハブ(AaBeam 等)には二重付与できず
            // AddComponent が null を返すため、既存があれば色だけ差し替えて再利用する
            if (!target.TryGetComponent<TrailRenderer>(out var trail))
                trail = target.AddComponent<TrailRenderer>();
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

        /// <summary>透過マテリアルキャッシュへの公開アクセサ（予兆リング等が共用するため）。</summary>
        public static Material GetTelegraphMaterial(Color color) => GetTransparentMaterial(color);

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

    /// <summary>
    /// リング/光柱/ビームを Time.deltaTime で補間し、寿命終了で自壊させる汎用アニメータ。
    /// VfxFade は MeshRenderer 専用のため、LineRenderer や縦伸びシリンダーは本コンポーネントで扱う。
    /// </summary>
    public sealed class VfxAnim : MonoBehaviour
    {
        private enum Kind { Ring, Pillar, Beam }

        private Kind  _kind;
        private Color _color;
        private float _life;
        private float _elapsed;

        // Ring
        private LineRenderer _line;
        private float        _fromRadius;
        private float        _toRadius;

        // Pillar
        private MeshRenderer          _renderer;
        private MaterialPropertyBlock _mpb;
        private Vector3               _pillarBaseScale;

        // Beam
        private float _beamWidth;

        public void BeginRing(LineRenderer line, Color color, float fromRadius, float toRadius, float life)
        {
            _kind       = Kind.Ring;
            _line       = line;
            _color      = color;
            _fromRadius = fromRadius;
            _toRadius   = toRadius;
            _life       = life > 0f ? life : 0.25f;
        }

        public void BeginPillar(MeshRenderer renderer, Color color, float life)
        {
            _kind            = Kind.Pillar;
            _renderer        = renderer;
            _color           = color;
            _life            = life > 0f ? life : 0.25f;
            _pillarBaseScale = transform.localScale;
            _mpb             = new MaterialPropertyBlock();
        }

        public void BeginBeam(LineRenderer line, Color color, float width, float life)
        {
            _kind      = Kind.Beam;
            _line      = line;
            _color     = color;
            _beamWidth = width;
            _life      = life > 0f ? life : 0.2f;
        }

        private void Update()
        {
            _elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(_elapsed / _life);

            var c = _color;
            c.a = Mathf.Lerp(_color.a, 0f, t);

            switch (_kind)
            {
                case Kind.Ring:
                    // 円はローカル XY 平面に敷いてあるため X,Y を半径でスケールする
                    float r = Mathf.Lerp(_fromRadius, _toRadius, t);
                    transform.localScale = new Vector3(r, r, 1f);
                    if (_line != null)
                    {
                        _line.startColor = c;
                        _line.endColor   = c;
                    }
                    break;

                case Kind.Pillar:
                    // 縦スケールは保ち、わずかに縮径しながらフェード
                    float shrink = Mathf.Lerp(1f, 0.7f, t);
                    transform.localScale = new Vector3(
                        _pillarBaseScale.x * shrink, _pillarBaseScale.y, _pillarBaseScale.z * shrink);
                    if (_renderer != null && _mpb != null)
                    {
                        _renderer.GetPropertyBlock(_mpb);
                        _mpb.SetColor("_BaseColor", c);
                        _mpb.SetColor("_Color", c);
                        _renderer.SetPropertyBlock(_mpb);
                    }
                    break;

                case Kind.Beam:
                    float w = Mathf.Lerp(_beamWidth, 0f, t);
                    if (_line != null)
                    {
                        _line.startWidth = w;
                        _line.endWidth   = w;
                        _line.startColor = c;
                        _line.endColor   = c;
                    }
                    break;
            }

            if (t >= 1f)
                Destroy(gameObject);
        }
    }
}
