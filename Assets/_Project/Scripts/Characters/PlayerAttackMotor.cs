using System;
using UnityEngine;

namespace Enigma.Character
{
    /// <summary>
    /// AttackMotion を Update で駆動し、簡易プロシージャルモーション演出を行う Humble MonoBehaviour。
    /// Animator には触らない（将来のアニメーションクリップ連携に備えた分離）。
    /// </summary>
    public sealed class PlayerAttackMotor : MonoBehaviour
    {
        [SerializeField] private Transform _modelRoot;
        [SerializeField] private LocomotionClipSwitcher _clipSwitcher;

        private AttackMotion _motion;
        private Vector3      _baseLocalPos;
        private bool         _baseLocalPosRecorded;

        // 実攻撃クリップ再生中はプロシージャルランジを抑制する（二重演出回避）。
        // Motion が None に戻ったら解除する。
        private bool _suppressLunge;

        public AttackMotion Motion
        {
            get
            {
                if (_motion == null) _motion = new AttackMotion();
                return _motion;
            }
        }

        /// <summary>
        /// プロシージャル攻撃モーションの対象モデルを差し替える。
        /// モデルスワップ（ChampionModelSwapper）が新モデルの Transform を渡す。
        /// ベース位置の再記録を促すためフラグをリセットする。
        /// </summary>
        public void SetModelRoot(Transform modelRoot)
        {
            _modelRoot            = modelRoot;
            _baseLocalPosRecorded = false;
        }

        /// <summary>
        /// 攻撃アニメーション再生用の LocomotionClipSwitcher を結線する。
        /// モデルスワップ（ChampionModelSwapper）が新モデルの switcher を渡す。
        /// </summary>
        public void SetClipSwitcher(LocomotionClipSwitcher clipSwitcher)
        {
            _clipSwitcher = clipSwitcher;
        }

        /// <summary>AttackMotion.TryBegin に委譲して攻撃モーションを開始する。</summary>
        public bool RequestAttack(float windup, float recovery, Action fire)
        {
            bool began = Motion.TryBegin(windup, recovery, fire);
            if (began)
            {
                // 実攻撃クリップが再生された場合のみランジを抑制（アニメと位置ランジの二重演出を避ける）。
                // クリップが無い（UnityChan 等）場合は false が返り、従来どおりプロシージャルランジを行う。
                _suppressLunge = _clipSwitcher != null && _clipSwitcher.PlayAttack(windup + recovery);
            }
            return began;
        }

        private void Awake()
        {
            // Motion を早期に生成して Null 参照を回避
            _ = Motion;
        }

        private void Update()
        {
            Motion.Tick(Time.deltaTime);
            UpdateProcAnim();
        }

        private void UpdateProcAnim()
        {
            if (_modelRoot == null) return;

            // 初回 None フェーズ時にベースポジションを記録
            if (!_baseLocalPosRecorded && Motion.Phase == AttackPhase.None)
            {
                _baseLocalPos         = _modelRoot.localPosition;
                _baseLocalPosRecorded = true;
            }

            if (!_baseLocalPosRecorded) return;

            // 実攻撃クリップ再生中はランジを抑制。モデルは基準位置を維持し、
            // Motion が None に戻ったタイミングで抑制を解除する。
            if (_suppressLunge)
            {
                _modelRoot.localPosition = _baseLocalPos;
                if (Motion.Phase == AttackPhase.None)
                    _suppressLunge = false;
                return;
            }

            const float windupZ    = -0.15f; // 後ろへ引く
            const float strikeZ    =  0.35f; // 前方へランジ（Recovery開始時の初期位置）
            const float lerpSpeed  =  8f;

            switch (Motion.Phase)
            {
                case AttackPhase.Windup:
                {
                    var target = _baseLocalPos + new Vector3(0f, 0f, windupZ);
                    _modelRoot.localPosition = Vector3.Lerp(
                        _modelRoot.localPosition, target, lerpSpeed * Time.deltaTime);
                    break;
                }
                case AttackPhase.Recovery:
                {
                    // Recovery 最初のフレームは前方ランジ位置から開始し、元位置へ戻す
                    // Strike 瞬間（Windup→Recovery 遷移時）にランジポジションをスナップ
                    var target = _baseLocalPos;
                    // ランジが残っていれば強制スナップ（1フレーム目のみ前方にいる）
                    var langePos = _baseLocalPos + new Vector3(0f, 0f, strikeZ);
                    if (Vector3.Distance(_modelRoot.localPosition, langePos) >
                        Vector3.Distance(_modelRoot.localPosition, _baseLocalPos + new Vector3(0f, 0f, windupZ)))
                    {
                        // Windup 側から来た場合はランジへスナップ
                        _modelRoot.localPosition = langePos;
                    }
                    _modelRoot.localPosition = Vector3.Lerp(
                        _modelRoot.localPosition, target, lerpSpeed * Time.deltaTime);
                    break;
                }
                case AttackPhase.None:
                {
                    // None 時は元位置へ戻す（キャンセル後など）
                    _modelRoot.localPosition = Vector3.Lerp(
                        _modelRoot.localPosition, _baseLocalPos, lerpSpeed * Time.deltaTime);
                    break;
                }
            }
        }

        /// <summary>
        /// Strike 瞬間にランジポジションをスナップさせるためのコールバックから呼び出すユーティリティ。
        /// AttackMotion の onStrike コールバック内で fire() の後に呼ぶことで、
        /// Recovery 初期フレームを前方位置から開始できる。
        /// </summary>
        public void SnapToLunge()
        {
            if (_modelRoot == null || !_baseLocalPosRecorded) return;
            _modelRoot.localPosition = _baseLocalPos + new Vector3(0f, 0f, 0.35f);
        }
    }
}
