namespace Enigma.Data
{
    /// <summary>
    /// 乱数生成の抽象。テストでフェイクに差し替え可能にするため。
    /// </summary>
    public interface IRandomSource
    {
        int Next(int maxExclusive);
    }
}
