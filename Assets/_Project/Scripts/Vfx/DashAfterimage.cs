using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using Enigma.Ability;

namespace Enigma.Vfx
{
    // ダッシュ演出: 短時間で複数のゴースト(残像)を生成して消えるエフェクト
    public static class DashAfterimage
    {
        public static void Spawn(GameObject owner, Color from, Color to, int count = 3, float intervalSeconds = 0.06f, float lifeSeconds = 0.4f)
        {
            if (owner == null) return;

            var emitterGo = new GameObject("AfterimageEmitter");
            var emitter = emitterGo.AddComponent<AfterimageEmitter>();
            emitter.Begin(owner, from, to, count, intervalSeconds, lifeSeconds);
        }

        private sealed class AfterimageEmitter : MonoBehaviour
        {
            public void Begin(GameObject owner, Color from, Color to, int count, float intervalSeconds, float lifeSeconds)
            {
                StartCoroutine(Run(owner, from, to, count, intervalSeconds, lifeSeconds));
            }

            private IEnumerator Run(GameObject owner, Color from, Color to, int count, float intervalSeconds, float lifeSeconds)
            {
                for (int i = 0; i < count; i++)
                {
                    SpawnGhost(owner, from, to, lifeSeconds);
                    yield return new WaitForSeconds(intervalSeconds);
                }
                Destroy(gameObject);
            }

            private static void SpawnGhost(GameObject owner, Color from, Color to, float lifeSeconds)
            {
                Mesh mesh;
                Transform sourceTransform;
                Mesh bakedMeshToDestroy = null;

                var skinned = owner.GetComponentInChildren<SkinnedMeshRenderer>();
                if (skinned != null)
                {
                    mesh = new Mesh();
                    skinned.BakeMesh(mesh);
                    bakedMeshToDestroy = mesh;
                    sourceTransform = skinned.transform;
                }
                else
                {
                    var meshFilter = owner.GetComponentInChildren<MeshFilter>();
                    if (meshFilter == null) return;
                    var meshRenderer = meshFilter.GetComponent<MeshRenderer>();
                    if (meshRenderer == null) return;
                    mesh = meshFilter.sharedMesh;
                    if (mesh == null) return;
                    sourceTransform = meshFilter.transform;
                }

                var ghost = new GameObject("AfterimageGhost");
                ghost.transform.SetPositionAndRotation(sourceTransform.position, sourceTransform.rotation);

                var filter = ghost.AddComponent<MeshFilter>();
                filter.mesh = mesh;

                var renderer = ghost.AddComponent<MeshRenderer>();
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.material = SkillVfx.GetTelegraphMaterial(from);

                var fade = ghost.AddComponent<AfterimageFade>();
                fade.Begin(renderer.material, from, to, lifeSeconds, bakedMeshToDestroy);
            }
        }

        private sealed class AfterimageFade : MonoBehaviour
        {
            private Material _material;
            private Color _from;
            private Color _to;
            private float _lifeSeconds;
            private float _elapsed;
            private Mesh _bakedMeshToDestroy;

            public void Begin(Material material, Color from, Color to, float lifeSeconds, Mesh bakedMeshToDestroy)
            {
                _material = material;
                _from = from;
                _to = to;
                _lifeSeconds = lifeSeconds;
                _bakedMeshToDestroy = bakedMeshToDestroy;
            }

            private void Update()
            {
                _elapsed += Time.deltaTime;
                float t = _lifeSeconds > 0f ? Mathf.Clamp01(_elapsed / _lifeSeconds) : 1f;

                if (_material != null)
                {
                    var c = Color.Lerp(_from, _to, t);
                    c.a = Mathf.Lerp(_from.a, 0f, t);
                    _material.color = c;
                }

                if (t >= 1f)
                {
                    if (_bakedMeshToDestroy != null) Destroy(_bakedMeshToDestroy);
                    Destroy(gameObject);
                }
            }
        }
    }
}
