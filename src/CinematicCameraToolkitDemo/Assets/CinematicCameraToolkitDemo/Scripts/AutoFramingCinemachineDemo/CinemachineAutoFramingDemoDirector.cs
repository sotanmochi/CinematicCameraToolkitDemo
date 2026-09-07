using System;
using CinematicCameraToolkit;
using CinematicCameraToolkit.Cinemachine;
using CinematicCameraToolkitDemo.AutoFraming;
using UnityEngine;

namespace CinematicCameraToolkitDemo.AutoFramingCinemachine
{
    /// <summary>
    /// Cinemachine 版 AutoFraming デモの Director。
    ///
    /// Cinemachine なし版と同じ状態機械を持つが、書き込み先がショットごとに複数あり、
    /// アクティブショットに応じて入れ替わる点が違う。
    ///
    /// SpringBone とアニメーションは自動更新から外してここから駆動する。実行順 -100 で
    /// CinemachineBrain（既定 0）より先に回るため、SpringBone → Brain の順序が保たれる。
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class CinemachineAutoFramingDemoDirector : MonoBehaviour
    {
        [SerializeField] private CharacterPoseController _pose;
        [SerializeField] private ShotSwitcher _shots;

        [Tooltip("オービット角の入力元。Cinemachine なし版と同じ操作感にするため共通のものを使う")]
        [SerializeField] private OrbitAngleController _orbit;

        [Header("Initial State")]
        [SerializeField] private AutoFramingDemoMode _initialMode = AutoFramingDemoMode.Animation;
        [SerializeField] private bool _initialSmoothingEnabled = true;
        [SerializeField] private FramingSmoothingPreset _initialPreset = FramingSmoothingPreset.Cinematic;

        [Header("Lens")]
        [Tooltip("被写体の画面占有サイズは AutoFraming が保つため、FOV を振るとドリーズームになる")]
        [SerializeField] [Range(10f, 90f)] private float _fieldOfView = 40f;
        [Tooltip("画面のロール。拡張は Dutch 込みの姿勢で解くので、傾けても被写体は画面端で切れない")]
        [SerializeField] [Range(-45f, 45f)] private float _dutch;

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
        public ShotSwitcher Shots => _shots;
        public OrbitAngleController Orbit => _orbit;

        /// <summary>
        /// アクティブショットの拡張。ショット切替で実体が変わるのでキャッシュしない。
        /// </summary>
        public CinemachineAutoFraming ActiveFraming
        {
            get
            {
                var shot = _shots != null ? _shots.ActiveShot : null;
                return shot != null ? shot.Framing : null;
            }
        }

        /// <summary>
        /// UI が表示値を取り込み直すために購読する。
        /// </summary>
        public event Action FramingStateInvalidated;

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
            get
            {
                var framing = ActiveFraming;
                return framing != null ? framing.Preset : _initialPreset;
            }
            set
            {
                ForEachFraming(framing => framing.Preset = value);
                ApplySmoothingForCurrentMode();
                ResetFramingSmoothing();
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

        /// <summary>
        /// Pose モード中に変えても、最も近い番号で取り直すので見た目が飛ばない。
        /// </summary>
        public int PoseCount
        {
            get => _pose != null ? _pose.PoseCount : 0;
            set
            {
                if (_pose == null) return;

                var clamped = Mathf.Clamp(
                    value, CharacterPoseController.MinPoseCount, CharacterPoseController.MaxPoseCount);
                if (_pose.PoseCount == clamped) return;

                _pose.PoseCount = clamped;
                if (Mode != AutoFramingDemoMode.Pose) return;

                _pose.ApplyPose(NearestPoseIndex());

                _skipSpringUpdateOnce = true;
                ResetFramingSmoothing();
            }
        }

        public float FieldOfView
        {
            get => _fieldOfView;
            set
            {
                _fieldOfView = Mathf.Clamp(value, 10f, 90f);
                ApplyLens();
            }
        }

        public float Dutch
        {
            get => _dutch;
            set
            {
                _dutch = Mathf.Clamp(value, -45f, 45f);
                ApplyLens();
            }
        }

        public void SelectShot(int index)
        {
            _shots?.Select(index);
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
            ResetFramingSmoothing();
            FieldOfView = 40f;
            Dutch = 0f;
        }

        private void Awake()
        {
            _userSmoothingEnabled = _initialSmoothingEnabled;
        }

        private void OnEnable()
        {
            if (_shots != null) _shots.ActiveShotChanged += OnActiveShotChanged;
        }

        private void OnDisable()
        {
            if (_shots != null) _shots.ActiveShotChanged -= OnActiveShotChanged;
        }

        private void Start()
        {
            // レンズはショットに依存しないので、対象を配る前に一度当てておく。
            ApplyLens();

            ForEachFraming(framing => framing.Preset = _initialPreset);

            // ApplyMargins が上書きするので、Inspector 設定値の取り込みはその前に行う。
            CaptureBaseMargins();

            ApplyMargins();
            EnterMode(_initialMode);

            // 1 フレーム目から正しい向きで解けるよう、Brain が回る前に角度を配っておく。
            ApplyOrbit();
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
            ResetFramingSmoothing();
        }

        private void LateUpdate()
        {
            if (_pose != null && Mode == AutoFramingDemoMode.Animation)
            {
                _pose.Advance(Time.deltaTime);

                if (_skipSpringUpdateOnce) _skipSpringUpdateOnce = false;
                else _pose.UpdateSpringBones();
            }


            ApplyOrbit();
        }

        private void OnActiveShotChanged()
        {
            // 新しいショットのマージンを基準値として取り込み直す。ショットごとに構図の性格が違う。
            CaptureBaseMargins();
            ApplyMargins();
            ApplySmoothingForCurrentMode();
            ResetFramingSmoothing();
            FramingStateInvalidated?.Invoke();
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
            ResetFramingSmoothing();
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
        /// Cinemachine なし版と同じ入力源を使い、慣性・タッチ・UI 上でのドラッグ抑止を共有する。
        /// </summary>
        private void ApplyOrbit()
        {
            if (_shots == null || _orbit == null) return;

            // CinemachineOrbitalFollow の水平軸は ±180° 範囲なので折り返して渡す。
            var horizontal = Mathf.DeltaAngle(0f, _orbit.HorizontalAngle);
            _shots.ApplyOrbit(horizontal, _orbit.VerticalAngle);
        }

        /// <summary>
        /// Pose モードで OFF にするのは、離散的なポーズ切替がフィルタにステップ入力として入り、
        /// 「新しい構図へゆっくり寄る」動きになってしまうため。
        /// </summary>
        private void ApplySmoothingForCurrentMode()
        {
            var enabled = Mode == AutoFramingDemoMode.Animation && _userSmoothingEnabled;
            ForEachFraming(framing => framing.SmoothingEnabled = enabled);
        }

        private void ResetFramingSmoothing()
        {
            ForEachFraming(framing => framing.ResetSmoothing());
        }

        private void CaptureBaseMargins()
        {
            var framing = ActiveFraming;
            if (framing == null) return;

            _baseMarginLeft = framing.MarginLeft;
            _baseMarginRight = framing.MarginRight;
            _baseMarginBottom = framing.MarginBottom;
            _baseMarginTop = framing.MarginTop;
        }

        /// <summary>
        /// マージンはアクティブショットにだけ効かせる。ショットごとに構図の性格が違う。
        /// </summary>
        private void ApplyMargins()
        {
            var framing = ActiveFraming;
            if (framing == null) return;

            framing.MarginLeft = _baseMarginLeft;
            framing.MarginRight = _baseMarginRight;
            framing.MarginBottom = _baseMarginBottom;
            framing.MarginTop = _baseMarginTop;
        }

        private void ApplyLens()
        {
            if (_shots == null) return;

            var shots = _shots.Shots;
            for (var i = 0; i < shots.Count; i++)
            {
                var camera = shots[i].Camera;
                if (camera == null) continue;

                camera.Lens.FieldOfView = _fieldOfView;
                camera.Lens.Dutch = _dutch;
            }
        }

        /// <summary>
        /// 全ショットへ配る。ブレンド中は 2 台が同時に解かれるので、
        /// スムージング系はアクティブ以外にも当てる必要がある。
        /// </summary>
        private void ForEachFraming(Action<CinemachineAutoFraming> action)
        {
            if (_shots == null) return;

            var shots = _shots.Shots;
            for (var i = 0; i < shots.Count; i++)
            {
                var framing = shots[i].Framing;
                if (framing != null) action(framing);
            }
        }
    }
}
