namespace Enigma.Combat
{
    /// <summary>
    /// ダメージ受付を開閉するゲート。<see cref="HealthComponent"/> に注入され、
    /// <see cref="AllowsDamage"/> が false の間、受けたダメージは 0 化される。
    /// null 注入(未設定)時は従来通りゲートなしで全ダメージが通る。
    /// </summary>
    public interface IDamageGate
    {
        bool AllowsDamage { get; }
    }
}
