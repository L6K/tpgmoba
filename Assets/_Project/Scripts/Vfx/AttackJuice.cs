using UnityEngine;
using Enigma.Character;

namespace Enigma.Vfx
{
    /// <summary>
    /// 「自分(操作プレイヤー)が攻撃を当てた」瞬間の手応え演出をまとめる静的ファサード。
    /// カメラの微シェイク + 大きな一撃のみヒットストップ。値は控えめ・調整しやすい定数で持つ。
    /// 演出量は <see cref="HitStopModel"/>(テスト済純ロジック)に委譲する。
    /// 弱い通常攻撃(AA)で毎回フリーズすると重く感じるため、ヒットストップは割合 or クリットで門番する。
    /// </summary>
    public static class AttackJuice
    {
        /// <summary>不具合時に1か所で無効化できるよう公開フラグにしておく。</summary>
        public static bool Enabled = true;

        // ダメージ割合がこの値以上(またはクリット)のときだけヒットストップを許可する。
        private const float HitStopMinFraction = 0.12f;
        // シェイク量 = ダメージ * 係数 を [Min,Max] にクランプ。AA は小さく、大技は大きく。
        private const float ShakePerDamage = 0.004f;
        private const float ShakeMin = 0.03f;
        private const float ShakeMax = 0.20f;

        private static OrbitCamera _camera;

        /// <summary>操作プレイヤーの攻撃が命中した時に呼ぶ。targetMaxHp&lt;=0 は割合0扱い。</summary>
        public static void PlayerLandedHit(float damage, float targetMaxHp, bool isCrit)
        {
            if (!Enabled || damage <= 0f) return;

            // カメラ微シェイク(未解決なら遅延取得。シーン再ロードで失効しても再取得する)
            if (_camera == null) _camera = Object.FindFirstObjectByType<OrbitCamera>();
            if (_camera != null)
                _camera.AddShake(Mathf.Clamp(damage * ShakePerDamage, ShakeMin, ShakeMax));

            // 大きな一撃 or クリットのみヒットストップ
            float frac = targetMaxHp > 0f ? damage / targetMaxHp : 0f;
            if (isCrit || frac >= HitStopMinFraction)
                HitStopController.Instance?.Request(HitStopModel.Seconds(damage, targetMaxHp, isCrit));
        }
    }
}
