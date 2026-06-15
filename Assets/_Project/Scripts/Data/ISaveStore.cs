namespace Enigma.Data
{
    /// <summary>
    /// 永続ストアの抽象。PlayerPrefs / SQLite 等に差し替え可能にするため。
    /// </summary>
    public interface ISaveStore
    {
        int   GetInt(string key, int defaultValue);
        void  SetInt(string key, int value);
        float GetFloat(string key, float defaultValue);
        void  SetFloat(string key, float value);
        void  Save();
    }
}
