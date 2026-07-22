using UnityEngine;
using UnityEngine.SceneManagement;

namespace Enigma.Vfx
{
    /// <summary>
    /// 命中の手応えを出す「ヒットストップ」(極短時間 Time.timeScale を落として一瞬止める)。
    /// シーン常駐シングルトン。<see cref="Request"/> で停止秒数を要求する(最大値で上書き・上限あり)。
    /// 復帰は unscaled 時間で計測するため停止中でも確実に解除される。
    /// 演出ロジックの長さは <see cref="HitStopModel"/>(純ロジック・テスト済)で算出する。
    /// </summary>
    public sealed class HitStopController : MonoBehaviour
    {
        // 体感が重くなりすぎないよう停止は浅め・短めに上限を設ける。
        private const float FreezeScale = 0.05f;
        private const float MaxFreezeSeconds = 0.12f;

        public static HitStopController Instance { get; private set; }

        private float _remaining;
        private bool  _frozen;

        // AfterSceneLoad はプロセス開始後1回しか走らず、本体は DontDestroyOnLoad でもないため、
        // BalanceSimRunner 等が2試合目のために AetherRift_Map を再ロードすると破棄されたまま
        // 再生成されない(CentralObjectiveDirector と同じ既知の穴)。sceneLoaded 購読で毎回補充する。
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
            if (Instance != null) return;
            new GameObject("HitStopController").AddComponent<HitStopController>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (_frozen) { Time.timeScale = 1f; _frozen = false; }
        }

        /// <summary>停止秒数を要求する(現在値との最大、上限 MaxFreezeSeconds でクランプ)。</summary>
        public void Request(float seconds)
        {
            if (seconds <= 0f) return;
            if (seconds > _remaining) _remaining = Mathf.Min(seconds, MaxFreezeSeconds);
        }

        private void Update()
        {
            if (_remaining > 0f)
            {
                if (!_frozen) { Time.timeScale = FreezeScale; _frozen = true; }
                // 停止中でも進む unscaled 時間で計測して確実に解除する
                _remaining -= Time.unscaledDeltaTime;
                if (_remaining <= 0f)
                {
                    _remaining = 0f;
                    Time.timeScale = 1f;
                    _frozen = false;
                }
            }
        }
    }
}
