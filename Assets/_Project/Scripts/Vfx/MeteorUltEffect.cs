using System.Collections;
using System.Collections.Generic;
using Enigma.Ability;
using UnityEngine;

namespace Enigma.Vfx
{
    /// <summary>
    /// ゼフのアルティメット「メテオフォール」の着弾地点エフェクト。
    /// Resources/Vfx/Ult/Ult_Zeph.prefab を Instantiate すると Start で自動再生する自己完結演出。
    /// VFX Graph 版へ差し替えるまでのプレースホルダ。
    /// </summary>
    public sealed class MeteorUltEffect : MonoBehaviour
    {
        private const float TelegraphRadius = 4.0f;
        private const float TelegraphShrinkDuration = 0.35f;
        private const float FallStartDelay = 0.15f;
        private const float FallDuration = 0.25f;
        private const float ImpactTime = FallStartDelay + FallDuration; // 0.40s

        private Color _cyan;
        private Color _magenta;

        // SkillVfx.GetTelegraphMaterial は色ごとの共有マテリアルを返すため、
        // 破棄・直接 color 変更はせず MaterialPropertyBlock で個別にフェードさせる
        private readonly List<GameObject> _spawnedGameObjects = new();
        private MaterialPropertyBlock _mpb;

        private void Start()
        {
            var profile = AttackVfxProfiles.For(ChampionVfx.Zeph);
            _cyan = SkillVfx.ToColor(profile.Primary, 3.2f);
            _magenta = SkillVfx.ToColor(profile.Secondary, 3.2f);
            _mpb = new MaterialPropertyBlock();

            StartCoroutine(PlaySequence());
        }

        private IEnumerator PlaySequence()
        {
            StartCoroutine(PlayTelegraph());
            StartCoroutine(PlayComet());

            yield return new WaitForSeconds(ImpactTime);
            PlayImpact();

            StartCoroutine(PlayAftermath());

            // 自前の一時オブジェクトは 3 秒以内に片付ける（外部 Destroy は 6 秒後）
            yield return new WaitForSeconds(Mathf.Max(0f, 3f - ImpactTime));
            CleanupSpawned();
        }

        // ------------------------------------------------------------
        // 1. 収縮予兆 (0〜0.35s)
        // ------------------------------------------------------------
        private IEnumerator PlayTelegraph()
        {
            var ringGo = new GameObject("Telegraph_Ring");
            _spawnedGameObjects.Add(ringGo);
            ringGo.transform.SetParent(transform, false);
            ringGo.transform.localPosition = new Vector3(0f, 0.05f, 0f);
            ringGo.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

            var line = ringGo.AddComponent<LineRenderer>();
            const int segments = 64;
            line.useWorldSpace = false;
            line.loop = true;
            line.positionCount = segments;
            line.numCapVertices = 0;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.sharedMaterial = SkillVfx.GetTelegraphMaterial(_cyan);

            // ルーン粒子代わりの加算クアッド6枚を円周かららせん状に中心へ吸い込む
            const int quadCount = 6;
            var quads = new List<Transform>();
            var quadRenderers = new List<MeshRenderer>();
            for (int i = 0; i < quadCount; i++)
            {
                var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                quad.name = "Telegraph_Rune";
                _spawnedGameObjects.Add(quad);
                var col = quad.GetComponent<Collider>();
                if (col != null) Destroy(col);
                quad.transform.SetParent(transform, false);
                quad.transform.localScale = Vector3.one * 0.25f;
                quad.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

                var renderer = quad.GetComponent<MeshRenderer>();
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.sharedMaterial = SkillVfx.GetTelegraphMaterial(_cyan);

                quads.Add(quad.transform);
                quadRenderers.Add(renderer);
            }

            float t = 0f;
            while (t < TelegraphShrinkDuration)
            {
                float p = t / TelegraphShrinkDuration;
                float smooth = p * p * (3f - 2f * p);
                float radius = Mathf.Lerp(TelegraphRadius, 0.6f, smooth);
                WriteCircle(line, radius);
                float ringAlpha = Mathf.Lerp(0f, 0.8f, Mathf.Clamp01(p * 2f));
                var ringColor = new Color(_cyan.r, _cyan.g, _cyan.b, ringAlpha);
                line.startColor = ringColor;
                line.endColor = ringColor;
                line.startWidth = 0.08f;
                line.endWidth = 0.08f;

                for (int i = 0; i < quadCount; i++)
                {
                    // 各ルーンは 0.3s かけて円周かららせん状に中心へ吸い込まれる
                    float runeP = Mathf.Clamp01(t / 0.3f);
                    float angle = (i / (float)quadCount) * Mathf.PI * 2f + runeP * Mathf.PI * 3f;
                    float r = Mathf.Lerp(TelegraphRadius, 0f, runeP);
                    quads[i].localPosition = new Vector3(Mathf.Cos(angle) * r, 0.06f, Mathf.Sin(angle) * r);
                    quads[i].localRotation = Quaternion.Euler(90f, angle * Mathf.Rad2Deg, 0f);
                    SetInstanceAlpha(quadRenderers[i], _cyan, Mathf.Lerp(0.9f, 0f, runeP));
                }

                t += Time.deltaTime;
                yield return null;
            }

            line.enabled = false;
            for (int i = 0; i < quadCount; i++)
                quads[i].gameObject.SetActive(false);
        }

