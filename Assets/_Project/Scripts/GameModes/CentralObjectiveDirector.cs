using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Enigma.Combat;
using Enigma.Core;
using Enigma.Objective;

namespace Enigma.GameModes
{
    /// <summary>
    /// 中央オブジェクト(ボス「エニグマ・コア」)の出現ライフサイクルを <see cref="ObjectiveSpawnTimerModel"/> で制御する。
    /// Dormant/Warning 中はボスを隠し(描画・当たり判定・AI を無効化)、Active で出現させる。
    /// 撃破されたら再出現をスケジュールする。チームバフ付与は NeutralBossController が担当(本クラスはタイミングのみ)。
    /// HUD は <see cref="State"/>/<see cref="SecondsUntilSpawn"/> を参照して中央コアの状態を表示する。
    /// </summary>
    public sealed class CentralObjectiveDirector : MonoBehaviour
    {
        // 試合開始から初回出現まで / 撃破後の再出現間隔 / 出現予告のリード秒
        [SerializeField] private float _firstSpawnDelay = 90f;
        [SerializeField] private float _respawnInterval = 150f;
        [SerializeField] private float _warningLead     = 15f;

        public static CentralObjectiveDirector Instance { get; private set; }

        public ObjectiveState State { get; private set; } = ObjectiveState.Dormant;
        public float SecondsUntilSpawn { get; private set; }
        /// <summary>中央オブジェクトが存在する試合か(ボスが見つかった場合のみ true)。</summary>
        public bool HasObjective => _boss != null;

        /// <summary>ボスの HealthComponent。非アクティブ/未解決/死亡時は null を返す(呼び側は攻撃可否をこれで判定する)。</summary>
        public HealthComponent BossHealth =>
            (_resolved && _bossActive && _bossHealth != null && _bossHealth.Model != null && !_bossHealth.Model.IsDead)
                ? _bossHealth
                : null;

        /// <summary>中央オブジェクトの累計撃破(制圧)回数。HUD がアナウンス検知に使う。</summary>
        public int CaptureCount { get; private set; }
        /// <summary>直近の制圧チーム。HUD がアナウンス色とテキストに使う。</summary>
        public TeamId LastCaptureTeam { get; private set; }

        /// <summary>
        /// 中央オブジェクト(ボス)のワールド座標を返す。Bot マクロが集合先を知るために参照する。
        /// ボス未解決のときは false（呼び側は大値距離として扱う）。
        /// </summary>
        public bool TryGetObjectivePosition(out Vector3 pos)
        {
            if (_resolved && _boss != null)
            {
                pos = _bossSpawnPos;
                return true;
            }
            pos = default;
            return false;
        }

