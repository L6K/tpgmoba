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

        private AttackMotion _motion;
        private Vector3      _baseLocalPos;
        private bool         _baseLocalPosRecorded;

        public AttackMotion Motion
        {
            get
            {
                if (_motion == null) _motion = new AttackMotion();
                return _motion;
            }
        }

        /// <summary>AttackMotion.TryBegin に委譲して攻撃モーションを開始する。</summary>
        public bool RequestAttack(float windup, float recovery, Action fire)
        {
            return Motion.TryBegin(windup, recovery, fire);
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
