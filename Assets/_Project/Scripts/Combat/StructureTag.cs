using UnityEngine;

namespace Enigma.Combat
{
    // 構造物(タワー/タイタン)であることを示す空マーカー。DamageUtility が対構造物バフ
    // (StructureDamage)の適用可否をここで判定する。ロジックは持たない Humble Object。
    public sealed class StructureTag : MonoBehaviour
    {
    }
}
