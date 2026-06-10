using UnityEngine.InputSystem;

namespace Enigma.Data
{
    public enum CastMode { Quick, QuickWithIndicator, Normal }

    public interface IControlSettingsService
    {
        CastMode CastMode { get; }
        void SetCastMode(CastMode mode);

        Key GetSkillKey(int slot);
        void SetSkillKey(int slot, Key key);
    }
}
