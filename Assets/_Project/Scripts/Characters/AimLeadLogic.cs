using UnityEngine;

namespace Enigma.Character
{
    // 方向指定スキルの Bot 照準用リード計算（一次予測、plain C#）。
    // Bot が対象の「現在位置」へそのまま撃つと、カイト中の対象を外し続ける問題への対処。
    public static class AimLeadLogic
    {
        // 過剰リード防止のための到達時間上限（秒）
        private const float MaxLeadTime = 1.5f;

        // shooterPos から targetPos への到達時間 t ≈ dist/projectileSpeed だけ、
        // targetVelocity 方向へ targetPos を進めた点を狙い点として返す（一次予測）。
        // projectileSpeed<=0（実質即着弾）は targetPos をそのまま返す。
        public static Vector3 PredictAimPoint(Vector3 shooterPos, Vector3 targetPos, Vector3 targetVelocity, float projectileSpeed)
        {
            if (projectileSpeed <= 0f) return targetPos;

            float dist = Vector3.Distance(shooterPos, targetPos);
            float t    = dist / projectileSpeed;
            if (t > MaxLeadTime) t = MaxLeadTime;

            return targetPos + targetVelocity * t;
        }
    }
}
