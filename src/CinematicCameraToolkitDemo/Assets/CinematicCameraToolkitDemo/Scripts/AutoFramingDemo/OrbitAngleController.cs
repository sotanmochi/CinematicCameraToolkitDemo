using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

namespace CinematicCameraToolkitDemo.AutoFraming
{
    /// <summary>
    /// オービット角の入力源。transform には一切触れない。
    ///
    /// 角度をカメラへ反映する相手は用途ごとに違う（<see cref="OrbitCameraController"/> は球面配置、
    /// Cinemachine 版は CinemachineOrbitalFollow）ため、角度を求める部分だけを切り出してある。
    ///
    /// 実行順 -100 は、角度を読む側より先に確定させるため。
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class OrbitAngleController : MonoBehaviour
    {
        [Header("Angles")]
        [Tooltip("水平角（方位角）[deg]。+Z 軸を 0° として Y 軸まわりに時計回り")]
        [FormerlySerializedAs("_yaw")]
        [SerializeField] private float _horizontalAngle = 180f;

        [Tooltip("垂直角（仰角）[deg]。水平面を 0° として、正のときカメラは中心より上")]
        [FormerlySerializedAs("_pitch")]
        [SerializeField] private float _verticalAngle = 8f;

        [Tooltip("±90° に近づくと被写体の投影が退化し、フレーミング解が極端になるためクランプする")]
        [FormerlySerializedAs("_minPitch")]
        [SerializeField] private float _minVerticalAngle = -20f;

        [FormerlySerializedAs("_maxPitch")]
        [SerializeField] private float _maxVerticalAngle = 60f;

        [Header("Input")]
        [Tooltip("ドラッグは deg / pixel。方向キーと Auto orbit の速度にも同じ係数が掛かる")]
        [FormerlySerializedAs("_dragSensitivity")]
        [SerializeField] private float _sensitivity = 0.5f;
        [SerializeField] private bool _invertY;
        [Tooltip("方向キーでの回転速度 [deg/s]。Sensitivity 1.0 のときの値")]
        [SerializeField] private float _keyRotateSpeed = 180f;
        [Tooltip("0 で慣性なし。値が大きいほど早く止まる")]
        [SerializeField] private float _inertiaDamping = 8f;
        [SerializeField] private bool _inputEnabled = true;

        [Header("Auto Orbit")]
        [SerializeField] private bool _autoOrbitEnabled;
        [Tooltip("自動回転の速度 [deg/s]。Sensitivity 1.0 のときの値")]
        [SerializeField] private float _autoOrbitSpeed = 40f;
        [Tooltip("真上から見て時計回りか。off で反時計回り")]
        [SerializeField] private bool _autoOrbitClockwise = true;

        private float _defaultHorizontalAngle;
        private float _defaultVerticalAngle;
        private Vector2 _angularVelocity;
        private bool _gestureActive;
        private bool _gestureOwnedByOrbit;

        /// <summary>
        /// UI 側が差し込む。null のときは常に UI 外として扱う。
        /// </summary>
        public Func<Vector2, bool> PointerOverUiPredicate { get; set; }

        /// <summary>
        /// true の間は方向キーをカメラに使わない。テキスト入力中のキャレット移動と取り合うため。
        /// </summary>
        public Func<bool> KeyboardCapturedByUiPredicate { get; set; }

        public float HorizontalAngle => _horizontalAngle;

        public float VerticalAngle => _verticalAngle;

        /// <summary>
        /// 真上から見た自動回転の向き。off で反時計回り。
        /// </summary>
        public bool AutoOrbitClockwise
        {
            get => _autoOrbitClockwise;
            set => _autoOrbitClockwise = value;
        }

        /// <summary>
        /// 操作感度。ドラッグ量・方向キー・Auto orbit の全部に掛かる。
        /// </summary>
        public float Sensitivity
        {
            get => _sensitivity;
            set => _sensitivity = Mathf.Max(0f, value);
        }

        public bool AutoOrbitEnabled
        {
            get => _autoOrbitEnabled;
            set => _autoOrbitEnabled = value;
        }

        /// <summary>
        /// 初期姿勢に戻す。慣性も破棄する。
        /// </summary>
        public void ResetToDefault()
        {
            _horizontalAngle = _defaultHorizontalAngle;
            _verticalAngle = Mathf.Clamp(_defaultVerticalAngle, _minVerticalAngle, _maxVerticalAngle);
            _angularVelocity = Vector2.zero;
        }

        private void Awake()
        {
            _defaultHorizontalAngle = _horizontalAngle;
            _defaultVerticalAngle = _verticalAngle;
        }

        private void LateUpdate()
        {
            var deltaTime = Time.deltaTime;

            if (_inputEnabled)
            {
                UpdatePointerInput(deltaTime);
                UpdateKeyboardInput(deltaTime);
            }
            else
            {
                _gestureActive = false;
                _gestureOwnedByOrbit = false;
            }

            if (!_gestureOwnedByOrbit) ApplyInertia(deltaTime);
            if (_autoOrbitEnabled && !_gestureOwnedByOrbit)
            {
                _horizontalAngle += _autoOrbitSpeed * _sensitivity * (_autoOrbitClockwise ? 1f : -1f) * deltaTime;
            }

            _verticalAngle = Mathf.Clamp(_verticalAngle, _minVerticalAngle, _maxVerticalAngle);
        }

        private void UpdatePointerInput(float deltaTime)
        {
            // タッチが実際に接触している間だけタッチ経路を使う。
            // タッチスクリーン搭載のノート PC でマウス操作が無視されるのを避ける。
            if (TryGetTouchDrag(out var pressed, out var released, out var screenPosition, out var delta))
            {
                ProcessDrag(pressed, released, screenPosition, delta, deltaTime);
                return;
            }

            var mouse = Mouse.current;
            if (mouse == null) return;

            ProcessDrag(
                mouse.leftButton.wasPressedThisFrame,
                mouse.leftButton.wasReleasedThisFrame || !mouse.leftButton.isPressed,
                mouse.position.ReadValue(),
                mouse.delta.ReadValue(),
                deltaTime);
        }

        private static bool TryGetTouchDrag(
            out bool pressed, out bool released, out Vector2 screenPosition, out Vector2 delta)
        {
            pressed = false;
            released = false;
            screenPosition = default;
            delta = default;

            var touchscreen = Touchscreen.current;
            if (touchscreen == null) return false;

            var touch = touchscreen.primaryTouch;
            if (touch == null) return false;
            if (!touch.press.isPressed && !touch.press.wasReleasedThisFrame) return false;

            pressed = touch.press.wasPressedThisFrame;
            released = touch.press.wasReleasedThisFrame || !touch.press.isPressed;
            screenPosition = touch.position.ReadValue();
            delta = touch.delta.ReadValue();

            // 2 本指はピンチとして扱うのでオービットには渡さない。
            if (CountActiveTouches(touchscreen) >= 2)
            {
                pressed = false;
                released = true;
                delta = Vector2.zero;
            }

            return true;
        }

        private void ProcessDrag(
            bool pressed, bool released, Vector2 screenPosition, Vector2 delta, float deltaTime)
        {
            if (pressed)
            {
                // ジェスチャの所有権は押した瞬間に決める。以降ポインタが UI 上を通過しても
                // ドラッグは中断されない。
                _gestureActive = true;
                _gestureOwnedByOrbit = !IsPointerOverUi(screenPosition);
                _angularVelocity = Vector2.zero;
            }

            if (!_gestureActive) return;

            if (released)
            {
                _gestureActive = false;
                _gestureOwnedByOrbit = false;
                return;
            }

            if (!_gestureOwnedByOrbit) return;

            var horizontalDelta = delta.x * _sensitivity;
            var verticalDelta = delta.y * _sensitivity * (_invertY ? 1f : -1f);

            _horizontalAngle += horizontalDelta;
            _verticalAngle = Mathf.Clamp(_verticalAngle + verticalDelta, _minVerticalAngle, _maxVerticalAngle);

            if (deltaTime > 0f) _angularVelocity = new Vector2(horizontalDelta, verticalDelta) / deltaTime;
        }

        private void ApplyInertia(float deltaTime)
        {
            if (_inertiaDamping <= 0f)
            {
                _angularVelocity = Vector2.zero;
                return;
            }

            if (_angularVelocity.sqrMagnitude < 0.0001f)
            {
                _angularVelocity = Vector2.zero;
                return;
            }

            _horizontalAngle += _angularVelocity.x * deltaTime;
            _verticalAngle = Mathf.Clamp(_verticalAngle + _angularVelocity.y * deltaTime, _minVerticalAngle, _maxVerticalAngle);
            _angularVelocity *= Mathf.Exp(-_inertiaDamping * deltaTime);
        }

        private static int CountActiveTouches(Touchscreen touchscreen)
        {
            var count = 0;
            var touches = touchscreen.touches;
            for (var i = 0; i < touches.Count; i++)
            {
                if (touches[i].press.isPressed) count++;
            }
            return count;
        }

        /// <summary>
        /// 方向キーでの回転。押した向きと同じ側へカメラが動く（＝ドラッグとは逆の符号則）。
        /// </summary>
        private void UpdateKeyboardInput(float deltaTime)
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return;
            if (KeyboardCapturedByUiPredicate != null && KeyboardCapturedByUiPredicate()) return;

            var horizontal = (keyboard.rightArrowKey.isPressed ? 1f : 0f) - (keyboard.leftArrowKey.isPressed ? 1f : 0f);
            var vertical = (keyboard.upArrowKey.isPressed ? 1f : 0f) - (keyboard.downArrowKey.isPressed ? 1f : 0f);
            if (horizontal == 0f && vertical == 0f) return;

            var step = _keyRotateSpeed * _sensitivity * deltaTime;
            _horizontalAngle -= horizontal * step;
            _verticalAngle = Mathf.Clamp(_verticalAngle + vertical * step * (_invertY ? -1f : 1f), _minVerticalAngle, _maxVerticalAngle);

            // キーを離した瞬間に止めたいので慣性は持たせない。
            _angularVelocity = Vector2.zero;
        }

        private bool IsPointerOverUi(Vector2 screenPosition)
        {
            return PointerOverUiPredicate != null && PointerOverUiPredicate(screenPosition);
        }
    }
}
