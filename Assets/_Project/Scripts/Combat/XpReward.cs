using UnityEngine;

namespace Enigma.Combat
{
    // 死亡時にXPを付与する量を保持するだけのデータコンポーネント。
    // ビルダーから SetAmount で差し替えられる。
    public sealed class XpReward : MonoBehaviour
    {
        [SerializeField] private float _amount = 20f;

        public float Amount => _amount;

        public void SetAmount(float value)
        {
            _amount = value;
        }
    }
}
