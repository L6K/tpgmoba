using System.Collections.Generic;
using Enigma.Data;

namespace Enigma.Tests
{
    /// <summary>
    /// 事前に並べた値を順に返す IRandomSource テスト用フェイク。
    /// キューが空の場合は 0 を返す。
    /// </summary>
    public sealed class FakeRandomSource : IRandomSource
    {
        private readonly Queue<int> _values;

        public FakeRandomSource(params int[] values)
        {
            _values = new Queue<int>(values);
        }

        public int Next(int maxExclusive) => _values.Count > 0 ? _values.Dequeue() : 0;
    }
}
