using UnityEngine;

namespace Enigma.Combat
{
    // 死亡時に付与するゴールド量を保持するデータコンポーネント。
    // XpReward と対称設計にしてビルダーから SetAmount で差し替えられる。
    public sealed class GoldReward : MonoBehaviour
    {
        [SerializeField] private int _amount = 20;

        public int Amount => _amount;

        public void SetAmount(int value)
        {
            _amount = value;
        }
    }
}
