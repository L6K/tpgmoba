using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Enigma.Combat
{
    // 被弾の瞬間、配下の全 Renderer を白寄りに明滅させてヒットの手応えを出す（Humble Object）。
    // MaterialPropertyBlock を使うため共有マテリアルを汚さず、トゥーン以外のマテリアルは黙ってスキップする。
    [RequireComponent(typeof(HealthComponent))]
    public sealed class HitFlash : MonoBehaviour
    {
        private const float FlashDuration = 0.08f;
        private const float FlashLerp = 0.6f; // 元色から白へ寄せる比率

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        private HealthComponent _health;
        private Renderer[] _renderers;
        private MaterialPropertyBlock _block;

        // 各 Renderer の元 _BaseColor。フラッシュ終了で元へ戻すため初回にキャッシュする
        private Color[] _baseColors;
        private bool[] _hasBaseColor;

        private Coroutine _routine;
        private float _flashEndTime;

        private void Awake()
        {
            _health = GetComponent<HealthComponent>();
            _block = new MaterialPropertyBlock();
            CacheRenderers();
        }

        private void OnEnable()
        {
            _health.Damaged += OnDamaged;
        }

        private void OnDisable()
        {
            _health.Damaged -= OnDamaged;
        }

        // _BaseColor を持つ Renderer だけをフラッシュ対象として収集する。
        // sharedMaterial の色を元色として記録（MaterialPropertyBlock 適用前の本来の色）。
        private void CacheRenderers()
        {
            var found = GetComponentsInChildren<Renderer>(true);
            var list = new List<Renderer>(found.Length);
            var colors = new List<Color>(found.Length);
            var has = new List<bool>(found.Length);

            foreach (var r in found)
            {
                var mat = r.sharedMaterial;
                if (mat != null && mat.HasProperty(BaseColorId))
                {
                    list.Add(r);
                    colors.Add(mat.GetColor(BaseColorId));
                    has.Add(true);
                }
            }

            _renderers = list.ToArray();
            _baseColors = colors.ToArray();
            _hasBaseColor = has.ToArray();
        }

        private void OnDamaged(float amount)
        {
            if (_renderers == null || _renderers.Length == 0) return;

            // 連続被弾は終了時刻を延長して再トリガー（コルーチンは1本のみ維持＝再入ガード）
            _flashEndTime = Time.time + FlashDuration;
            ApplyFlash(true);

            if (_routine == null)
                _routine = StartCoroutine(FlashRoutine());
        }

        private IEnumerator FlashRoutine()
        {
            while (Time.time < _flashEndTime)
                yield return null;

            ApplyFlash(false);
            _routine = null;
        }

        // flash=true で白寄り、false で元色へ戻す。MaterialPropertyBlock で非破壊に上書きする。
        private void ApplyFlash(bool flash)
        {
            for (int i = 0; i < _renderers.Length; i++)
            {
                var r = _renderers[i];
                if (r == null || !_hasBaseColor[i]) continue;

                Color target = flash
                    ? Color.Lerp(_baseColors[i], Color.white, FlashLerp)
                    : _baseColors[i];

                r.GetPropertyBlock(_block);
                _block.SetColor(BaseColorId, target);
                r.SetPropertyBlock(_block);
            }
        }
    }
}
