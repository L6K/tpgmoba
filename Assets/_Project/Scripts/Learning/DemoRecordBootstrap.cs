using System;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.MLAgents.Demonstrations;
using Unity.MLAgents.Policies;

namespace Enigma.Learning
{
    /// <summary>
    /// RL 再開(BC ウォームスタート)用の Demonstration 録画トリガー。
    /// Temp/demo_record_request.json が存在するときのみ Arena シーンロード時に、
    /// DemonstrationRecorder を持つ Agent の BehaviorType を HeuristicOnly に切り替えて
    /// 手書きAI(MicroDuelAgent.Heuristic → CombatMicroModel.Decide)の操作を .demo に記録させる。
    /// トリガーファイルが無ければ何もしないため、通常の学習(BehaviorType Default)には無影響。
    /// </summary>
    public static class DemoRecordBootstrap
    {
        private const string SceneName = "Arena";
        private const string RequestPath = "Temp/demo_record_request.json";

        [Serializable]
        private class DemoRecordRequest
        {
            public float timeScale = 1f;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoApply()
        {
            if (SceneManager.GetActiveScene().name != SceneName) return;
            if (!File.Exists(RequestPath)) return;

            var recorder = UnityEngine.Object.FindFirstObjectByType<DemonstrationRecorder>();
            if (recorder == null)
            {
                Debug.LogWarning("[DemoRecordBootstrap] demo_record_request.json found but no DemonstrationRecorder in scene. Skipping.");
                return;
            }

            var request = JsonUtility.FromJson<DemoRecordRequest>(File.ReadAllText(RequestPath));
            if (request == null) request = new DemoRecordRequest();
            if (request.timeScale <= 0f) request.timeScale = 1f;

            var behaviorParams = recorder.GetComponent<BehaviorParameters>();
            if (behaviorParams != null) behaviorParams.BehaviorType = BehaviorType.HeuristicOnly;

            recorder.Record = true;
            Time.timeScale = request.timeScale;

            Debug.Log($"[DemoRecordBootstrap] recording enabled: name={recorder.DemonstrationName}, dir={recorder.DemonstrationDirectory}, timeScale={request.timeScale}");
        }
    }
}
