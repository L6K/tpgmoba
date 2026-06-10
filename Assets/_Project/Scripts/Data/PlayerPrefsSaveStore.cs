using UnityEngine;

namespace Enigma.Data
{
    /// <summary>
    /// ISaveStore の PlayerPrefs 実装。
    /// </summary>
    public sealed class PlayerPrefsSaveStore : ISaveStore
    {
        public int   GetInt(string key, int defaultValue)     => PlayerPrefs.GetInt(key, defaultValue);
        public void  SetInt(string key, int value)            => PlayerPrefs.SetInt(key, value);
        public float GetFloat(string key, float defaultValue) => PlayerPrefs.GetFloat(key, defaultValue);
        public void  SetFloat(string key, float value)        => PlayerPrefs.SetFloat(key, value);
        public void  Save()                                   => PlayerPrefs.Save();
    }
}
