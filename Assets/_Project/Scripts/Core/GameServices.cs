using Enigma.Data;

namespace Enigma.Core
{
    // composition root だけは static を許容する。
    // アプリ全体で唯一の組み立て地点であり、MonoBehaviour に依存せず
    // どのシーンからでも呼べる必要があるため。
    public static class GameServices
    {
        public static ISettingsService    Settings   { get; private set; }
        public static ICharacterOwnership Ownership  { get; private set; }
        public static IGachaService       Gacha      { get; private set; }

        public static bool IsInitialized => Settings != null && Ownership != null && Gacha != null;

        /// <summary>標準実装（PlayerPrefs / Unity API）で組み立てる。</summary>
        public static void Initialize()
        {
            var store     = new PlayerPrefsSaveStore();
            var applier   = new UnitySystemSettingsApplier();
            var ownership = new CharacterOwnershipService(store);
            var gacha     = new GachaService(store, ownership, new SystemRandomSource());
            var settings  = new SettingsService(store, applier);

            Initialize(settings, ownership, gacha);
        }

        /// <summary>テスト・差し替え用。任意の実装を注入できる。</summary>
        public static void Initialize(
            ISettingsService    settings,
            ICharacterOwnership ownership,
            IGachaService       gacha)
        {
            Settings  = settings;
            Ownership = ownership;
            Gacha     = gacha;
        }

        /// <summary>テスト後のクリーンアップ用。</summary>
        public static void Reset()
        {
            Settings  = null;
            Ownership = null;
            Gacha     = null;
        }
    }
}
