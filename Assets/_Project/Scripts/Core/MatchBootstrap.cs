using UnityEngine;
using Enigma.Ability;
using Enigma.Character;
using Enigma.Combat;

namespace Enigma.Core
{
    // シーン開始時にピック済みキャラのスキル・ステータスをプレイヤーへ注入する composition root。
    // GameServices が未初期化でも安全に動作するよう null チェックを挟む。
    public sealed class MatchBootstrap : MonoBehaviour
    {
        [SerializeField] private SkillCaster      _skillCaster;
        [SerializeField] private HealthComponent  _health;
        [SerializeField] private AutoAttack       _autoAttack;
        [SerializeField] private PlayerController  _playerController;

        private void Start()
        {
            // シーン単体起動時など未初期化パスへの保険
            if (!GameServices.IsInitialized)
                GameServices.Initialize();

            var picked = GameServices.Match?.PickedCharacter;
            if (picked == null) return;

            // 見た目（3Dモデル）を差し替える。UnityChan/未結線は既存モデル維持（戻り値 null）。
            ChampionModelSwapper.Apply(gameObject, picked);

            ApplySkills(picked);
            ApplyStats(picked);
            ApplyRelics();
        }

        // 試合前に選択したレリックの集約効果を適用する（最大HP/開始シールド/CDR）。
        private void ApplyRelics()
        {
            var ids = GameServices.Match?.SelectedRelicIds;
            if (ids == null) return;

            var health = _health != null ? _health : GetComponent<HealthComponent>();
            Enigma.Data.RelicApplier.ApplyIds(ids, health?.Model, _skillCaster, gameObject);
        }

        private void ApplySkills(CharacterData picked)
        {
            if (picked.Skills == null) return;

            // 1スロットでも非 null があれば差し替えを行う
            bool hasAny = false;
            foreach (var s in picked.Skills)
            {
                if (s != null) { hasAny = true; break; }
            }
            if (!hasAny) return;

            _skillCaster?.SetSkills(picked.Skills);
        }

        private void ApplyStats(CharacterData picked)
        {
            // HealthModel は maxHp 既定 200 で構築されるため、差分を AddMaxHp で加算してピック値へ寄せる
            var health = _health != null ? _health : GetComponent<HealthComponent>();
            if (health != null && picked.BaseHp > 0f)
            {
                float delta = picked.BaseHp - health.Model.MaxHp;
                if (Mathf.Abs(delta) > 0.001f)
                    health.Model.AddMaxHp(delta);
            }

            var autoAttack = _autoAttack != null ? _autoAttack : GetComponent<AutoAttack>();
            if (autoAttack != null)
            {
                autoAttack.Configure(picked.AttackDamage, picked.AttackRange, picked.AttackCooldown);
                autoAttack.SetChampion(picked.CharId);
            }

            var controller = _playerController != null ? _playerController : GetComponent<PlayerController>();
            if (controller != null && picked.MoveSpeed > 0f)
                controller.SetMoveSpeed(picked.MoveSpeed);
        }
    }
}
