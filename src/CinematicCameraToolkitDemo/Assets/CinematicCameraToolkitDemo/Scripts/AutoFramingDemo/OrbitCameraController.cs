using UnityEngine;

namespace CinematicCameraToolkitDemo.AutoFraming
{
    /// <summary>
    /// Pivot を中心とした球面上にカメラを配置する。
    ///
    /// AutoFraming と併用する場合、ここで書いた位置は SmoothedFramingFollower（実行順 0）が
    /// 「その姿勢で被写体が収まる位置」で上書きするため、Distance は構図に影響しない。
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public sealed class OrbitCameraController : MonoBehaviour
    {
        [Tooltip("配置するカメラ")]
        [SerializeField] private Transform _camera;

        [Tooltip("オービット角の入力元")]
        [SerializeField] private OrbitAngleController _input;

        [Tooltip("周回の中心。未設定ならワールド原点")]
        [SerializeField] private Transform _pivot;

        [Tooltip("Pivot からの距離 [m]")]
        [SerializeField] private float _distance = 3f;

        private void Awake()
        {
            // 未設定でも何も起きないだけなので、症状から原因へ辿れるよう起動時に知らせる。
            if (_camera == null) Debug.LogError($"[{nameof(OrbitCameraController)}] Camera が未設定です。", this);
            if (_input == null) Debug.LogError($"[{nameof(OrbitCameraController)}] オービット角の入力元が未設定です。", this);
        }

        private void LateUpdate()
        {
            if (_camera == null || _input == null) return;

            var center = _pivot != null ? _pivot.position : Vector3.zero;

            // 水平角がヨー、垂直角がピッチに対応する（Euler の引数順は pitch, yaw）。
            _camera.SetPositionAndRotation(
                center + OrbitOffset(_input.HorizontalAngle, _input.VerticalAngle, _distance),
                Quaternion.Euler(_input.VerticalAngle, _input.HorizontalAngle, 0f));
        }

        /// <summary>
        /// 球面座標を中心からのオフセットへ変換する。
        /// 水平角は +Z を 0° として Y 軸まわりに時計回り、垂直角は水平面からの仰角で、
        /// 正のときカメラは中心より上に来る。
        /// </summary>
        public static Vector3 OrbitOffset(
            float horizontalAngleDegrees, float verticalAngleDegrees, float distance)
        {
            var horizontalAngle = horizontalAngleDegrees * Mathf.Deg2Rad;
            var verticalAngle = verticalAngleDegrees * Mathf.Deg2Rad;
            var radius = distance * Mathf.Cos(verticalAngle);

            return new Vector3(
                -radius * Mathf.Sin(horizontalAngle),
                distance * Mathf.Sin(verticalAngle),
                -radius * Mathf.Cos(horizontalAngle));
        }
    }
}