        // AfterSceneLoad はプロセス開始後1回しか走らない。ディレクター自身は DontDestroyOnLoad
        // ではない通常シーンオブジェクトのため、BalanceSimRunner 等が2試合目のために
        // AetherRift_Map を再ロードすると破棄されたまま再生成されず、中央ボスが起動しなくなる
        // (実測: 2試合目以降 CoreCaptured が一切発生しない)。sceneLoaded 購読で毎回補充する。
        private static bool _sceneLoadedHooked;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoSpawn()
        {
            if (!_sceneLoadedHooked)
            {
                SceneManager.sceneLoaded += OnSceneLoaded;
                _sceneLoadedHooked = true;
            }
            TrySpawn();
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => TrySpawn();

        private static void TrySpawn()
        {
            if (SceneManager.GetActiveScene().name != "AetherRift_Map") return;
            if (FindObjectOfType<CentralObjectiveDirector>() != null) return;
            new GameObject("CentralObjectiveDirector").AddComponent<CentralObjectiveDirector>();
        }

        private ObjectiveSpawnTimerModel _timer;

        private NeutralBossController _boss;
        private HealthComponent       _bossHealth;
        private Collider[]            _bossColliders;
        private Renderer[]            _bossRenderers;
        private Quaternion            _bossUprightRot;
        private Vector3               _bossSpawnPos;
        private bool                  _bossActive;
        private bool                  _resolved;

        // チーム別の中央オブジェクト撃破回数。撃破報酬を回数で段階化するために保持する。
        private readonly Dictionary<TeamId, int> _killCountByTeam = new();

        private void Awake()
        {
            Instance = this;
            _timer = new ObjectiveSpawnTimerModel(_firstSpawnDelay, _respawnInterval, _warningLead);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (_bossHealth?.Model != null) _bossHealth.Model.Died -= OnBossKilled;
        }

        private void ResolveBoss()
        {
            _boss = FindObjectOfType<NeutralBossController>();
            if (_boss == null) return; // まだ生成前。次フレーム再試行

            var go = _boss.gameObject;
            _bossHealth     = go.GetComponent<HealthComponent>();
            _bossColliders  = go.GetComponentsInChildren<Collider>(true); // 子Colliderも含めて全て管理
            _bossRenderers  = go.GetComponentsInChildren<Renderer>(true);
            _bossUprightRot = go.transform.rotation;
            _bossSpawnPos   = go.transform.position;

            if (_bossHealth?.Model != null) _bossHealth.Model.Died += OnBossKilled;

            // 開始時は出現前なので隠す(初回出現まで Dormant)
            HideBoss();
            _resolved = true;
        }

        private void Update()
        {
            if (!_resolved) { ResolveBoss(); if (!_resolved) return; }
            if (_boss == null) return;

            float now = Time.timeSinceLevelLoad;
            State             = _timer.GetState(now);
            SecondsUntilSpawn = _timer.SecondsUntilSpawn(now);

            bool shouldBeActive = State == ObjectiveState.Active;
            if (shouldBeActive && !_bossActive) SpawnBoss();
            else if (!shouldBeActive && _bossActive) HideBoss();
        }

        private void SpawnBoss()
        {
            _bossActive = true;

            var t = _boss.transform;
            t.SetPositionAndRotation(_bossSpawnPos, _bossUprightRot); // 撃破時の転倒演出をリセット

            if (_bossHealth?.Model != null && _bossHealth.Model.IsDead)
                _bossHealth.Model.Revive();

            SetColliders(true);
            SetRenderers(true);
            _boss.enabled = true;
        }

        private void HideBoss()
        {
            _bossActive = false;
            _boss.enabled = false;
            SetColliders(false);
            SetRenderers(false);
        }

        private void SetRenderers(bool on)
        {
            if (_bossRenderers == null) return;
            for (int i = 0; i < _bossRenderers.Length; i++)
                if (_bossRenderers[i] != null) _bossRenderers[i].enabled = on;
        }

        // 子Colliderも含めて全ての当たり判定を切り替える(非表示中に隠れたボスへAoEが当たるのを防ぐ)
        private void SetColliders(bool on)
        {
            if (_bossColliders == null) return;
            for (int i = 0; i < _bossColliders.Length; i++)
                if (_bossColliders[i] != null) _bossColliders[i].enabled = on;
        }

        // ボス撃破時(HealthModel.Died)。再出現をスケジュールし、撃破チームへ報酬バフを付与する。
        // 転倒演出は NeutralBossController.OnDied が担当。
        private void OnBossKilled()
        {
            if (!_bossActive) return;
            float now = Time.timeSinceLevelLoad;
            _timer.NotifyKilled(now);
            // 次の Update で State が Dormant になり HideBoss が走る

            GrantKillReward(now);
        }

        // 撃破チームへ撃破回数で段階化した報酬バフを付与する。段階の中身は CoreBuffSchedule(純関数)が
        // 決め、ここは返り値を適用するだけにする(1回目: Damage / 2回目: + MoveSpeed /
        // 3回目以降: Damage 強化 + MoveSpeed + 全味方 Shield + StructureDamage)。
        private void GrantKillReward(float now)
        {
            var buffs = GameServices.ObjectiveBuffs;
            if (buffs == null) return;

            var killerTag = _bossHealth?.LastAttacker?.GetComponentInParent<TeamTag>();
            if (killerTag == null) return;
            TeamId team = killerTag.Team;
            if (team == TeamId.Neutral) return;

            _killCountByTeam.TryGetValue(team, out int n);
            n += 1;
            _killCountByTeam[team] = n;

            // 制圧の検知用に直近チームと累計回数を公開する
            LastCaptureTeam = team;
            CaptureCount++;

            foreach (var grant in CoreBuffSchedule.ForKillCount(n))
            {
                buffs.Grant(team, grant.Type, grant.Magnitude, grant.Duration, now);

                // Shield は ObjectiveBuffModel への記録(HUD表示用)だけでは実効果が無いため、
                // 実シールドを別途チーム全体へ即時付与する
                if (grant.Type == ObjectiveBuffType.Shield)
                    GrantTeamShield(team, grant.Magnitude, grant.Duration);
            }
        }

        // チームの生存全 HealthComponent へシールドを一括付与する（付与時1回のみ適用される種別）。
        private void GrantTeamShield(TeamId team, float amount, float dur)
        {
            var healths = FindObjectsByType<HealthComponent>(FindObjectsSortMode.None);
            for (int i = 0; i < healths.Length; i++)
            {
                var hc = healths[i];
                if (hc == null || hc.Model == null || hc.Model.IsDead) continue;
                var tag = hc.GetComponentInParent<TeamTag>();
                if (tag == null || tag.Team != team) continue;
                hc.Model.AddShield(amount, dur);
            }
        }
    }
}
