using UnityEngine;

namespace Enigma.Vision
{
    /// <summary>
    /// 視界レイキャストで遮蔽体として扱う構造物に付与する空マーカー。
    /// FogOfWarDirector の地形遮蔽判定(ILineOfSightChecker)がこのタグの有無で当たり判定を絞り込む。
    /// </summary>
    public sealed class VisionBlockerTag : MonoBehaviour
    {
    }
}
