using UnityEngine;

namespace Enigma.Data
{
    /// <summary>
    /// 設定値の保存・適用を担当。PlayerPrefs で永続化。
    /// </summary>
    public sealed class SettingsService : ISettingsService
    {
        private const string BgmKey     = "settings_bgm";
        private const string SeKey      = "settings_se";
        private const string VoiceKey   = "settings_voice";
        private const string QualityKey = "settings_quality";
        private const string WindowKey  = "settings_window";

        private readonly ISaveStore            _store;
        private readonly ISystemSettingsApplier _applier;

        public float BgmVolume    { get; private set; } = 0.8f;
        public float SeVolume     { get; private set; } = 1.0f;
        public float VoiceVolume  { get; private set; } = 1.0f;
        public int   QualityLevel { get; private set; } = 2;
        public int   WindowMode   { get; private set; } = 1;

        public SettingsService(ISaveStore store, ISystemSettingsApplier applier)
        {
            _store   = store;
            _applier = applier;
        }

        public void Load()
        {
            BgmVolume    = _store.GetFloat(BgmKey,    0.8f);
            SeVolume     = _store.GetFloat(SeKey,     1.0f);
            VoiceVolume  = _store.GetFloat(VoiceKey,  1.0f);
            QualityLevel = _store.GetInt(QualityKey,  2);
            WindowMode   = _store.GetInt(WindowKey,   1);
        }

        public void Apply(float bgm, float se, float voice, int quality, int windowMode)
        {
            BgmVolume    = bgm;
            SeVolume     = se;
            VoiceVolume  = voice;
            QualityLevel = quality;
            WindowMode   = windowMode;

            _applier.ApplyVolume(bgm, se, voice);
            _applier.ApplyQuality(quality);
            _applier.ApplyWindowMode(windowMode);

            _store.SetFloat(BgmKey,    bgm);
            _store.SetFloat(SeKey,     se);
            _store.SetFloat(VoiceKey,  voice);
            _store.SetInt(QualityKey,  quality);
            _store.SetInt(WindowKey,   windowMode);
            _store.Save();

            Debug.Log($"[Settings] Applied — BGM:{bgm:P0} SE:{se:P0} Voice:{voice:P0} Quality:{quality} Window:{windowMode}");
        }
    }
}
