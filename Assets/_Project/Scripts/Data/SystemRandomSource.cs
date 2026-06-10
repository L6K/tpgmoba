using SystemRandom = System.Random;

namespace Enigma.Data
{
    /// <summary>
    /// IRandomSource の System.Random 実装。
    /// </summary>
    public sealed class SystemRandomSource : IRandomSource
    {
        private readonly SystemRandom _random = new();

        public int Next(int maxExclusive) => _random.Next(maxExclusive);
    }
}
