using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace Enigma.Minion
{
    /// <summary>
    /// runtimeAnimatorController を持たない FBX 由来の Animator 上で、
    /// AnimationClip を Playables API でループ再生する軽量コンポーネント。
    ///
    /// ビルダー(BuildAetherRiftMap)が FBX サブアセットの AnimationClip を
    /// _clips に結線する。優先順位は次の通り:
    ///   1. _clips の中で名前に _clipNameContains を含む最初のクリップ
    ///   2. _clips の先頭
    /// クリップ自体が非ループ設定でも、再生位置が末尾に達したら再 Play して
    /// ループ外観を維持する（時間監視によるフォールバック）。
    /// </summary>
    public sealed class AutoPlayClip : MonoBehaviour
    {
        [SerializeField] private string _clipNameContains = "Walk";
        [SerializeField] private AnimationClip[] _clips;

        private PlayableGraph _graph;
        private AnimationClip _clip;
        private double _startTime;
        private bool _playing;

        private void Start()
        {
            var animator = GetComponentInChildren<Animator>();
            if (animator == null) return;

            _clip = SelectClip();
            if (_clip == null) return;

            // runtimeAnimatorController があると Playables の出力と競合するため切り離す
            animator.runtimeAnimatorController = null;

            var output = AnimationPlayableUtilities.PlayClip(animator, _clip, out _graph);
            _ = output;
            _startTime = Time.timeAsDouble;
            _playing = true;
        }

        private AnimationClip SelectClip()
        {
            if (_clips == null || _clips.Length == 0) return null;

            if (!string.IsNullOrEmpty(_clipNameContains))
            {
                foreach (var c in _clips)
                {
                    if (c != null &&
                        c.name.IndexOf(_clipNameContains, System.StringComparison.OrdinalIgnoreCase) >= 0)
                        return c;
                }
            }

            foreach (var c in _clips)
                if (c != null) return c;

            return null;
        }

        private void Update()
        {
            if (!_playing || _clip == null || !_graph.IsValid()) return;

            // 非ループクリップが末尾に達したら先頭から再生し直す
            if (_clip.length > 0.0001f &&
                Time.timeAsDouble - _startTime >= _clip.length)
            {
                _graph.GetRootPlayable(0).SetTime(0.0);
                _startTime = Time.timeAsDouble;
            }
        }

        private void OnDestroy()
        {
            if (_graph.IsValid())
                _graph.Destroy();
        }
    }
}
