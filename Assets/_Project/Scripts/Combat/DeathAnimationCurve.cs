using UnityEngine;

namespace Enigma.Combat
{
    // 死亡演出の補間カーブ（Unity 依存なし・テスト可能な plain C#）。
    // t は正規化時間 0..1 を前提とし、範囲外は端点へクランプする。
    public static class DeathAnimationCurve
    {
        // 倒れ角度 0..90 度。立っているうちはゆっくり、傾くほど加速する自然な転倒のため
        // イーズイン（t*t）にする。
        public static float ToppleAngle(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * 90f;
        }

        // 不透明 1 → 透明 0。倒れ切る直前まで見えていて欲しいので後半で急減衰させる
        // （1 - t^2 = (1-t)(1+t) は後半ほど傾きが大きい）。
        public static float FadeAlpha(float t)
        {
            t = Mathf.Clamp01(t);
            return 1f - t * t;
        }

        // 構造物の沈下量 0 → depth。倒壊が進むほど沈む見せ方に合わせてイーズイン。
        public static float SinkDepth(float t, float depth)
        {
            t = Mathf.Clamp01(t);
            return t * t * depth;
        }
    }
}
