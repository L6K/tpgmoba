using System.Collections.Generic;
using Enigma.Data;

namespace Enigma.Tests
{
    /// <summary>
    /// Dictionary ベースの ISaveStore テスト用フェイク。
    /// </summary>
    public sealed class FakeSaveStore : ISaveStore
    {
        private readonly Dictionary<string, int>   _ints   = new();
        private readonly Dictionary<string, float> _floats = new();

        public int SaveCallCount { get; private set; }

        public int   GetInt(string key, int defaultValue)     => _ints.TryGetValue(key, out var v)   ? v : defaultValue;
        public void  SetInt(string key, int value)            => _ints[key]   = value;
        public float GetFloat(string key, float defaultValue) => _floats.TryGetValue(key, out var v) ? v : defaultValue;
        public void  SetFloat(string key, float value)        => _floats[key] = value;
        public void  Save()                                   => SaveCallCount++;
    }
}
