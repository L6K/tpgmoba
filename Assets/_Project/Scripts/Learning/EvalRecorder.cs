using System.IO;
using UnityEngine;

namespace Enigma.Learning
{
    // アリーナの評価計測: エピソードごとの勝者と所要時間を JSONL に追記する。
    // 死亡は Died イベント購読で捕捉する(Agent が同一フレーム内で死亡→リスポーンまで
    // 完結させるため、Update での IsDead ポーリングでは死を観測できない)。
    public sealed class EvalRecorder : MonoBehaviour
    {
        [SerializeField] private ArenaFighter _blue;
        [SerializeField] private ArenaFighter _red;
        [SerializeField] private string _label = "eval";
        [SerializeField] private float _timeScale = 10f;

        private float _episodeStart;
        private int _episodes;
        private bool _subscribed;

        public int Episodes => _episodes;

        private void Start()
        {
            Time.timeScale = _timeScale;
            _episodeStart = Time.time;
            TrySubscribe();
        }

        private void Update()
        {
            // フィールド結線がシーン読込順で遅れる場合に備えた遅延購読
            if (!_subscribed) TrySubscribe();
        }

        private void TrySubscribe()
        {
            if (_blue == null || _red == null) return;
            _blue.Health.Model.Died += () => OnDeath(blueDied: true);
            _red.Health.Model.Died += () => OnDeath(blueDied: false);
            _subscribed = true;
        }

        private void OnDeath(bool blueDied)
        {
            _episodes++;
            float duration = Time.time - _episodeStart;
            string winner = blueDied ? "red" : "blue";
            float blueHp = _blue.Health.Model.CurrentHp;
            float redHp = _red.Health.Model.CurrentHp;
            Directory.CreateDirectory("Temp");
            File.AppendAllText($"Temp/eval_{_label}.jsonl",
                $"{{\"episode\":{_episodes},\"winner\":\"{winner}\",\"duration\":{duration:F1},\"blueHp\":{blueHp:F0},\"redHp\":{redHp:F0}}}\n");
            _episodeStart = Time.time;
        }

        public void Configure(ArenaFighter blue, ArenaFighter red, string label, float timeScale)
        {
            _blue = blue;
            _red = red;
            _label = label;
            _timeScale = timeScale;
        }
    }
}
