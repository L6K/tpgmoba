using UnityEngine;
using UnityEngine.SceneManagement;
using Enigma.Character;

namespace Enigma.Combat
{
    /// <summary>
    /// 死亡中はプレイヤーの行動系コンポーネントを無効化し、リスポーン（Revived）で復帰させる Humble Object。
    /// 見た目の倒れ演出は DeathPresenter に委譲する（本クラスは入力/行動の停止のみを担う）。
    ///
    /// プレイヤー専用（EnemyChampionAI には自己結線しない）: Bot は既に内部の _isDead ガードで
    /// 移動/知覚/攻撃/リコールを止めており、EnemyChampionAI.enabled=false にすると Update が
    /// 止まって RespawnRoutine 完了後の復帰処理まで動かなくなる危険がある。
    /// プレイヤーは PlayerController / AutoAttack / SkillCaster / PlayerAttackMotor が各々 Update で
    /// 入力を直接ポーリングする構造で死亡ガードを持たないため、本クラスが enabled=false で止める。
    /// </summary>
    [RequireComponent(typeof(HealthComponent))]
    [RequireComponent(typeof(PlayerController))]
    public sealed class DeathStateController : MonoBehaviour
    {
        private HealthComponent _health;
        private CharacterController _cc;
        private Enigma.Ability.SkillCaster _skillCaster;
        private PlayerAttackMotor _attackMotor;
        private Behaviour[] _actionComponents;

        private void Awake()
        {
            _health = GetComponent<HealthComponent>();
            _cc     = GetComponent<CharacterController>();
            _skillCaster = GetComponent<Enigma.Ability.SkillCaster>();
            _attackMotor = GetComponent<PlayerAttackMotor>();

            // 存在するものだけ集める（キャラ構成によっては AutoAttack/SkillCaster が無いことがある）
            var list = new System.Collections.Generic.List<Behaviour>();
            void AddIfPresent(Behaviour b) { if (b != null) list.Add(b); }
            AddIfPresent(GetComponent<PlayerController>());
            AddIfPresent(GetComponent<AutoAttack>());
            AddIfPresent(_skillCaster);
            AddIfPresent(_attackMotor);
            _actionComponents = list.ToArray();
        }

        private void OnEnable()
        {
            if (_health == null) _health = GetComponent<HealthComponent>();
            _health.Model.Died += OnDied;
            _health.Model.Revived += OnRevived;
        }

        private void OnDisable()
        {
            if (_health?.Model == null) return;
            _health.Model.Died -= OnDied;
            _health.Model.Revived -= OnRevived;
        }

        private void OnDied()
        {
            // enabled=false は Update を止めるだけで、既にアーム中のスキルインジケーター表示は
            // 残ってしまうため、無効化前に明示的にキャンセルしておく
            _skillCaster?.CancelArmedForDeath();

            // Windup/Recovery 中に死亡した場合、enabled=false で Tick が止まるだけだと
            // リスポーン後に古いターゲットへの攻撃が再開してしまうため強制中断する
            _attackMotor?.Motion.ForceCancel();

            SetActionEnabled(false);
            // 死体の押し合い防止（PlayerRespawn のワープ処理とも競合しない。ワープ前後で再度 false/true される）
            if (_cc != null) _cc.enabled = false;
        }

        private void OnRevived()
        {
            if (_cc != null) _cc.enabled = true;
            SetActionEnabled(true);
        }

        private void SetActionEnabled(bool enabled)
        {
            foreach (var b in _actionComponents)
                if (b != null) b.enabled = enabled;
        }

        // ── 自己結線（シーン/プレハブ変更なしで完結させる） ──────────────────

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void RegisterSceneLoadHook()
        {
            // BalanceSimRunner がシーンを再ロードする度に呼ばれるよう、初回のみ購読する
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            AttachToScene();
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => AttachToScene();

        private static void AttachToScene()
        {
            if (SceneManager.GetActiveScene().name != "AetherRift_Map") return;

            foreach (var pc in Object.FindObjectsByType<PlayerController>(FindObjectsSortMode.None))
            {
                var go = pc.gameObject;
                if (go.GetComponent<HealthComponent>() == null) continue; // 対象外（HealthComponent 必須）
                if (go.GetComponent<DeathStateController>() != null) continue;
                go.AddComponent<DeathStateController>();
            }
        }
    }
}
