using UnityEngine;
using Enigma.Ability;

namespace Enigma.Core
{
    // シーン開始時にピック済みキャラのスキルを SkillCaster へ注入する composition root。
    // GameServices が未初期化でも安全に動作するよう null チェックを挟む。
    public sealed class MatchBootstrap : MonoBehaviour
    {
        [SerializeField] private SkillCaster _skillCaster;

        private void Start()
        {
            // シーン単体起動時など未初期化パスへの保険
            if (!GameServices.IsInitialized)
                GameServices.Initialize();

            var picked = GameServices.Match?.PickedCharacter;
            if (picked == null || picked.Skills == null) return;

            // 1スロットでも非 null があれば差し替えを行う
            bool hasAny = false;
            foreach (var s in picked.Skills)
            {
                if (s != null) { hasAny = true; break; }
            }
            if (!hasAny) return;

            _skillCaster?.SetSkills(picked.Skills);
        }
    }
}
