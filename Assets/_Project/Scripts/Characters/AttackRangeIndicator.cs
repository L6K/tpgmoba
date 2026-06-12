using UnityEngine;
using UnityEngine.InputSystem;

namespace Enigma.Character
{
    // A キー長押し中だけ AA 射程を示す円リングを足元に表示する。
    // 半径はピック/強化で変動しうるため、表示中は毎フレーム AutoAttack の射程で更新する。
    public sealed class AttackRangeIndicator : MonoBehaviour
    {
        [SerializeField] private AutoAttack _autoAttack;

        private const int   Segments = 64;
        private const float RingY     = 0.12f;
        private const float RingWidth = 0.06f;

        private LineRenderer _line;

        private void Start()
        {
            var ringGo = new GameObject("AttackRangeRing");
            ringGo.transform.SetParent(transform, false);
            ringGo.transform.localPosition = Vector3.zero;

            _line = ringGo.AddComponent<LineRenderer>();
            _line.useWorldSpace = false;          // 自分の子として追従させる
            _line.loop          = true;
            _line.positionCount = Segments;
            _line.widthMultiplier = RingWidth;
            _line.numCornerVertices = 0;
            _line.shadowCastingMode  = UnityEngine.Rendering.ShadowCastingMode.Off;
            _line.receiveShadows     = false;
            _line.alignment          = LineAlignment.View;

            var mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            var color = new Color(0.5f, 0.9f, 1.0f, 0.6f);
            mat.SetColor("_BaseColor", color);
            ConfigureTransparent(mat);
            _line.material = mat;
            _line.startColor = color;
            _line.endColor   = color;

            ringGo.SetActive(false);
        }

        private void Update()
        {
            bool show = Keyboard.current?.aKey.isPressed == true && _autoAttack != null;

            if (_line.gameObject.activeSelf != show)
                _line.gameObject.SetActive(show);

            if (!show) return;

            // 射程はピックで変わるため毎フレーム反映する
            UpdateRing(_autoAttack.AttackRange);
        }

        private void UpdateRing(float radius)
        {
            for (int i = 0; i < Segments; i++)
            {
                float angle = (i / (float)Segments) * Mathf.PI * 2f;
                _line.SetPosition(i, new Vector3(
                    Mathf.Cos(angle) * radius, RingY, Mathf.Sin(angle) * radius));
            }
        }

        // URP/Unlit を標準アルファ合成の Transparent として描く設定。
        private static void ConfigureTransparent(Material mat)
        {
            mat.SetFloat("_Surface", 1f); // 0=Opaque,1=Transparent
            mat.SetFloat("_Blend", 0f);   // 0=Alpha
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.DisableKeyword("_SURFACE_TYPE_OPAQUE");
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = 3000;
        }
    }
}
