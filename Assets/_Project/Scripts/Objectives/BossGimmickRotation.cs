namespace Enigma.Objective
{
    public enum BossGimmick { ChasingCircles, SectorCleave, StackMarker }

    // ギミックのローテーション順序を管理する plain C# クラス（テスト対象）
    public sealed class BossGimmickRotation
    {
        private static readonly BossGimmick[] Order =
        {
            BossGimmick.ChasingCircles,
            BossGimmick.SectorCleave,
            BossGimmick.StackMarker,
        };

        private int _index;

        public BossGimmick Next()
        {
            var current = Order[_index];
            _index = (_index + 1) % Order.Length;
            return current;
        }

        public void Reset() => _index = 0;
    }
}
