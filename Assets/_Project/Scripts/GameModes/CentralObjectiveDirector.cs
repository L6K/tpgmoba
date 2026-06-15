using UnityEngine;
using UnityEngine.SceneManagement;
using Enigma.Combat;
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

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoSpawn()
        {
            if (SceneManager.GetActiveScene().name != "AetherRift_Map") return;
            if (FindObjectOfType<CentralObjectiveDirector>() != null) return;
            new GameObject("CentralObjectiveDirector").AddComponent<CentralObjectiveDirector>();
        }

        private ObjectiveSpawnTimerModel _timer;

        private NeutralBossController _boss;
        private HealthComponent       _bossHealth;
        private Collider              _bossCol;
        private Renderer[]            _bossRenderers;
        private Quaternion            _bossUprightRot;
        private Vector3               _bossSpawnPos;
        private bool                  _bossActive;
        private bool                  _resolved;

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
            _bossCol        = go.GetComponent<Collider>();
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

            if (_bossCol != null) _bossCol.enabled = true;
            SetRenderers(true);
            _boss.enabled = true;
        }

        private void HideBoss()
        {
            _bossActive = false;
            _boss.enabled = false;
            if (_bossCol != null) _bossCol.enabled = false;
            SetRenderers(false);
        }

        private void SetRenderers(bool on)
        {
            if (_bossRenderers == null) return;
            for (int i = 0; i < _bossRenderers.Length; i++)
                if (_bossRenderers[i] != null) _bossRenderers[i].enabled = on;
        }

        // ボス撃破時(HealthModel.Died)。再出現をスケジュールする。
        // バフ付与・転倒演出は NeutralBossController.OnDied が担当済み。
        private void OnBossKilled()
        {
            if (!_bossActive) return;
            _timer.NotifyKilled(Time.timeSinceLevelLoad);
            // 次の Update で State が Dormant になり HideBoss が走る
        }
    }
}
