using UnityEngine;

namespace Enigma.Combat
{
    // thorne Q(チェーンフック)等、命中した敵を発射者側へ引き寄せるプル量を計算する純ロジック。
    // Unity API に非依存（EditMode テストから直接検証できる）。
    // XZ平面(水平)のみで計算し、y は targetPos のまま返す（地形の起伏は呼び出し側の
    // CharacterController + 重力に任せ、テレポート的な y 直指定はしない）。
    public static class PullDisplacementLogic
    {
        /// <summary>
        /// caster から target へのベクトル上で、target を最大 pullDistance だけ caster 側へ
        /// 引き寄せた新しい位置を返す。caster-target 間の水平距離が pullDistance 未満なら
        /// めり込み防止のため minSeparation だけ手前で止める。
        /// caster と target が水平位置で一致する(=方向が定まらない)場合は targetPos をそのまま返す。
        /// </summary>
        public static Vector3 PullTarget(Vector3 casterPos, Vector3 targetPos, float pullDistance, float minSeparation)
        {
            Vector2 caster = new Vector2(casterPos.x, casterPos.z);
            Vector2 target = new Vector2(targetPos.x, targetPos.z);

            Vector2 toCaster = caster - target;
            float dist = toCaster.magnitude;

            if (dist < 0.0001f || pullDistance <= 0f)
                return targetPos;

            Vector2 dir = toCaster / dist;

            // 発射者との距離が pullDistance 未満なら、minSeparation まで手前で停止する
            float travel = Mathf.Min(pullDistance, Mathf.Max(0f, dist - minSeparation));

            Vector2 result = target + dir * travel;
            return new Vector3(result.x, targetPos.y, result.y);
        }
    }
}
