using UnityEngine;

namespace Enigma.Data
{
    /// <summary>
    /// 試合中に参照するレリックの「持続効果」を保持するマーカーコンポーネント。
    /// 開始時の即時効果(最大HP/シールド/CDR)は RelicApplier がその場で適用するが、
    /// キル時加速のようにイベント時へ遅延する効果はここに値を置き、フック側が読む。
    /// </summary>
    public sealed class PlayerRelicEffects : MonoBehaviour
    {
        /// <summary>キル時に得る移動加速の割合（0.25 = +25%）。0 なら効果なし。</summary>
        public float MoveSpeedOnKill { get; private set; }

        /// <summary>中立ユニットへの与ダメ増加割合（0.20 = +20%）。0 なら効果なし。</summary>
        public float NeutralDamageBonus { get; private set; }

        /// <summary>キル時加速の持続秒。</summary>
        public const float MoveSpeedOnKillDuration = 4f;

        public void SetMoveSpeedOnKill(float fraction)
        {
            MoveSpeedOnKill = fraction < 0f ? 0f : fraction;
        }

        public void SetNeutralDamageBonus(float fraction)
        {
            NeutralDamageBonus = fraction < 0f ? 0f : fraction;
        }

        public static PlayerRelicEffects GetOrAdd(GameObject go)
        {
            if (go == null) return null;
            var c = go.GetComponent<PlayerRelicEffects>();
            return c != null ? c : go.AddComponent<PlayerRelicEffects>();
        }
    }
}
