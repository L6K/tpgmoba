using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Enigma.Combat
{
    // 全ユニット/構造物共通の死亡演出（Humble Object）。
    // HealthModel の Died/Revived を購読し、見た目（_visualRoot）の倒壊・沈下・フェードを担う。
    // ロジック値は DeathAnimationCurve に委譲し、本クラスは Unity 入出力のみ。
    [RequireComponent(typeof(HealthComponent))]
    public sealed class DeathPresenter : MonoBehaviour
    {
        private enum DeathMode { Topple, Sink }

        [SerializeField] private DeathMode _mode = DeathMode.Topple;
        [SerializeField] private float _duration = 1.2f;
        [SerializeField] private bool _destroyWhenDone = false;
        [SerializeField] private Transform _visualRoot;

        private HealthComponent _health;

        // 復元用に初期ローカル姿勢と元マテリアル配列をキャッシュする
        private Vector3 _initialLocalPos;
        private Quaternion _initialLocalRot;
        private Renderer[] _renderers;
        private Material[][] _originalMaterials;

        // 演出中に差し替えるフェード用マテリアル（リーク防止のため完了/復元で破棄）
        private readonly List<Material> _fadeMaterials = new List<Material>();

        // 頭上 HP バーは演出から除外し、死亡中は即時非表示にする
        private GameObject _healthBar;

        private bool _playing;
        private Coroutine _routine;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");

        private void Awake()
        {
            _health = GetComponent<HealthComponent>();
            if (_visualRoot == null) _visualRoot = transform;

            _initialLocalPos = _visualRoot.localPosition;
            _initialLocalRot = _visualRoot.localRotation;

            CacheRenderers();
        }

        private void OnEnable()
        {
            _health.Model.Died += OnDied;
            _health.Model.Revived += OnRevived;
        }

        private void OnDisable()
        {
            if (_health?.Model == null) return;
            _health.Model.Died -= OnDied;
            _health.Model.Revived -= OnRevived;
        }

        private void OnDestroy()
        {
            DestroyFadeMaterials();
        }

        // 頭上バー（名前 "HealthBar"）を演出対象から除外するため、Renderer 収集時に枝ごと除く。
        // バーは _visualRoot ではなくユニットルート直下に付くことがあるため、ルートから探索する。
        private void CacheRenderers()
        {
            _healthBar = FindHealthBar(transform);

            var list = new List<Renderer>();
            foreach (var r in _visualRoot.GetComponentsInChildren<Renderer>(true))
            {
                if (_healthBar != null && r.transform.IsChildOf(_healthBar.transform)) continue;
                list.Add(r);
            }

            _renderers = list.ToArray();
            _originalMaterials = new Material[_renderers.Length][];
            for (int i = 0; i < _renderers.Length; i++)
                _originalMaterials[i] = _renderers[i].sharedMaterials;
        }

        private static GameObject FindHealthBar(Transform root)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == "HealthBar") return t.gameObject;
            return null;
        }

        /// <summary>
        /// モデルスワップ後、死亡演出の対象となる見た目ルートを差し替える。
        /// 旧ルートの Renderer/初期姿勢キャッシュを破棄して新ルートで取り直す。
        /// 演出中の呼び出しは想定しない（試合開始時の1回のみ）。
        /// </summary>
        public void SetVisualRoot(Transform visualRoot)
        {
            if (visualRoot == null) return;

            // 演出中のフェードマテリアルが残っていれば破棄しておく
            DestroyFadeMaterials();

            _visualRoot = visualRoot;
            _initialLocalPos = _visualRoot.localPosition;
            _initialLocalRot = _visualRoot.localRotation;

            CacheRenderers();
        }

        private void OnDied()
        {
            // 二重再生ガード（多段ヒット等で Died が想定外に再入しても安全に）
            if (_playing) return;
            _playing = true;

            if (_healthBar != null) _healthBar.SetActive(false);

            SwapToFadeMaterials();
            _routine = StartCoroutine(PlayRoutine());
        }

        private IEnumerator PlayRoutine()
        {
            // 倒壊は水平面内のランダム軸まわり（毎回違う向きに倒れて単調さを避ける）
            Vector3 toppleAxis = RandomHorizontalAxis();
            float sinkDepth = _mode == DeathMode.Sink ? ComputeSinkDepth() : 0f;

            float elapsed = 0f;
            while (elapsed < _duration)
            {
                float t = _duration > 0f ? elapsed / _duration : 1f;

                if (_mode == DeathMode.Topple)
                {
                    float angle = DeathAnimationCurve.ToppleAngle(t);
                    _visualRoot.localRotation =
                        _initialLocalRot * Quaternion.AngleAxis(angle, toppleAxis);
                }
                else
                {
                    float depth = DeathAnimationCurve.SinkDepth(t, sinkDepth);
                    _visualRoot.localPosition = _initialLocalPos - new Vector3(0f, depth, 0f);
                }

                ApplyFadeAlpha(DeathAnimationCurve.FadeAlpha(t));
                elapsed += Time.deltaTime;
                yield return null;
            }

            ApplyFadeAlpha(0f);

            if (_destroyWhenDone)
            {
                Destroy(gameObject);
            }
            else
            {
                // 非表示中はマテリアルインスタンスを保持する必要がないため、先に元へ戻して破棄する。
                // 復元（Revive）では sharedMaterials を戻すので再生成は不要。
                RestoreOriginalMaterials();
                DestroyFadeMaterials();

                // _visualRoot がロジックを持つルート自身の場合、SetActive(false) すると
                // リスポーンのコルーチンや Update まで止まるため Renderer 無効化に留める
                if (_visualRoot == transform)
                    SetRenderersEnabled(false);
                else
                    _visualRoot.gameObject.SetActive(false);
            }

            _routine = null;
        }

        private void OnRevived()
        {
            if (_routine != null)
            {
                StopCoroutine(_routine);
                _routine = null;
            }

            _visualRoot.localPosition = _initialLocalPos;
            _visualRoot.localRotation = _initialLocalRot;

            RestoreOriginalMaterials();
            DestroyFadeMaterials();

            if (_visualRoot == transform)
                SetRenderersEnabled(true);
            else
                _visualRoot.gameObject.SetActive(true);
            if (_healthBar != null) _healthBar.SetActive(true);

            _playing = false;
        }

        private void SetRenderersEnabled(bool enabled)
        {
            if (_renderers == null) return;
            foreach (var r in _renderers)
                if (r != null) r.enabled = enabled;
        }

        // 元 sharedMaterial の _BaseColor / _BaseMap を引き継いだ URP/Unlit 透過マテリアルへ差し替える
        private void SwapToFadeMaterials()
        {
            var unlit = Shader.Find("Universal Render Pipeline/Unlit");

            for (int i = 0; i < _renderers.Length; i++)
            {
                var src = _originalMaterials[i];
                var fade = new Material[src.Length];
                for (int j = 0; j < src.Length; j++)
                    fade[j] = CreateFadeMaterial(unlit, src[j]);
                _renderers[i].sharedMaterials = fade;
            }
        }

        private Material CreateFadeMaterial(Shader unlit, Material source)
        {
            var mat = new Material(unlit);

            Color baseColor = Color.white;
            if (source != null && source.HasProperty(BaseColorId))
                baseColor = source.GetColor(BaseColorId);
            mat.SetColor(BaseColorId, baseColor);

            if (source != null && source.HasProperty(BaseMapId))
            {
                var tex = source.GetTexture(BaseMapId);
                if (tex != null) mat.SetTexture(BaseMapId, tex);
            }

            EnableTransparency(mat);
            _fadeMaterials.Add(mat);
            return mat;
        }

        // URP/Unlit を Alpha ブレンド透過に切り替える（surface=Transparent 相当のレンダーステート）
        private static void EnableTransparency(Material mat)
        {
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", 0f);
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.DisableKeyword("_SURFACE_TYPE_OPAQUE");
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        }

        private void ApplyFadeAlpha(float alpha)
        {
            foreach (var mat in _fadeMaterials)
            {
                if (mat == null) continue;
                var c = mat.GetColor(BaseColorId);
                c.a = alpha;
                mat.SetColor(BaseColorId, c);
            }
        }

        private void RestoreOriginalMaterials()
        {
            if (_renderers == null) return;
            for (int i = 0; i < _renderers.Length; i++)
            {
                if (_renderers[i] == null) continue;
                _renderers[i].sharedMaterials = _originalMaterials[i];
            }
        }

        private void DestroyFadeMaterials()
        {
            foreach (var mat in _fadeMaterials)
                if (mat != null) Destroy(mat);
            _fadeMaterials.Clear();
        }

        // 見た目の bounds 高さの 0.8 倍だけ沈める
        private float ComputeSinkDepth()
        {
            if (_renderers == null || _renderers.Length == 0) return 1f;

            bool has = false;
            Bounds b = default;
            foreach (var r in _renderers)
            {
                if (r == null) continue;
                if (!has) { b = r.bounds; has = true; }
                else b.Encapsulate(r.bounds);
            }
            return has ? b.size.y * 0.8f : 1f;
        }

        private static Vector3 RandomHorizontalAxis()
        {
            float a = Random.value * Mathf.PI * 2f;
            return new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
        }
    }
}
