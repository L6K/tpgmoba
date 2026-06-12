using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace Enigma.Character
{
    /// <summary>
    /// runtimeAnimatorController を持たない FBX 由来 Animator 上で、移動速度に応じて
    /// Idle / Walk クリップを Playables API で切り替え再生する軽量コンポーネント。
    ///
    /// AutoPlayClip（ミニオン用・単一クリップループ）の代わりに、プレイヤー champ モデルで
    /// 使用する。CharacterController.velocity の水平成分の大きさを閾値で判定し、
    /// 状態が変わった瞬間にのみ対象クリップへ再生し直す（毎フレームの再生し直しは避ける）。
    /// 非ループクリップでも末尾到達で先頭へ巻き戻してループ外観を維持する。
    ///
    /// _walk が null の場合は Idle 固定で動作する。
    /// </summary>
    public sealed class LocomotionClipSwitcher : MonoBehaviour
    {
        [SerializeField] private AnimationClip _idle;
        [SerializeField] private AnimationClip _walk;
        [SerializeField] private CharacterController _controller;
        [SerializeField] private float _walkSpeedThreshold = 0.5f;

        private Animator _animator;
        private PlayableGraph _graph;
        private AnimationClipPlayable _playable;
        private AnimationClip _current;
        private double _clipStartTime;
        private bool _walking;

        /// <summary>
        /// スワッパーからの結線用。Start 前後どちらで呼ばれても整合するよう、
        /// 再生中であれば即座に現在状態のクリップを反映する。
        /// </summary>
        public void Configure(AnimationClip idle, AnimationClip walk, CharacterController controller)
        {
            _idle       = idle;
            _walk       = walk;
            _controller = controller;
            if (_graph.IsValid())
                PlayState(_walking, force: true);
        }

        private void Start()
        {
            _animator = GetComponentInChildren<Animator>();
            if (_animator == null || _idle == null) return;

            // runtimeAnimatorController があると Playables 出力と競合するため切り離す
            _animator.runtimeAnimatorController = null;

            var output = AnimationPlayableOutput.Create(
                _graph = PlayableGraph.Create($"{name}_Locomotion"), "Animation", _animator);
            _ = output;
            _graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
            PlayState(false, force: true);
            _graph.Play();
        }

        private void PlayState(bool walking, bool force)
        {
            if (!_graph.IsValid()) return;

            var target = (walking && _walk != null) ? _walk : _idle;
            if (target == null) return;
            if (!force && target == _current) return;

            if (_playable.IsValid())
                _playable.Destroy();

            _playable = AnimationClipPlayable.Create(_graph, target);
            var output = _graph.GetOutput(0);
            ((AnimationPlayableOutput)output).SetSourcePlayable(_playable);

            _current       = target;
            _walking       = walking;
            _clipStartTime = Time.timeAsDouble;
        }

        private void Update()
        {
            if (!_graph.IsValid() || _current == null) return;

            // 速度判定（CharacterController 未結線時は Idle 固定）
            if (_controller != null)
            {
                var v = _controller.velocity;
                v.y = 0f;
                bool shouldWalk = v.magnitude > _walkSpeedThreshold;
                if (shouldWalk != _walking)
                    PlayState(shouldWalk, force: false);
            }

            // 非ループクリップが末尾に達したら巻き戻してループ外観を維持
            if (_current.length > 0.0001f &&
                Time.timeAsDouble - _clipStartTime >= _current.length)
            {
                _playable.SetTime(0.0);
                _clipStartTime = Time.timeAsDouble;
            }
        }

        private void OnDestroy()
        {
            if (_graph.IsValid())
                _graph.Destroy();
        }
    }
}
