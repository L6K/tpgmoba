using Enigma.Data;
using Enigma.Combat;
using Enigma.GameModes;

namespace Enigma.Core
{
    // composition root だけは static を許容する。
    // アプリ全体で唯一の組み立て地点であり、MonoBehaviour に依存せず
    // どのシーンからでも呼べる必要があるため。
    public static class GameServices
    {
        public static ISettingsService        Settings        { get; private set; }
        public static ICharacterOwnership     Ownership       { get; private set; }
        public static IGachaService           Gacha           { get; private set; }
        public static IControlSettingsService ControlSettings { get; private set; }
        public static IMatchmakingService     Matchmaking     { get; private set; }
        public static IMatchContext           Match           { get; private set; }
        public static ObjectiveBuffModel      ObjectiveBuffs  { get; private set; }

        public static bool IsInitialized => Settings != null && Ownership != null && Gacha != null && ControlSettings != null;

        /// <summary>標準実装（PlayerPrefs / Unity API）で組み立てる。</summary>
        public static void Initialize()
        {
            var store           = new PlayerPrefsSaveStore();
            var applier         = new UnitySystemSettingsApplier();
            var ownership       = new CharacterOwnershipService(store);
            var gacha           = new GachaService(store, ownership, new SystemRandomSource());
            var settings        = new SettingsService(store, applier);
            var controlSettings = new ControlSettingsService(store);

            Initialize(settings, ownership, gacha, controlSettings,
                new MatchmakingService(new SystemRandomSource()), new MatchContext());
        }

        /// <summary>テスト・差し替え用。任意の実装を注入できる。</summary>
        public static void Initialize(
            ISettingsService        settings,
            ICharacterOwnership     ownership,
            IGachaService           gacha,
            IControlSettingsService controlSettings = null,
            IMatchmakingService     matchmaking     = null,
            IMatchContext           match           = null)
        {
            Settings        = settings;
            Ownership       = ownership;
            Gacha           = gacha;
            ControlSettings = controlSettings;
            Matchmaking     = matchmaking ?? new MatchmakingService(new SystemRandomSource());
            Match           = match       ?? new MatchContext();
            ObjectiveBuffs  = new ObjectiveBuffModel();
        }

        /// <summary>テスト後のクリーンアップ用。</summary>
        public static void Reset()
        {
            Settings        = null;
            Ownership       = null;
            Gacha           = null;
            ControlSettings = null;
            Matchmaking     = null;
            Match           = null;
            ObjectiveBuffs  = null;
        }
    }
}
