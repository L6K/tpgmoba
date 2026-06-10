namespace Enigma.Data
{
    /// <summary>
    /// 設定値の読み込み・適用・保存を担当するサービスの抽象。
    /// </summary>
    public interface ISettingsService
    {
        float BgmVolume    { get; }
        float SeVolume     { get; }
        float VoiceVolume  { get; }
        int   QualityLevel { get; }
        int   WindowMode   { get; }

        void Load();
        void Apply(float bgm, float se, float voice, int quality, int windowMode);
    }
}
