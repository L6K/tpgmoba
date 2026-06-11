using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using Enigma.Combat;
using Enigma.Data;

namespace Enigma.Core
{
    public sealed class MatchFlowController : MonoBehaviour
    {
        [SerializeField] private HealthComponent _blueTitan;
        [SerializeField] private HealthComponent _redTitan;

        // 二重発火防止: 最初の1回のみ結果を確定する
        private bool _resolved;

        private void Start()
        {
            if (!GameServices.IsInitialized) GameServices.Initialize();

            _blueTitan.Model.Died += OnBlueTitanDied;
            _redTitan.Model.Died  += OnRedTitanDied;
        }

        private void OnDestroy()
        {
            // Model は MonoBehaviour の外に存在するため、シーン破棄時に明示的に解除
            if (_blueTitan != null) _blueTitan.Model.Died -= OnBlueTitanDied;
            if (_redTitan  != null) _redTitan.Model.Died  -= OnRedTitanDied;
        }

        private void OnRedTitanDied()  => ResolveMatch(MatchResult.Victory);
        private void OnBlueTitanDied() => ResolveMatch(MatchResult.Defeat);

        private void ResolveMatch(MatchResult result)
        {
            if (_resolved) return;
            _resolved = true;

            GameServices.Match.Result               = result;
            GameServices.Match.MatchDurationSeconds = Time.timeSinceLevelLoad;

            StartCoroutine(LoadResultAfterDelay());
        }

        private IEnumerator LoadResultAfterDelay()
        {
            yield return new WaitForSeconds(2f);
            SceneManager.LoadScene("Result");
        }
    }
}
