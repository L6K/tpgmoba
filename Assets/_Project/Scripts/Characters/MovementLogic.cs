using UnityEngine;

namespace Enigma.Character
{
    /// <summary>
    /// 状態を持たない純粋関数ユーティリティ。
    /// </summary>
    public static class MovementLogic
    {
        /// <summary>
        /// 入力ベクトルをカメラのヨー角で回転し、ワールド空間の移動方向を返す。
        /// 入力がゼロの場合は Vector3.zero を返す。
        /// </summary>
        public static Vector3 CameraRelativeMove(Vector2 input, float cameraYawDegrees)
        {
            if (input == Vector2.zero) return Vector3.zero;

            var worldInput = new Vector3(input.x, 0f, input.y);
            var rotation = Quaternion.Euler(0f, cameraYawDegrees, 0f);
            return (rotation * worldInput).normalized;
        }

        /// <summary>
        /// moveDirection がゼロの場合は current をそのまま返す。
        /// </summary>
        public static Quaternion RotateTowards(Quaternion current, Vector3 moveDirection, float maxDegreesDelta)
        {
            if (moveDirection == Vector3.zero) return current;

            var target = Quaternion.LookRotation(moveDirection, Vector3.up);
            return Quaternion.RotateTowards(current, target, maxDegreesDelta);
        }
    }
}
