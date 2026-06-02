using UnityEngine;

namespace Enigma.UI
{
    /// <summary>
    /// 設定値の保存・適用を担当。PlayerPrefs で永続化。
    /// </summary>
    public static class SettingsManager
    {
        // ── Keys ──────────────────────────────────────
        const string KEY_BGM    = "settings_bgm";
        const string KEY_SE     = "settings_se";
        const string KEY_VOICE  = "settings_voice";
        const string KEY_QUALITY = "settings_quality";
        const string KEY_WINDOW  = "settings_window";

        // ── Defaults ──────────────────────────────────
        public static float BgmVolume   { get; private set; } = 0.8f;
        public static float SeVolume    { get; private set; } = 1.0f;
        public static float VoiceVolume { get; private set; } = 1.0f;
        public static int   QualityLevel { get; private set; } = 2; // 高
        public static int   WindowMode   { get; private set; } = 1; // フルスクリーン

        // ── Load ──────────────────────────────────────
        public static void Load()
        {
            BgmVolume    = PlayerPrefs.GetFloat(KEY_BGM,    0.8f);
            SeVolume     = PlayerPrefs.GetFloat(KEY_SE,     1.0f);
            VoiceVolume  = PlayerPrefs.GetFloat(KEY_VOICE,  1.0f);
            QualityLevel = PlayerPrefs.GetInt(KEY_QUALITY,  2);
            WindowMode   = PlayerPrefs.GetInt(KEY_WINDOW,   1);
        }

        // ── Apply & Save ──────────────────────────────
        public static void Apply(float bgm, float se, float voice, int quality, int windowMode)
        {
            BgmVolume    = bgm;
            SeVolume     = se;
            VoiceVolume  = voice;
            QualityLevel = quality;
            WindowMode   = windowMode;

            // サウンド適用（AudioMixer未導入のため仮でAudioListener使用）
            AudioListener.volume = bgm;

            // 画質
            QualitySettings.SetQualityLevel(quality, true);

            // ウィンドウモード
            switch (windowMode)
            {
                case 0: // ウィンドウ
                    Screen.fullScreenMode = FullScreenMode.Windowed;
                    break;
                case 1: // フルスクリーン
                    Screen.fullScreenMode = FullScreenMode.ExclusiveFullScreen;
                    break;
                case 2: // ボーダーレス
                    Screen.fullScreenMode = FullScreenMode.FullScreenWindow;
                    break;
            }

            Save();
            Debug.Log($"[Settings] Applied — BGM:{bgm:P0} SE:{se:P0} Voice:{voice:P0} Quality:{quality} Window:{windowMode}");
        }

        static void Save()
        {
            PlayerPrefs.SetFloat(KEY_BGM,    BgmVolume);
            PlayerPrefs.SetFloat(KEY_SE,     SeVolume);
            PlayerPrefs.SetFloat(KEY_VOICE,  VoiceVolume);
            PlayerPrefs.SetInt(KEY_QUALITY,  QualityLevel);
            PlayerPrefs.SetInt(KEY_WINDOW,   WindowMode);
            PlayerPrefs.Save();
        }
    }
}
