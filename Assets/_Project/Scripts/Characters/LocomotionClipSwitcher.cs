using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace Enigma.Character
{
    /// <summary>
    /// runtimeAnimatorController を持たない FBX 由来 Animator 上で、移動速度に応じて
    /// Idle / Move クリップを Playables API で切り替え再生する軽量コンポーネント。
    ///
    /// AutoPlayClip（ミニオン用・単一クリップループ）の代わりに、プレイヤー champ モデルで
    /// 使用する。CharacterController.velocity の水平成分の大きさを閾値で判定し、
    /// 状態が変わった瞬間にのみ対象クリップへクロスフェードする（毎フレームの再生し直しは避ける）。
    /// 非ループクリップでも末尾到達で先頭へ巻き戻してループ外観を維持する。
    ///
    /// 原神/鳴潮風のなめらかさのため、状態切替は即時差し替えではなく
    /// AnimationMixerPlayable(2 入力) の重みを 0.18 秒で線形クロスフェードする。
    /// クロスフェード完了時に古い playable を破棄してノードリークを防ぐ。
    ///
    /// Move は Run 系があれば Run、無ければ Walk を使う（_run が null なら _walk へフォールバック）。
    /// アイドル継続中は _idleVariants を数ループごとに挟んで棒立ちを避ける。
    ///
    /// 攻撃は AnimationLayerMixerPlayable の上位レイヤー(layer1)でワンショット再生する。
    /// _attackMask を結線すると上半身のみへ適用され、下半身は移動レイヤー(layer0)を継続するため
    /// AA 中も足がガタつかない。_attackMask が null の場合は layer1 を全身に効かせる。
    /// _attackClips が複数あれば呼ばれるたびに順繰り（AA コンボ）、無ければ _attack 単発。
    ///
    /// _walk/_run/_idleVariants が全て null でも Idle 固定で動作する。
    /// </summary>
    public sealed class LocomotionClipSwitcher : MonoBehaviour
    {
        [SerializeField] private AnimationClip _idle;
        [SerializeField] private AnimationClip _walk;
        // Run 系（あれば Move で優先使用）。null なら _walk へフォールバック。
        [SerializeField] private AnimationClip _run;
        // 棒立ち回避用のアイドルバリアント（null/空可）。
        [SerializeField] private AnimationClip[] _idleVariants;
        [SerializeField] private AnimationClip _attack;
        // AA コンボ用の複数攻撃クリップ（順繰り）。空なら _attack 単発へフォールバック。
        [SerializeField] private AnimationClip[] _attackClips;
        [SerializeField] private AvatarMask _attackMask;
        [SerializeField] private CharacterController _controller;
        [SerializeField] private float _walkSpeedThreshold = 0.5f;

        // 状態切替クロスフェード長（秒）。原神/鳴潮風のなめらかさのターゲット値。
        private const float CrossfadeDuration = 0.18f;

        private Animator _animator;
        private PlayableGraph _graph;
        private AnimationLayerMixerPlayable _layerMixer;

        // layer0: 移動（Idle/Move 切替・クロスフェード）。layer1: 攻撃ワンショット。
        // layer0 の中身は 2 入力ミキサ（input0=現在クリップ, input1=前クリップ）で、
        // 切替時に重みを 0→1 へ補間し、完了したら前クリップを破棄する。
        private AnimationMixerPlayable _locoMixer;
        private AnimationClipPlayable _currentPlayable;
        private AnimationClipPlayable _previousPlayable;
        private AnimationClipPlayable _attackPlayable;

        private AnimationClip _current;
        private double _clipStartTime;
        private bool _walking;

        // クロスフェード進行管理。重みは Update で線形補間する。
        private bool _crossfading;
        private float _crossfadeElapsed;

        // アイドルバリアント挿入の順繰りロジック（plain C# に切り出してテスト）。
        private IdleVariantSequencer _idleSequencer;
        // 現在 layer0 で再生中なのがバリアント（true）かベース（Idle/Move, false）か。
        private bool _playingVariant;

        // 攻撃ワンショット再生中フラグ。再生終了の時間監視に使う。
        private bool _attacking;
        private double _attackEndTime;
        // AA コンボの順繰り位置。
        private int _attackComboIndex;

        // 固定シードで System.Random を使う（UnityEngine.Random 禁止規約）。
        // バリアント選択の擬似ランダム化に使用。
        private const int IdleSeed = 9173;

        /// <summary>
        /// スワッパーからの結線用（マスク無し・既存互換オーバーロード）。
        /// </summary>
        public void Configure(AnimationClip idle, AnimationClip walk, AnimationClip attack, CharacterController controller)
        {
            Configure(idle, walk, attack, controller, null);
        }

        /// <summary>
        /// 既存 5 引数互換オーバーロード（run/idleVariants/attackClips 無し）。
        /// </summary>
        public void Configure(AnimationClip idle, AnimationClip walk, AnimationClip attack, CharacterController controller, AvatarMask attackMask)
        {
            Configure(idle, walk, run: null, idleVariants: null, attack: attack,
                attackClips: null, controller: controller, attackMask: attackMask);
        }

        /// <summary>
        /// 拡張結線用。Start 前後どちらで呼ばれても整合するよう、再生中であれば即座に現在状態を反映する。
        /// run が null なら Move で walk を使う。idleVariants が空ならバリアント挿入なし。
        /// attackClips が空なら攻撃は attack 単発。_attackMask を渡すと攻撃が上半身のみへ適用される。
        /// </summary>
        public void Configure(
            AnimationClip idle,
            AnimationClip walk,
            AnimationClip run,
            AnimationClip[] idleVariants,
            AnimationClip attack,
            AnimationClip[] attackClips,
            CharacterController controller,
            AvatarMask attackMask)
        {
            _idle         = idle;
            _walk         = walk;
            _run          = run;
            _idleVariants = idleVariants;
            _attack       = attack;
            _attackClips  = attackClips;
            _controller   = controller;
            _attackMask   = attackMask;

            BuildIdleSequencer();

            if (_graph.IsValid())
            {
                ApplyLayerMask();
                PlayState(_walking, force: true);
            }
        }

        // Move 状態で実際に使うクリップ: Run があれば Run、無ければ Walk。
        private AnimationClip MoveClip => _run != null ? _run : _walk;

        private void BuildIdleSequencer()
        {
            int count = _idleVariants != null ? _idleVariants.Length : 0;
            _idleSequencer = new IdleVariantSequencer(count, seed: IdleSeed);
        }

        /// <summary>
        /// 攻撃クリップを durationSeconds で1周再生する。攻撃クリップが無ければ false を返す。
        /// _attackClips が複数あれば呼ばれるたびに次のクリップへ順繰り（AA コンボ）、
        /// 1 本以下なら _attack 単発へフォールバックする。
        /// クリップ全体が durationSeconds で再生し終わるよう speed = clip.length / duration でスケールし、
        /// 再生終了（時間監視）で攻撃レイヤーの重みを 0 に戻して移動レイヤーのみへ復帰する。
        /// 攻撃中の再入は古い攻撃 playable を破棄して張り直す。
        /// </summary>
        public bool PlayAttack(float durationSeconds)
        {
            if (!_layerMixer.IsValid() || durationSeconds <= 0.0001f)
                return false;

            var clip = NextAttackClip();
            if (clip == null) return false;

            // 攻撃中の再入: 古い攻撃 playable を破棄（Disconnect は Destroy が面倒を見る）
            if (_attackPlayable.IsValid())
            {
                _layerMixer.DisconnectInput(1);
                _attackPlayable.Destroy();
            }

            _attackPlayable = AnimationClipPlayable.Create(_graph, clip);
            _layerMixer.ConnectInput(1, _attackPlayable, 0);
            _layerMixer.SetInputWeight(1, 1f);

            // クリップ全体が duration で再生し終わるよう速度スケール
            float speed = clip.length > 0.0001f ? clip.length / durationSeconds : 1f;
            _attackPlayable.SetSpeed(speed);

            _attacking     = true;
            _attackEndTime = Time.timeAsDouble + durationSeconds;
            return true;
        }

        // 次に再生する攻撃クリップ。_attackClips が複数あれば順繰り、無ければ _attack。
        private AnimationClip NextAttackClip()
        {
            if (_attackClips != null && _attackClips.Length > 0)
            {
                // null 要素を飛ばしつつ順繰り（全 null なら _attack へフォールバック）
                int n = _attackClips.Length;
                for (int i = 0; i < n; i++)
                {
                    int idx = _attackComboIndex % n;
                    _attackComboIndex = (_attackComboIndex + 1) % n;
                    if (_attackClips[idx] != null)
                        return _attackClips[idx];
                }
            }
            return _attack;
        }

        private void Start()
        {
            _animator = GetComponentInChildren<Animator>();
            if (_animator == null || _idle == null) return;

            if (_idleSequencer == null) BuildIdleSequencer();

            // runtimeAnimatorController があると Playables 出力と競合するため切り離す
            _graph = PlayableGraph.Create($"{name}_Locomotion");
            _graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
            _animator.runtimeAnimatorController = null;

            var output = AnimationPlayableOutput.Create(_graph, "Animation", _animator);

            // 2レイヤーミキサ: layer0=移動（常時 weight 1）, layer1=攻撃（攻撃時のみ weight 1）
            _layerMixer = AnimationLayerMixerPlayable.Create(_graph, 2);
            output.SetSourcePlayable(_layerMixer);

            // layer0 の中身: 2 入力クロスフェードミキサ（input0=現在, input1=前）
            _locoMixer = AnimationMixerPlayable.Create(_graph, 2);
            _layerMixer.ConnectInput(0, _locoMixer, 0);
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

        // layer0（移動）のクリップを target へクロスフェードで切り替える。攻撃レイヤーには触れない。
        // force=true でも、即時差し替えではなく現在クリップからのクロスフェードを開始する
        // （初回 _current==null のときだけ即時フェードイン）。
        private void PlayState(bool walking, bool force)
        {
            var target = (walking && MoveClip != null) ? MoveClip : _idle;
            if (target == null) return;

            // ベース（Idle/Move）への切替なのでバリアント再生フラグは下ろす
            _playingVariant = false;
            CrossfadeTo(target, force);
            _walking = walking;
        }

        // target クリップへクロスフェード開始。同一クリップへの非強制切替は無視する。
        private void CrossfadeTo(AnimationClip target, bool force)
        {
            if (!_locoMixer.IsValid() || target == null) return;
            if (!force && target == _current && !_crossfading) return;

            // 進行中のクロスフェードがあれば即確定して前 playable を破棄（多重フェード回避）
            FinalizeCrossfade();

            // 初回（現在クリップ無し）は input0 へ即フェードイン
            if (!_currentPlayable.IsValid())
            {
                _currentPlayable = AnimationClipPlayable.Create(_graph, target);
                _locoMixer.ConnectInput(0, _currentPlayable, 0);
                _locoMixer.SetInputWeight(0, 1f);
                _locoMixer.SetInputWeight(1, 0f);

                _current       = target;
                _clipStartTime = Time.timeAsDouble;
                return;
            }

            // 現在クリップを input1（前クリップ）へ退避し、新クリップを input0 へ。
            // 重みは input0=0 から開始し、Update で 1 へ線形補間する。
            _previousPlayable = _currentPlayable;
            _locoMixer.DisconnectInput(1);
            _locoMixer.DisconnectInput(0);
            _locoMixer.ConnectInput(1, _previousPlayable, 0);

            _currentPlayable = AnimationClipPlayable.Create(_graph, target);
            _locoMixer.ConnectInput(0, _currentPlayable, 0);

            _locoMixer.SetInputWeight(0, 0f);
            _locoMixer.SetInputWeight(1, 1f);

            _current          = target;
            _clipStartTime    = Time.timeAsDouble;
            _crossfading      = true;
            _crossfadeElapsed = 0f;
        }

        // 進行中クロスフェードを weight=1 で確定し、前クリップ playable を破棄する。
        private void FinalizeCrossfade()
        {
            if (!_crossfading) return;
            _crossfading = false;

            if (_locoMixer.IsValid())
            {
                _locoMixer.SetInputWeight(0, 1f);
                _locoMixer.SetInputWeight(1, 0f);
                if (_previousPlayable.IsValid())
                    _locoMixer.DisconnectInput(1);
            }
            if (_previousPlayable.IsValid())
                _previousPlayable.Destroy();
        }

        private void Update()
        {
            if (!_layerMixer.IsValid() || _current == null) return;

            // クロスフェード進行: 0.18 秒で input0 を 0→1（input1 は 1→0）へ線形補間
            if (_crossfading)
            {
                _crossfadeElapsed += Time.deltaTime;
                float t = Mathf.Clamp01(_crossfadeElapsed / CrossfadeDuration);
                if (_locoMixer.IsValid())
                {
                    _locoMixer.SetInputWeight(0, t);
                    _locoMixer.SetInputWeight(1, 1f - t);
                }
                if (t >= 1f) FinalizeCrossfade();
            }

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

            // 現在クリップの末尾到達処理（クロスフェード中の前クリップ巻き戻しは不要）
            if (_current.length > 0.0001f &&
                Time.timeAsDouble - _clipStartTime >= _current.length)
            {
                OnCurrentClipLooped();
            }
        }

        // 現在 layer0 クリップが 1 周した。
        // 停止中(Idle)はアイドルバリアント挿入ロジックを回し、それ以外（Move/バリアント）は巻き戻す。
        private void OnCurrentClipLooped()
        {
            // バリアント再生が終わったらベース Idle へ戻す（クロスフェード経由）
            if (_playingVariant)
            {
                _idleSequencer?.NotifyVariantCompleted();
                _playingVariant = false;
                CrossfadeTo(_idle, force: true);
                return;
            }

            // ベース Idle 継続中: バリアント挿入タイミングなら 1 つ挟む
            if (!_walking && _idle != null && _current == _idle &&
                _idleSequencer != null && _idleSequencer.HasVariants)
            {
                int idx = _idleSequencer.NotifyBaseLoopCompleted();
                if (idx >= 0 && _idleVariants != null && idx < _idleVariants.Length &&
                    _idleVariants[idx] != null)
                {
                    _playingVariant = true;
                    CrossfadeTo(_idleVariants[idx], force: true);
                    return;
                }
            }

            // それ以外（Move ループ、または非ループ素材の巻き戻し）: 現在クリップを先頭へ
            if (_currentPlayable.IsValid())
                _currentPlayable.SetTime(0.0);
            _clipStartTime = Time.timeAsDouble;
        }

        private void OnDestroy()
        {
            if (_graph.IsValid())
                _graph.Destroy();
        }
    }
}
