using UnityEngine;

namespace Enigma.Character
{
    // 方向指定スキルの Bot 照準用リード計算（一次予測、plain C#）。
    // Bot が対象の「現在位置」へそのまま撃つと、カイト中の対象を外し続ける問題への対処。
    public static class AimLeadLogic
    {
        // 過剰リード防止のための到達時間上限（秒）
        private const float MaxLeadTime = 1.5f;

        // GroundAoe の起爆遅延リード上限（秒）。方向指定の MaxLeadTime とは別枠。
        private const float MaxGroundLeadDelay = 1.2f;

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

        // 地点指定スキル（GroundAoe）用のリード計算。TelegraphCircle の起爆遅延
        // (delaySeconds) の間に対象が targetVelocity で進む分を見越した地点を返す。
        // delaySeconds は 0〜MaxGroundLeadDelay にクランプ（負値は0扱い、過剰リードは1.2sで頭打ち）。
        public static Vector3 PredictGroundPoint(Vector3 targetPos, Vector3 targetVelocity, float delaySeconds)
        {
            float delay = Mathf.Clamp(delaySeconds, 0f, MaxGroundLeadDelay);
            return targetPos + targetVelocity * delay;
        }

        // 発射者から狙い点までの距離がスキル射程を超える場合、射程内に収める
        // （発射者→狙い点の方向を保ったまま距離だけ range にクランプ）。
        // range<=0 または距離が既に range 以下ならそのまま point を返す。
        public static Vector3 ClampToRange(Vector3 shooterPos, Vector3 point, float range)
        {
            if (range <= 0f) return point;

            Vector3 offset = point - shooterPos;
            float   dist   = offset.magnitude;
            if (dist <= range) return point;

            return shooterPos + offset.normalized * range;
        }
    }
}
