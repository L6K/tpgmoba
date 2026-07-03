using System.Collections.Generic;
using UnityEngine;

namespace Enigma.Map
{
    /// <summary>
    /// 茂みゾーンの器。視界ルール(茂み内は外部から見えにくくする等)は次スライスM-Vで実装するため、
    /// ここでは領域判定(Contains)と全ゾーンの登録簿(Active)のみを提供する。
    /// </summary>
    public sealed class BrushZone : MonoBehaviour
    {
        [SerializeField] private float _radius = 3f;

        public float Radius => _radius;

        public static readonly List<BrushZone> Active = new List<BrushZone>();

        private void OnEnable()
        {
            Active.Add(this);
        }

        private void OnDisable()
        {
            Active.Remove(this);
        }

        /// <summary>水平距離(y無視)のみで判定する。地形の起伏に追従して置かれるゾーンのため。</summary>
        public bool Contains(Vector3 pos)
        {
            float dx = pos.x - transform.position.x;
            float dz = pos.z - transform.position.z;
            return (dx * dx + dz * dz) <= _radius * _radius;
        }
    }
}
