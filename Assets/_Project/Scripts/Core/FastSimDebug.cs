using UnityEngine;
using UnityEngine.InputSystem;

namespace Enigma.Core
{
    /// <summary>
    /// F9 で timeScale を 1x/5x/10x に巡回させるデバッグ用ツール。
    /// 学習データ収集を高速化する目的(将来の ML-Agents 大量試行のため)。
    /// </summary>
    public sealed class FastSimDebug : MonoBehaviour
    {
        private static readonly float[] Scales = { 1f, 5f, 10f };
        private int _index;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoSpawn()
        {
            if (FindObjectOfType<FastSimDebug>() != null) return;
            var go = new GameObject("FastSimDebug");
            go.AddComponent<FastSimDebug>();
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return;
            if (!keyboard.f9Key.wasPressedThisFrame) return;

            _index = (_index + 1) % Scales.Length;
            Time.timeScale = Scales[_index];
            Debug.Log("[FastSim] timeScale=" + Time.timeScale);
        }

        private void OnGUI()
        {
            if (Mathf.Approximately(Time.timeScale, 1f)) return;
            GUI.Label(new Rect(8, Screen.height - 24, 60, 20), "x" + Mathf.RoundToInt(Time.timeScale));
        }
    }
}
