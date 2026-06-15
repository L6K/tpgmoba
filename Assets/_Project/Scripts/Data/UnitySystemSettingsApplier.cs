using UnityEngine;

namespace Enigma.Data
{
    /// <summary>
    /// ISystemSettingsApplier の Unity 実装。
    /// AudioMixer 未導入のため AudioListener.volume を BGM ボリュームとして使用（暫定）。
    /// </summary>
    public sealed class UnitySystemSettingsApplier : ISystemSettingsApplier
    {
        public void ApplyVolume(float bgm, float se, float voice)
        {
            AudioListener.volume = bgm;
        }

        public void ApplyQuality(int level)
        {
            QualitySettings.SetQualityLevel(level, true);
        }

        public void ApplyWindowMode(int mode)
        {
            switch (mode)
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
        }
    }
}