        // ------------------------------------------------------------
        // 2. 彗星落下 (0.15〜0.40s)
        // ------------------------------------------------------------
        private IEnumerator PlayComet()
        {
            yield return new WaitForSeconds(FallStartDelay);

            var cometGo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            cometGo.name = "Comet";
            _spawnedGameObjects.Add(cometGo);
            var col = cometGo.GetComponent<Collider>();
            if (col != null) Destroy(col);
            cometGo.transform.SetParent(transform, false);
            cometGo.transform.localScale = Vector3.one * 0.8f;

            var renderer = cometGo.GetComponent<MeshRenderer>();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.sharedMaterial = SkillVfx.GetTelegraphMaterial(_cyan);

            var trail = cometGo.AddComponent<TrailRenderer>();
            trail.time = 0.3f;
            trail.startWidth = 0.5f;
            trail.endWidth = 0f;
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            trail.receiveShadows = false;
            trail.sharedMaterial = SkillVfx.GetTelegraphMaterial(Color.white);
            trail.startColor = new Color(_cyan.r, _cyan.g, _cyan.b, 1f);
            trail.endColor = new Color(_magenta.r, _magenta.g, _magenta.b, 0f);

            Vector3 start = transform.position + new Vector3(1.5f, 12f, 0f);
            Vector3 end = transform.position;

            float t = 0f;
            while (t < FallDuration)
            {
                float p = t / FallDuration;
                cometGo.transform.position = Vector3.Lerp(start, end, p);
                t += Time.deltaTime;
                yield return null;
            }

            cometGo.transform.position = end;
            cometGo.SetActive(false);
        }

        // ------------------------------------------------------------
        // 3. 着弾 (t≈0.40s)
        // ------------------------------------------------------------
        private void PlayImpact()
        {
            Vector3 pos = transform.position;

            SkillVfx.SpawnBurst(pos, _cyan, 1f, 4.5f, 0.35f);
            StartCoroutine(DelayedSecondaryBurst(pos));

            // 実効果半径4.0と一致させる
            SkillVfx.SpawnRing(pos, _cyan, 0f, TelegraphRadius, 0.5f);
            SkillVfx.SpawnPillar(pos, _magenta, 0.6f, 5f, 0.4f);

            SpawnRadialSparks(pos);
        }

        private IEnumerator DelayedSecondaryBurst(Vector3 pos)
        {
            yield return new WaitForSeconds(0.08f);
            SkillVfx.SpawnBurst(pos + Vector3.up * 0.3f, _magenta, 0.5f, 3f, 0.3f);
        }

        private void SpawnRadialSparks(Vector3 pos)
        {
            const int sparkCount = 16;
            for (int i = 0; i < sparkCount; i++)
            {
                float angle = (i / (float)sparkCount) * Mathf.PI * 2f;
                Vector3 dir = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                StartCoroutine(PlaySpark(pos, dir));
            }
        }

        private IEnumerator PlaySpark(Vector3 pos, Vector3 dir)
        {
            var go = new GameObject("Impact_Spark");
            _spawnedGameObjects.Add(go);
            go.transform.position = pos;

            var line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.numCapVertices = 2;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.sharedMaterial = SkillVfx.GetTelegraphMaterial(_cyan);

            const float life = 0.25f;
            float t = 0f;
            while (t < life)
            {
                float p = t / life;
                Vector3 from = pos + dir * Mathf.Lerp(1f, 2.4f, p);
                Vector3 to = pos + dir * Mathf.Lerp(1.6f, 3f, p);
                line.SetPosition(0, from);
                line.SetPosition(1, to);

                var c = _cyan;
                c.a = Mathf.Lerp(1f, 0f, p);
                line.startColor = c;
                line.endColor = c;
                line.startWidth = 0.1f;
                line.endWidth = 0.02f;

                t += Time.deltaTime;
                yield return null;
            }

            go.SetActive(false);
        }

