namespace Enigma.Character
{
    public sealed class AttackCooldown
    {
        private readonly float _cooldownSeconds;
        private float _lastConsumeTime = float.MinValue;

        public AttackCooldown(float cooldownSeconds)
        {
            _cooldownSeconds = cooldownSeconds;
        }

        /// <summary>
        /// 前回消費から cooldown 秒以上経過していれば true を返し、時刻を記録する。
        /// </summary>
        public bool TryConsume(float currentTime)
        {
            if (currentTime - _lastConsumeTime < _cooldownSeconds) return false;
            _lastConsumeTime = currentTime;
            return true;
        }

        /// <summary>消費せずに CD が明けているか確認する（アーム開始の判定用）。</summary>
        public bool IsReady(float currentTime)
        {
            return currentTime - _lastConsumeTime >= _cooldownSeconds;
        }
    }
}
