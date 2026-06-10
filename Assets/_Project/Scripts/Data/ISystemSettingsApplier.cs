namespace Enigma.Data
{
    /// <summary>
    /// Unity API を通じてシステム設定を適用する抽象。テストでフェイクに差し替え可能にするため。
    /// </summary>
    public interface ISystemSettingsApplier
    {
        void ApplyVolume(float bgm, float se, float voice);
        void ApplyQuality(int level);
        void ApplyWindowMode(int mode);
    }
}
