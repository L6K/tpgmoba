using System.Collections;
using UnityEngine;

namespace Enigma.Combat
{
    // 扇形予兆: delay 後に扇形内の IDamageable へダメージを与えて消滅
    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(MeshRenderer))]
    public sealed class TelegraphSector : MonoBehaviour
    {
        private const int Segments = 16;

        private Vector3    _origin;
        private Vector3    _direction;
        private float      _angleDegrees;
        private float      _radius;
        private float      _damage;
        private GameObject _owner;

        public void Init(
            Vector3 origin,
            Vector3 direction,
            float angleDegrees,
            float radius,
            float delaySeconds,
            float damage,
            GameObject owner)
        {
            _origin       = origin;
            _direction    = direction.normalized;
            _angleDegrees = angleDegrees;
            _radius       = radius;
            _damage       = damage;
            _owner        = owner;

            transform.position = new Vector3(origin.x, origin.y + 0.06f, origin.z);

            BuildMesh();
            StartCoroutine(ExplodeAfter(delaySeconds));
        }

        private void BuildMesh()
        {
            var mesh = new Mesh { name = "SectorMesh" };

            // 中心 + Segments+1 頂点で扇形を構成
            var vertices  = new Vector3[Segments + 2];
            var triangles = new int[Segments * 3];

            vertices[0] = Vector3.zero;

            float halfAngle     = _angleDegrees * 0.5f;
            // XZ 平面上の角度を計算するため direction の XZ 成分の向きを基準にする
            float baseAngle = Mathf.Atan2(_direction.z, _direction.x) * Mathf.Rad2Deg;

            for (int i = 0; i <= Segments; i++)
            {
                float t     = (float)i / Segments;
                float angle = (baseAngle - halfAngle + _angleDegrees * t) * Mathf.Deg2Rad;
                // ローカル座標は transform が origin 上にあるため (0,0,0) 起点
                vertices[i + 1] = new Vector3(Mathf.Cos(angle) * _radius, 0f, Mathf.Sin(angle) * _radius);
            }

            for (int i = 0; i < Segments; i++)
            {
                triangles[i * 3]     = 0;
                triangles[i * 3 + 1] = i + 1;
                triangles[i * 3 + 2] = i + 2;
            }

            mesh.vertices  = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();

            GetComponent<MeshFilter>().mesh = mesh;
        }

        private IEnumerator ExplodeAfter(float delay)
        {
            yield return new WaitForSeconds(delay);

            var hits = Physics.OverlapSphere(_origin, _radius);
            foreach (var col in hits)
            {
                if (_owner != null && col.gameObject == _owner) continue;

                if (!IsInsideSector(_origin, _direction, _angleDegrees, _radius, col.transform.position))
                    continue;

                var damageable = col.GetComponentInParent<IDamageable>();
                if (damageable != null)
                    damageable.TakeDamage(_damage);
            }

            yield return new WaitForSeconds(0.15f);
            Destroy(gameObject);
        }

        /// <summary>
        /// 点 point が扇形（origin 中心、direction 中心軸、angleDegrees 開角、radius 半径）の内側かを判定する。
        /// テスト対象の純粋関数。
        /// </summary>
        public static bool IsInsideSector(
            Vector3 origin,
            Vector3 direction,
            float angleDegrees,
            float radius,
            Vector3 point)
        {
            var toPoint = point - origin;
            // XZ 平面上の距離のみで判定（高さは無視）
            var toPointXZ  = new Vector3(toPoint.x, 0f, toPoint.z);
            var dirXZ      = new Vector3(direction.x, 0f, direction.z).normalized;

            float dist = toPointXZ.magnitude;
            if (dist > radius) return false;

            // ゼロベクトルは中心なので扇形内
            if (dist < Mathf.Epsilon) return true;

            float angle = Vector3.Angle(dirXZ, toPointXZ);
            return angle <= angleDegrees * 0.5f;
        }
    }
}
