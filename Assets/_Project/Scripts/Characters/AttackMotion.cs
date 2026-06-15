using System;

namespace Enigma.Character
{
    public enum AttackPhase { None, Windup, Recovery }

    /// <summary>
    /// 攻撃3段階モーション（準備→攻撃瞬間→硬直）のロジック。
    /// plain C# クラスとして実装し、MonoBehaviour 非依存でテスト可能にする。
    /// </summary>
    public sealed class AttackMotion
    {
        private AttackPhase _phase = AttackPhase.None;
        private float       _timer;
        private float       _recoverySeconds;
        private Action      _onStrike;

        public AttackPhase Phase         => _phase;
        public bool        MovementLocked => _phase == AttackPhase.Windup;

        /// <summary>
        /// 攻撃開始を要求する。
        /// None または Recovery 中のみ受理（Recovery 中は現在のリカバリを破棄して新 Windup へ）。
        /// Windup 中は false を返す。
        /// </summary>
        public bool TryBegin(float windupSeconds, float recoverySeconds, Action onStrike)
        {
            if (_phase == AttackPhase.Windup) return false;

            _phase           = AttackPhase.Windup;
            _timer           = windupSeconds;
            _recoverySeconds = recoverySeconds;
            _onStrike        = onStrike;
            return true;
        }

        /// <summary>
        /// フレーム更新。Windup 完了で onStrike を実行して Recovery へ移行。
        /// Recovery 完了で None へ。
        /// </summary>
        public void Tick(float deltaSeconds)
        {
            if (_phase == AttackPhase.None) return;

            _timer -= deltaSeconds;
            if (_timer > 0f) return;

            if (_phase == AttackPhase.Windup)
            {
                // Strike は瞬間なので状態を持たず、コールバック実行後即 Recovery へ
                _onStrike?.Invoke();
                _onStrike = null;
                _phase    = AttackPhase.Recovery;
                _timer    = _recoverySeconds;
            }
            else if (_phase == AttackPhase.Recovery)
            {
                _phase = AttackPhase.None;
                _timer = 0f;
            }
        }

        /// <summary>
        /// Recovery 中のみ None へキャンセル（移動入力によるキャンセル用）。
        /// Windup 中は何もしない。
        /// </summary>
        public void CancelRecovery()
        {
            if (_phase == AttackPhase.Recovery)
            {
                _phase = AttackPhase.None;
                _timer = 0f;
            }
        }
    }
}