        // ------------------------------------------------------------
        // 4. 余韻 (0.4〜2.4s)
        // ------------------------------------------------------------
        private IEnumerator PlayAftermath()
        {
            StartCoroutine(PlayScorchGlow());
            StartCoroutine(PlayFloatingRunes());
            yield return null;
        }

        private IEnumerator PlayScorchGlow()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = "ScorchGlow";
            _spawnedGameObjects.Add(go);
            var col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, 0.03f, 0f);
            go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            go.transform.localScale = Vector3.one * 6f; // 半径3 相当

            var renderer = go.GetComponent<MeshRenderer>();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            var baseColor = new Color(0.1f, 0.05f, 0.2f, 0.5f);
            renderer.sharedMaterial = SkillVfx.GetTelegraphMaterial(baseColor);

            const float life = 2f;
            float t = 0f;
            while (t < life)
            {
                float p = t / life;
                SetInstanceAlpha(renderer, baseColor, Mathf.Lerp(0.5f, 0f, p));
                t += Time.deltaTime;
                yield return null;
            }

            go.SetActive(false);
        }

        private IEnumerator PlayFloatingRunes()
        {
            const int runeCount = 4;
            var runes = new List<Transform>();
            var renderers = new List<MeshRenderer>();

            for (int i = 0; i < runeCount; i++)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
                go.name = "FloatingRune";
                _spawnedGameObjects.Add(go);
                var col = go.GetComponent<Collider>();
                if (col != null) Destroy(col);
                go.transform.SetParent(transform, false);
                float angle = (i / (float)runeCount) * Mathf.PI * 2f;
                Vector3 basePos = new Vector3(Mathf.Cos(angle) * 0.4f, 0.1f, Mathf.Sin(angle) * 0.4f);
                go.transform.localPosition = basePos;
                go.transform.localScale = Vector3.one * 0.2f;

                var renderer = go.GetComponent<MeshRenderer>();
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.sharedMaterial = SkillVfx.GetTelegraphMaterial(_magenta);

                runes.Add(go.transform);
                renderers.Add(renderer);
            }

            const float life = 1.2f;
            float t = 0f;
            while (t < life)
            {
                float p = t / life;
                for (int i = 0; i < runeCount; i++)
                {
                    float angle = (i / (float)runeCount) * Mathf.PI * 2f;
                    Vector3 basePos = new Vector3(Mathf.Cos(angle) * 0.4f, 0.1f + Mathf.Lerp(0f, 1f, p), Mathf.Sin(angle) * 0.4f);
                    runes[i].localPosition = basePos;
                    runes[i].localRotation = Quaternion.Euler(0f, p * 180f, 0f);
                    SetInstanceAlpha(renderers[i], _magenta, Mathf.Lerp(1f, 0f, p));
                }
                t += Time.deltaTime;
                yield return null;
            }

            for (int i = 0; i < runeCount; i++)
                runes[i].gameObject.SetActive(false);
        }

        // ------------------------------------------------------------
        // ユーティリティ
        // ------------------------------------------------------------
        private static void WriteCircle(LineRenderer line, float radius)
        {
            int count = line.positionCount;
            for (int i = 0; i < count; i++)
            {
                float a = i / (float)count * Mathf.PI * 2f;
                line.SetPosition(i, new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius, 0f));
            }
        }

        /// <summary>共有マテリアルを汚さないよう MaterialPropertyBlock でアルファのみ個別上書きする。</summary>
        private void SetInstanceAlpha(MeshRenderer renderer, Color baseColor, float alpha)
        {
            if (renderer == null || _mpb == null) return;
            var c = baseColor;
            c.a = Mathf.Clamp01(alpha);
            renderer.GetPropertyBlock(_mpb);
            _mpb.SetColor("_BaseColor", c);
            _mpb.SetColor("_Color", c);
            renderer.SetPropertyBlock(_mpb);
        }

        private void CleanupSpawned()
        {
            for (int i = 0; i < _spawnedGameObjects.Count; i++)
            {
                var go = _spawnedGameObjects[i];
                if (go != null)
                    Destroy(go);
            }
            _spawnedGameObjects.Clear();
        }

        private void OnDestroy()
        {
            CleanupSpawned();
        }
    }
}
