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
    /// 攻撃は AnimationLayerMixerPlayable の上位レイヤー(layer1)でワンショット再生する。
    /// _attackMask を結線すると上半身のみへ適用され、下半身は移動レイヤー(layer0)の
    /// Idle/Walk を継続するため AA 中も足がガタつかない。_attackMask が null の場合は
    /// layer1 を全身に効かせる（Generic リグの攻撃クリップは元から全身想定）。
    ///
    /// _walk が null の場合は Idle 固定で動作する。
    /// </summary>
    public sealed class LocomotionClipSwitcher : MonoBehaviour
    {
        [SerializeField] private AnimationClip _idle;
        [SerializeField] private AnimationClip _walk;
        [SerializeField] private AnimationClip _attack;
        [SerializeField] private AvatarMask _attackMask;
        [SerializeField] private CharacterController _controller;
        [SerializeField] private float _walkSpeedThreshold = 0.5f;

        private Animator _animator;
        private PlayableGraph _graph;
        private AnimationLayerMixerPlayable _layerMixer;

        // layer0: 移動（Idle/Walk 切替）。layer1: 攻撃ワンショット。
        private AnimationClipPlayable _locoPlayable;
        private AnimationClipPlayable _attackPlayable;

        private AnimationClip _current;
        private double _clipStartTime;
        private bool _walking;

        // 攻撃ワンショット再生中フラグ。再生終了の時間監視に使う。
        private bool _attacking;
        private double _attackEndTime;

        /// <summary>
        /// スワッパーからの結線用（マスク無し・既存互換オーバーロード）。
        /// </summary>
        public void Configure(AnimationClip idle, AnimationClip walk, AnimationClip attack, CharacterController controller)
        {
            Configure(idle, walk, attack, controller, null);
        }

        /// <summary>
        /// スワッパーからの結線用。Start 前後どちらで呼ばれても整合するよう、
        /// 再生中であれば即座に現在状態のクリップを反映する。
        /// _attackMask に AvatarMask を渡すと攻撃レイヤーが上半身のみへ適用される。
        /// </summary>
        public void Configure(AnimationClip idle, AnimationClip walk, AnimationClip attack, CharacterController controller, AvatarMask attackMask)
        {
            _idle        = idle;
            _walk        = walk;
            _attack      = attack;
            _controller  = controller;
            _attackMask  = attackMask;
            if (_graph.IsValid())
            {
                ApplyLayerMask();
                PlayState(_walking, force: true);
            }
        }

        /// <summary>
        /// 攻撃クリップを durationSeconds で1周再生する。AttackClip が無ければ何もせず false を返す。
        /// クリップ全体が durationSeconds で再生し終わるよう speed = clip.length / duration でスケールし、
        /// 再生終了（時間監視）で攻撃レイヤーの重みを 0 に戻して移動レイヤーのみへ復帰する。
        /// 攻撃中の再入は古い攻撃 playable を破棄して張り直す。
        /// </summary>
        public bool PlayAttack(float durationSeconds)
        {
            if (!_layerMixer.IsValid() || _attack == null || durationSeconds <= 0.0001f)
                return false;

            // 攻撃中の再入: 古い攻撃 playable を破棄（Disconnect は Destroy が面倒を見る）
            if (_attackPlayable.IsValid())
            {
                _layerMixer.DisconnectInput(1);
                _attackPlayable.Destroy();
            }

            _attackPlayable = AnimationClipPlayable.Create(_graph, _attack);
            _layerMixer.ConnectInput(1, _attackPlayable, 0);
            _layerMixer.SetInputWeight(1, 1f);

            // クリップ全体が duration で再生し終わるよう速度スケール
            float speed = _attack.length > 0.0001f ? _attack.length / durationSeconds : 1f;
            _attackPlayable.SetSpeed(speed);

            _attacking     = true;
            _attackEndTime = Time.timeAsDouble + durationSeconds;
            return true;
        }

        private void Start()
        {
            _animator = GetComponentInChildren<Animator>();
            if (_animator == null || _idle == null) return;

            // runtimeAnimatorController があると Playables 出力と競合するため切り離す
            _graph = PlayableGraph.Create($"{name}_Locomotion");
            _graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
            _animator.runtimeAnimatorController = null;

            var output = AnimationPlayableOutput.Create(_graph, "Animation", _animator);

            // 2レイヤーミキサ: layer0=移動（常時 weight 1）, layer1=攻撃（攻撃時のみ weight 1）
            _layerMixer = AnimationLayerMixerPlayable.Create(_graph, 2);
            output.SetSourcePlayable(_layerMixer);
            _layerMixer.SetInputWeight(0, 1f);
            _layerMixer.SetInputWeight(1, 0f);
            ApplyLayerMask();

            PlayState(false, force: true);
            _graph.Play();
        }

        // _attackMask が結線済みなら layer1 を上半身マスクで限定する。
        // null の場合は layer1 を全身適用（マスク未設定の既定挙動）。
        private void ApplyLayerMask()
        {
            if (!_layerMixer.IsValid()) return;
            if (_attackMask != null)
                _layerMixer.SetLayerMaskFromAvatarMask(1, _attackMask);
        }

        // layer0（移動）のクリップを切り替える。攻撃レイヤーには触れない。
        private void PlayState(bool walking, bool force)
        {
            if (!_layerMixer.IsValid()) return;

            var target = (walking && _walk != null) ? _walk : _idle;
            if (target == null) return;
            if (!force && target == _current) return;

            if (_locoPlayable.IsValid())
            {
                _layerMixer.DisconnectInput(0);
                _locoPlayable.Destroy();
            }

            _locoPlayable = AnimationClipPlayable.Create(_graph, target);
            _layerMixer.ConnectInput(0, _locoPlayable, 0);
            _layerMixer.SetInputWeight(0, 1f);

            _current       = target;
            _walking       = walking;
            _clipStartTime = Time.timeAsDouble;
        }

        private void Update()
        {
            if (!_layerMixer.IsValid() || _current == null) return;

            // 攻撃ワンショット中: 再生終了で攻撃レイヤーを破棄して移動レイヤーのみへ復帰
            if (_attacking && Time.timeAsDouble >= _attackEndTime)
            {
                _attacking = false;
                _layerMixer.SetInputWeight(1, 0f);
                if (_attackPlayable.IsValid())
                {
                    _layerMixer.DisconnectInput(1);
                    _attackPlayable.Destroy();
                }
            }

            // 速度判定（CharacterController 未結線時は Idle 固定）。
            // レイヤー化により足は常に移動レイヤーが担うため、攻撃中も切替を抑制しない。
            if (_controller != null)
            {
                var v = _controller.velocity;
                v.y = 0f;
                bool shouldWalk = v.magnitude > _walkSpeedThreshold;
                if (shouldWalk != _walking)
                    PlayState(shouldWalk, force: false);
            }

            // 非ループ移動クリップが末尾に達したら巻き戻してループ外観を維持
            if (_current.length > 0.0001f &&
                Time.timeAsDouble - _clipStartTime >= _current.length)
            {
                if (_locoPlayable.IsValid())
                    _locoPlayable.SetTime(0.0);
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
