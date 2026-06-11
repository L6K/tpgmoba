using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.SceneManagement;
using Enigma.Core;
using Enigma.Data;

namespace Enigma.UI
{
    public sealed class ResultScreenController : MonoBehaviour
    {
        [SerializeField] private UIDocument _uiDocument;

        private void OnEnable()
        {
            if (!GameServices.IsInitialized) GameServices.Initialize();

            var root = _uiDocument.rootVisualElement;

            var titleLabel    = root.Q<Label>("result-title");
            var subLabel      = root.Q<Label>("result-sub");
            var charLabel     = root.Q<Label>("result-char");
            var durationLabel = root.Q<Label>("result-duration");
            var kdLabel       = root.Q<Label>("result-kd");
            var homeBtn       = root.Q<Button>("result-home");
            var replayBtn     = root.Q<Button>("result-replay");

            var match  = GameServices.Match;
            // None は勝利扱い（テスト・直接遷移時のフォールバック）
            bool isVictory = match.Result != MatchResult.Defeat;

            if (isVictory)
            {
                titleLabel.text = "VICTORY";
                subLabel.text   = "タイタン撃破!";
                titleLabel.AddToClassList("result-title--victory");
            }
            else
            {
                titleLabel.text = "DEFEAT";
                subLabel.text   = "タイタンを破壊された…";
                titleLabel.AddToClassList("result-title--defeat");
            }

            charLabel.text = match.PickedCharacter?.DisplayName ?? "ゼフ";

            int totalSec = Mathf.FloorToInt(match.MatchDurationSeconds);
            int m = totalSec / 60;
            int s = totalSec % 60;
            durationLabel.text = $"{m}:{s:D2}";

            if (kdLabel != null)
                kdLabel.text = $"{match.Kills} / {match.Deaths}";

            homeBtn.clicked   += OnHomeClicked;
            replayBtn.clicked += OnReplayClicked;
        }

        private void OnDisable()
        {
            var root = _uiDocument?.rootVisualElement;
            if (root == null) return;

            var homeBtn   = root.Q<Button>("result-home");
            var replayBtn = root.Q<Button>("result-replay");
            if (homeBtn   != null) homeBtn.clicked   -= OnHomeClicked;
            if (replayBtn != null) replayBtn.clicked -= OnReplayClicked;
        }

        private void OnHomeClicked()
        {
            SceneManager.LoadScene("MainMenu");
        }

        private void OnReplayClicked()
        {
            // 結果をリセットしてから遷移（次戦のために初期化）
            GameServices.Match.Result = MatchResult.None;
            SceneManager.LoadScene("MainMenu");
        }
    }
}
