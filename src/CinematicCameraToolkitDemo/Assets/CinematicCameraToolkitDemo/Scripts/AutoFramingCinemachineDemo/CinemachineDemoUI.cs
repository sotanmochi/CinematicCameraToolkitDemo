using System;
using System.Collections.Generic;
using CinematicCameraToolkit;
using CinematicCameraToolkitDemo.AutoFraming;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.UIElements;

namespace CinematicCameraToolkitDemo.AutoFramingCinemachine
{
    /// <summary>
    /// Cinemachine 版デモの操作パネル。
    ///
    /// <see cref="AutoFramingDemoUI"/> と同じ方針。ただしマージンとスムージングは
    /// アクティブショットの設定を映すため、ショット切替時に Director の通知で再同期する。
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class CinemachineDemoUI : MonoBehaviour
    {
        [SerializeField] private CinemachineAutoFramingDemoDirector _director;

        private UIDocument _document;
        private VisualElement _root;
        private bool _bound;

        private FramingGuideView _guide;

        private VisualElement _shotTabs;
        private readonly List<Button> _shotButtons = new();

        private DropdownField _blendStyleDropdown;
        private Slider _blendTimeSlider;

        private Slider _fovSlider;
        private Slider _dutchSlider;

        private Button _modePose;
        private Button _modeAnimation;
        private VisualElement _poseGroup;
        private SliderInt _poseCountSlider;
        private VisualElement _animationGroup;
        private Label _poseLabel;
        private Slider _speedSlider;

        private Toggle _smoothingToggle;
        private DropdownField _presetDropdown;

        private Slider _marginLeftSlider;
        private Slider _marginRightSlider;
        private Slider _marginBottomSlider;
        private Slider _marginTopSlider;
        private DropdownField _alignHDropdown;
        private DropdownField _alignVDropdown;

        private Slider _sensitivitySlider;
        private Toggle _autoOrbitToggle;
        private Toggle _autoOrbitClockwiseToggle;
        private Button _resetViewButton;

        private static readonly string[] PresetNames = Enum.GetNames(typeof(FramingSmoothingPreset));
        private static readonly string[] AlignmentNames = Enum.GetNames(typeof(FramingAxisAlignment));
        private static readonly string[] BlendStyleNames = Enum.GetNames(typeof(CinemachineBlendDefinition.Styles));

        private void Awake()
        {
            _document = GetComponent<UIDocument>();
        }

        private void OnDisable()
        {
            if (_director != null)
            {
                _director.FramingStateInvalidated -= RefreshFramingValues;
                if (_director.Orbit != null)
                {
                    _director.Orbit.PointerOverUiPredicate = null;
                    _director.Orbit.KeyboardCapturedByUiPredicate = null;
                }
            }
            _bound = false;
            _guide = null;
        }

        private void Update()
        {
            if (!EnsureBound()) return;

            DemoUiFocus.ReleaseFocusOnClickOutside(_root, IsPointerOverUI);
            RefreshDynamicState();
            UpdateGuide();
        }

        /// <summary>
        /// true の間は方向キーをカメラへ渡さない。
        /// </summary>
        public bool IsKeyboardCapturedByUI()
        {
            return DemoUiFocus.ConsumesArrowKeys(_root);
        }

        /// <summary>
        /// 装飾用の要素は picking-mode=Ignore にしてあるので、その上ではドラッグが通る。
        /// </summary>
        public bool IsPointerOverUI(Vector2 screenPosition)
        {
            return DemoUiFocus.IsPointerOverUi(_root, screenPosition);
        }

        private bool EnsureBound()
        {
            if (_bound) return true;
            if (_director == null) return false;

            _root = _document != null ? _document.rootVisualElement : null;
            if (_root == null) return false;

            QueryElements();
            BuildShotTabs();
            InitializeValues();
            RegisterCallbacks();

            _guide = FramingGuideView.TryCreate(_root);

            _director.FramingStateInvalidated += RefreshFramingValues;
            if (_director.Orbit != null)
            {
                _director.Orbit.PointerOverUiPredicate = IsPointerOverUI;
                _director.Orbit.KeyboardCapturedByUiPredicate = IsKeyboardCapturedByUI;
            }

            _bound = true;
            return true;
        }

        /// <summary>
        /// 枠はアクティブショットの値。ブレンド中は Brain が 2 つの構図を補間しているため、
        /// 枠と被写体は一時的に一致しない。
        /// </summary>
        private void UpdateGuide()
        {
            if (_guide == null) return;

            var framing = _director.ActiveFraming;
            if (framing == null) _guide.Hide();
            else _guide.Show(framing.CurrentMargin);
        }

        private void QueryElements()
        {
            _shotTabs = _root.Q<VisualElement>("shot-tabs");

            _blendStyleDropdown = _root.Q<DropdownField>("blend-style-dropdown");
            _blendTimeSlider = _root.Q<Slider>("blend-time-slider");

            _fovSlider = _root.Q<Slider>("fov-slider");
            _dutchSlider = _root.Q<Slider>("dutch-slider");

            _modePose = _root.Q<Button>("mode-pose");
            _modeAnimation = _root.Q<Button>("mode-animation");
            _poseGroup = _root.Q<VisualElement>("pose-group");
            _poseCountSlider = _root.Q<SliderInt>("pose-count-slider");
            _animationGroup = _root.Q<VisualElement>("animation-group");
            _poseLabel = _root.Q<Label>("pose-label");
            _speedSlider = _root.Q<Slider>("speed-slider");

            _smoothingToggle = _root.Q<Toggle>("smoothing-toggle");
            _presetDropdown = _root.Q<DropdownField>("preset-dropdown");

            _marginLeftSlider = _root.Q<Slider>("margin-left-slider");
            _marginRightSlider = _root.Q<Slider>("margin-right-slider");
            _marginBottomSlider = _root.Q<Slider>("margin-bottom-slider");
            _marginTopSlider = _root.Q<Slider>("margin-top-slider");
            _alignHDropdown = _root.Q<DropdownField>("align-h-dropdown");
            _alignVDropdown = _root.Q<DropdownField>("align-v-dropdown");

            _sensitivitySlider = _root.Q<Slider>("sensitivity-slider");
            _autoOrbitToggle = _root.Q<Toggle>("auto-orbit-toggle");
            _autoOrbitClockwiseToggle = _root.Q<Toggle>("auto-orbit-clockwise-toggle");
            _resetViewButton = _root.Q<Button>("reset-view-button");
        }

        /// <summary>
        /// ショットタブは ShotSwitcher の定義から作る。UXML に固定で並べると
        /// ショットを足したときに UI 側の更新漏れが起きるため。
        /// </summary>
        private void BuildShotTabs()
        {
            if (_shotTabs == null) return;

            _shotTabs.Clear();
            _shotButtons.Clear();

            var shots = _director.Shots != null ? _director.Shots.Shots : null;
            if (shots == null) return;

            for (var i = 0; i < shots.Count; i++)
            {
                var index = i;
                var button = new Button(() => _director.SelectShot(index)) { text = shots[i].DisplayName };
                button.AddToClassList("tab");
                _shotTabs.Add(button);
                _shotButtons.Add(button);
            }
        }

        private void InitializeValues()
        {
            var pose = _director.Pose;
            var shots = _director.Shots;
            var orbit = _director.Orbit;

            SetChoices(_presetDropdown, PresetNames);
            SetChoices(_alignHDropdown, AlignmentNames);
            SetChoices(_alignVDropdown, AlignmentNames);
            SetChoices(_blendStyleDropdown, BlendStyleNames);

            if (shots != null)
            {
                if (_blendStyleDropdown != null) _blendStyleDropdown.index = (int)shots.BlendStyle;
                _blendTimeSlider?.SetValueWithoutNotify(shots.BlendTime);
            }

            _fovSlider?.SetValueWithoutNotify(_director.FieldOfView);
            _dutchSlider?.SetValueWithoutNotify(_director.Dutch);

            if (_speedSlider != null && pose != null) _speedSlider.SetValueWithoutNotify(pose.PlaybackSpeed);
            if (_poseCountSlider != null && pose != null) _poseCountSlider.SetValueWithoutNotify(pose.PoseCount);

            if (_sensitivitySlider != null && orbit != null)
            {
                _sensitivitySlider.SetValueWithoutNotify(orbit.Sensitivity);
            }

            if (_autoOrbitToggle != null && orbit != null)
            {
                _autoOrbitToggle.SetValueWithoutNotify(orbit.AutoOrbitEnabled);
            }

            if (_autoOrbitClockwiseToggle != null && orbit != null)
            {
                _autoOrbitClockwiseToggle.SetValueWithoutNotify(orbit.AutoOrbitClockwise);
            }

            RefreshFramingValues();
        }

        private void RefreshFramingValues()
        {
            _smoothingToggle?.SetValueWithoutNotify(_director.SmoothingEnabled);

            _marginLeftSlider?.SetValueWithoutNotify(_director.BaseMarginLeft);
            _marginRightSlider?.SetValueWithoutNotify(_director.BaseMarginRight);
            _marginBottomSlider?.SetValueWithoutNotify(_director.BaseMarginBottom);
            _marginTopSlider?.SetValueWithoutNotify(_director.BaseMarginTop);

            var framing = _director.ActiveFraming;
            if (framing == null) return;

            if (_presetDropdown != null) _presetDropdown.index = (int)framing.Preset;
            if (_alignHDropdown != null) _alignHDropdown.index = (int)framing.HorizontalAlignment;
            if (_alignVDropdown != null) _alignVDropdown.index = (int)framing.VerticalAlignment;
        }

        private void RegisterCallbacks()
        {
            _blendStyleDropdown?.RegisterValueChangedCallback(_ =>
            {
                if (_director.Shots == null) return;
                var index = Mathf.Clamp(_blendStyleDropdown.index, 0, BlendStyleNames.Length - 1);
                _director.Shots.BlendStyle = (CinemachineBlendDefinition.Styles)index;
            });

            _blendTimeSlider?.RegisterValueChangedCallback(e =>
            {
                if (_director.Shots != null) _director.Shots.BlendTime = e.newValue;
            });

            _fovSlider?.RegisterValueChangedCallback(e => _director.FieldOfView = e.newValue);
            _dutchSlider?.RegisterValueChangedCallback(e => _director.Dutch = e.newValue);

            if (_modePose != null) _modePose.clicked += () => _director.SetMode(AutoFramingDemoMode.Pose);
            if (_modeAnimation != null)
                _modeAnimation.clicked += () => _director.SetMode(AutoFramingDemoMode.Animation);

            if (_root.Q<Button>("pose-prev") is Button prev) prev.clicked += _director.RequestPrevPose;
            if (_root.Q<Button>("pose-next") is Button next) next.clicked += _director.RequestNextPose;

            _speedSlider?.RegisterValueChangedCallback(e =>
            {
                if (_director.Pose != null) _director.Pose.PlaybackSpeed = e.newValue;
            });

            _poseCountSlider?.RegisterValueChangedCallback(e => _director.PoseCount = e.newValue);

            _smoothingToggle?.RegisterValueChangedCallback(e => _director.SmoothingEnabled = e.newValue);

            _presetDropdown?.RegisterValueChangedCallback(_ =>
            {
                var index = Mathf.Clamp(_presetDropdown.index, 0, PresetNames.Length - 1);
                _director.SmoothingPreset = (FramingSmoothingPreset)index;
            });

            _marginLeftSlider?.RegisterValueChangedCallback(e =>
            {
                _director.BaseMarginLeft = e.newValue;
                _marginLeftSlider.SetValueWithoutNotify(_director.BaseMarginLeft);
            });
            _marginRightSlider?.RegisterValueChangedCallback(e =>
            {
                _director.BaseMarginRight = e.newValue;
                _marginRightSlider.SetValueWithoutNotify(_director.BaseMarginRight);
            });
            _marginBottomSlider?.RegisterValueChangedCallback(e =>
            {
                _director.BaseMarginBottom = e.newValue;
                _marginBottomSlider.SetValueWithoutNotify(_director.BaseMarginBottom);
            });
            _marginTopSlider?.RegisterValueChangedCallback(e =>
            {
                _director.BaseMarginTop = e.newValue;
                _marginTopSlider.SetValueWithoutNotify(_director.BaseMarginTop);
            });

            _alignHDropdown?.RegisterValueChangedCallback(_ =>
            {
                var framing = _director.ActiveFraming;
                if (framing == null) return;

                var index = Mathf.Clamp(_alignHDropdown.index, 0, AlignmentNames.Length - 1);
                framing.HorizontalAlignment = (FramingAxisAlignment)index;
                framing.ResetSmoothing();
            });

            _alignVDropdown?.RegisterValueChangedCallback(_ =>
            {
                var framing = _director.ActiveFraming;
                if (framing == null) return;

                var index = Mathf.Clamp(_alignVDropdown.index, 0, AlignmentNames.Length - 1);
                framing.VerticalAlignment = (FramingAxisAlignment)index;
                framing.ResetSmoothing();
            });

            _sensitivitySlider?.RegisterValueChangedCallback(e =>
            {
                if (_director.Orbit != null) _director.Orbit.Sensitivity = e.newValue;
            });

            _autoOrbitToggle?.RegisterValueChangedCallback(e =>
            {
                if (_director.Orbit != null) _director.Orbit.AutoOrbitEnabled = e.newValue;
            });

            _autoOrbitClockwiseToggle?.RegisterValueChangedCallback(e =>
            {
                if (_director.Orbit != null) _director.Orbit.AutoOrbitClockwise = e.newValue;
            });

            if (_resetViewButton != null) _resetViewButton.clicked += OnResetViewClicked;
        }

        private void OnResetViewClicked()
        {
            _director.ResetView();

            _fovSlider?.SetValueWithoutNotify(_director.FieldOfView);
            _dutchSlider?.SetValueWithoutNotify(_director.Dutch);
        }

        private void RefreshDynamicState()
        {
            var isPose = _director.Mode == AutoFramingDemoMode.Pose;

            SetSelected(_modePose, isPose);
            SetSelected(_modeAnimation, !isPose);
            SetVisible(_poseGroup, isPose);
            SetVisible(_animationGroup, !isPose);

            var activeIndex = _director.Shots != null ? _director.Shots.ActiveIndex : -1;
            for (var i = 0; i < _shotButtons.Count; i++) SetSelected(_shotButtons[i], i == activeIndex);

            if (_poseLabel != null && _director.Pose != null)
            {
                _poseLabel.text = string.IsNullOrEmpty(_director.Pose.CurrentPoseName)
                    ? "-"
                    : _director.Pose.CurrentPoseName;
            }

            // Pose モードではスムージングが強制 OFF になるため、トグルを無効化して理由を示す。
            _smoothingToggle?.SetEnabled(!isPose);
        }

        private static void SetChoices(DropdownField dropdown, string[] names)
        {
            if (dropdown == null) return;
            dropdown.choices = new List<string>(names);
        }

        private static void SetSelected(Button button, bool selected)
        {
            if (button == null) return;
            button.EnableInClassList("selected", selected);
        }

        private static void SetVisible(VisualElement element, bool visible)
        {
            if (element == null) return;
            element.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }
}
