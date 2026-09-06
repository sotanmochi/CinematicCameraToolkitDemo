using CinematicCameraToolkit;
using UnityEngine;

namespace CinematicCameraToolkitDemo.AutoFraming
{
    /// <summary>
    /// Cinemachine を使わない AutoFramingDemo シーンの Director。
    ///
    /// フレーミング実装は immutable なパッケージにあり Script Execution Order を設定できない。
    /// SpringBone とアニメーションを自動更新から外してここから駆動することで、
    /// 「アニメーション評価 → SpringBone → カメラ姿勢 → フレーミング計算」の順序を確定させる。
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class AutoFramingDemoDirector : MonoBehaviour
    {
        [SerializeField] private CharacterPoseController _pose;
        [SerializeField] private SmoothedFramingFollower _follower;
        [SerializeField] private OrbitAngleController _orbit;

        [Header("Initial State")]
        [SerializeField] private AutoFramingDemoMode _initialMode = AutoFramingDemoMode.Animation;
        [SerializeField] private bool _initialSmoothingEnabled = true;
        [SerializeField] private FramingSmoothingPreset _initialPreset = FramingSmoothingPreset.Cinematic;

        private float _baseMarginLeft;
        private float _baseMarginRight;
        private float _baseMarginBottom;
        private float _baseMarginTop;

        private bool _userSmoothingEnabled = true;
        private bool _skipSpringUpdateOnce;
        private int _pendingPoseSteps;
        private bool _pendingModeChange;
        private AutoFramingDemoMode _requestedMode;

        public AutoFramingDemoMode Mode { get; private set; } = AutoFramingDemoMode.Animation;

        public CharacterPoseController Pose => _pose;
        public SmoothedFramingFollower Follower => _follower;
        public OrbitAngleController Orbit => _orbit;

        /// <summary>
        /// Pose モードでは OFF に強制されるが、値は保持して Animation モードで復帰する。
        /// </summary>
        public bool SmoothingEnabled
        {
            get => _userSmoothingEnabled;
            set
            {
                _userSmoothingEnabled = value;
                ApplySmoothingForCurrentMode();
            }
        }

        public FramingSmoothingPreset SmoothingPreset
        {
            get => _follower != null ? _follower.Preset : _initialPreset;
            set
            {
                if (_follower == null) return;

                _follower.Preset = value;
                ApplySmoothingForCurrentMode();
                _follower.ResetSmoothing();
            }
        }

        /// <summary>
        /// マージン [%]。
        /// </summary>
        public float BaseMarginLeft
        {
            get => _baseMarginLeft;
            set { _baseMarginLeft = RenderTargetMarginLimits.ClampPercentage(value, _baseMarginRight); ApplyMargins(); }
        }

        public float BaseMarginRight
        {
            get => _baseMarginRight;
            set { _baseMarginRight = RenderTargetMarginLimits.ClampPercentage(value, _baseMarginLeft); ApplyMargins(); }
        }

        public float BaseMarginBottom
        {
            get => _baseMarginBottom;
            set { _baseMarginBottom = RenderTargetMarginLimits.ClampPercentage(value, _baseMarginTop); ApplyMargins(); }
        }

        public float BaseMarginTop
        {
            get => _baseMarginTop;
            set { _baseMarginTop = RenderTargetMarginLimits.ClampPercentage(value, _baseMarginBottom); ApplyMargins(); }
        }

        public void RequestNextPose()
        {
            _pendingPoseSteps++;
        }

        public void RequestPrevPose()
        {
            _pendingPoseSteps--;
        }

        /// <summary>
        /// UI から即座に呼ばれても安全なよう、切替はフレーム先頭まで遅延させる。
        /// </summary>
        public void SetMode(AutoFramingDemoMode mode)
        {
            _requestedMode = mode;
            _pendingModeChange = true;
        }

        public void ResetView()
        {
            if (_orbit != null) _orbit.ResetToDefault();
            if (_follower != null) _follower.ResetSmoothing();
        }

        private void Awake()
        {
            _userSmoothingEnabled = _initialSmoothingEnabled;
        }

        private void Start()
        {
            if (_follower != null) _follower.Preset = _initialPreset;

            // ApplyMargins が上書きするので、Inspector 設定値の取り込みはその前に行う。
            CaptureBaseMargins();

            ApplyMargins();
            EnterMode(_initialMode);
        }

        private void Update()
        {
            if (_pendingModeChange)
            {
                _pendingModeChange = false;
                if (_requestedMode != Mode) EnterMode(_requestedMode);
            }

            if (_pendingPoseSteps == 0) return;

            var steps = _pendingPoseSteps;
            _pendingPoseSteps = 0;

            if (Mode != AutoFramingDemoMode.Pose || _pose == null) return;

            _pose.ApplyPose(_pose.CurrentPoseIndex + steps);

            _skipSpringUpdateOnce = true;
            if (_follower != null) _follower.ResetSmoothing();
        }

        private void LateUpdate()
        {
            if (_pose == null) return;

            // Pose モードは FastForward 後の姿勢で静止させたいので SpringBone を進めない。
            if (Mode != AutoFramingDemoMode.Animation) return;

            _pose.Advance(Time.deltaTime);

            if (_skipSpringUpdateOnce) _skipSpringUpdateOnce = false;
            else _pose.UpdateSpringBones();
        }

        private void EnterMode(AutoFramingDemoMode mode)
        {
            Mode = mode;

            if (_pose != null)
            {
                // 現在時刻に最も近いポーズから始めると、切替時の見た目の飛びが小さい。
                if (mode == AutoFramingDemoMode.Pose) _pose.ApplyPose(NearestPoseIndex());
                else _pose.SettleSpringBones();

                // FastForward 済みなので、このフレームの SpringBone 通常更新はスキップする。
                _skipSpringUpdateOnce = true;
            }

            ApplySmoothingForCurrentMode();
            if (_follower != null) _follower.ResetSmoothing();
        }

        private int NearestPoseIndex()
        {
            if (_pose == null) return 0;

            var count = _pose.PoseCount;
            if (count <= 1 || _pose.ClipLength <= 0f) return 0;

            var normalized = (float)(_pose.CurrentTime / _pose.ClipLength);
            return Mathf.Clamp(Mathf.RoundToInt(normalized * (count - 1)), 0, count - 1);
        }

        /// <summary>
        /// Pose モードで OFF にするのは、離散的なポーズ切替がフィルタにステップ入力として入り、
        /// 「新しい構図へゆっくり寄る」動きになってしまうため。
        /// </summary>
        private void ApplySmoothingForCurrentMode()
        {
            if (_follower == null) return;
            _follower.SmoothingEnabled = Mode == AutoFramingDemoMode.Animation && _userSmoothingEnabled;
        }

        private void CaptureBaseMargins()
        {
            if (_follower == null) return;

            _baseMarginLeft = _follower.MarginLeft;
            _baseMarginRight = _follower.MarginRight;
            _baseMarginBottom = _follower.MarginBottom;
            _baseMarginTop = _follower.MarginTop;
        }

        private void ApplyMargins()
        {
            if (_follower == null) return;

            _follower.MarginLeft = _baseMarginLeft;
            _follower.MarginRight = _baseMarginRight;
            _follower.MarginBottom = _baseMarginBottom;
            _follower.MarginTop = _baseMarginTop;
        }
    }
}
