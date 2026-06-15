using System;

namespace Enigma.Character
{
    /// <summary>
    /// アイドル継続中に、ベース Idle を数ループ再生するごとにアイドルバリアントを 1 つ挟む
    /// タイミング/選択ロジックを担う plain C# クラス（Unity 非依存・テスト対象）。
    ///
    /// LocomotionClipSwitcher から切り離すことで、ループ計数とバリアント選択順を
    /// EditMode テストで検証可能にしている。Playables/クロスフェード自体は呼び出し側が担う。
    ///
    /// 動作: ベース Idle のループ完了ごとに <see cref="NotifyBaseLoopCompleted"/> を呼ぶと、
    /// 設定された閾値（2〜4 ループ）に達した時点で次に挟むバリアント番号を返す。
    /// バリアントを 1 つ再生し終えたら <see cref="NotifyVariantCompleted"/> を呼んでベースへ戻す。
    /// </summary>
    public sealed class IdleVariantSequencer
    {
        private readonly int _variantCount;
        private readonly int _minLoops;
        private readonly int _maxLoops;

        // 固定シードの System.Random（UnityEngine.Random 禁止規約に従う）。
        // バリアント選択を順繰りでなく擬似ランダムにする場合に使用。null なら順繰り。
        private readonly Random _random;

        private int _baseLoops;       // 直近のベース Idle 連続ループ回数
        private int _loopsUntilNext;  // 次にバリアントを挟むまでに必要なループ回数
        private int _sequenceIndex;   // 順繰り選択用の位置

        /// <param name="variantCount">利用可能なバリアント数（0 以下なら常にバリアントを挟まない）。</param>
        /// <param name="seed">
        /// 0 以外を渡すと固定シードの System.Random でバリアントを擬似ランダム選択。
        /// 0 を渡すと順繰り選択（決定的）。
        /// </param>
        /// <param name="minLoops">バリアントを挟むまでの最小ベースループ数（既定 2）。</param>
        /// <param name="maxLoops">バリアントを挟むまでの最大ベースループ数（既定 4）。</param>
        public IdleVariantSequencer(int variantCount, int seed = 0, int minLoops = 2, int maxLoops = 4)
        {
            _variantCount = variantCount < 0 ? 0 : variantCount;
            // min/max を [1, ...] にクランプし、min <= max を保証する
            _minLoops = minLoops < 1 ? 1 : minLoops;
            _maxLoops = maxLoops < _minLoops ? _minLoops : maxLoops;
            _random   = seed != 0 ? new Random(seed) : null;
            _loopsUntilNext = RollLoopThreshold();
        }

        /// <summary>バリアントを 1 つでも持っているか。</summary>
        public bool HasVariants => _variantCount > 0;

        /// <summary>
        /// ベース Idle が 1 ループ完了したことを通知する。
        /// 挟むべきタイミングに達したら挟むバリアントの index（0..variantCount-1）を返し、
        /// まだならば -1 を返す。-1 のときは引き続きベース Idle を再生する。
        /// </summary>
        public int NotifyBaseLoopCompleted()
        {
            if (!HasVariants) return -1;

            _baseLoops++;
            if (_baseLoops < _loopsUntilNext) return -1;

            _baseLoops = 0;
            return SelectVariant();
        }

        /// <summary>
        /// バリアントの再生が終わったことを通知する。次回挟むまでのループ閾値を引き直す。
        /// </summary>
        public void NotifyVariantCompleted()
        {
            _loopsUntilNext = RollLoopThreshold();
        }

        // 次に挟むバリアント index を決める。_random があれば擬似ランダム、なければ順繰り。
        private int SelectVariant()
        {
            if (_random != null)
                return _random.Next(_variantCount);

            int picked = _sequenceIndex % _variantCount;
            _sequenceIndex = (_sequenceIndex + 1) % _variantCount;
            return picked;
        }

        // 次にバリアントを挟むまでのベースループ数を [min, max] から決める。
        // _random があれば範囲内で擬似ランダム、なければ min 固定（決定的）。
        private int RollLoopThreshold()
        {
            if (_minLoops == _maxLoops) return _minLoops;
            if (_random != null)
                return _random.Next(_minLoops, _maxLoops + 1);
            return _minLoops;
        }
    }
}
