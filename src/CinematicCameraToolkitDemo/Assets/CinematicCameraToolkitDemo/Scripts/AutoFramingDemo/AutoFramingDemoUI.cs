using System;
using System.Collections.Generic;
using CinematicCameraToolkit;
using UnityEngine;
using UnityEngine.UIElements;

namespace CinematicCameraToolkitDemo.AutoFraming
{
    /// <summary>
    /// 操作パネル。UI → モデルは各コールバック、モデル → UI は Update() での表示更新のみとし、
    /// 双方向バインドは行わない（値はユーザー操作でしか変わらないため）。
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class AutoFramingDemoUI : MonoBehaviour
    {
        [SerializeField] private AutoFramingDemoDirector _director;

        private UIDocument _document;
        private VisualElement _root;
        private bool _bound;

        private FramingGuideView _guide;

        private Button _modePose;
        private Button _modeAnimation;
        private VisualElement _poseGroup;
        private VisualElement _animationGroup;
        private Label _poseLabel;

        private Toggle _smoothingToggle;
        private DropdownField _presetDropdown;
        private Slider _speedSlider;

        private Slider _marginLeftSlider;
        private Slider _marginRightSlider;
        private Slider _marginBottomSlider;
        private Slider _marginTopSlider;
        private DropdownField _alignHDropdown;
        private DropdownField _alignVDropdown;

        private Slider _sensitivitySlider;
        private Toggle _autoOrbitToggle;
        private Button _resetViewButton;

        private static readonly string[] PresetNames = Enum.GetNames(typeof(FramingSmoothingPreset));
        private static readonly string[] AlignmentNames = Enum.GetNames(typeof(FramingAxisAlignment));

        private void Awake()
        {
            _document = GetComponent<UIDocument>();
        }

        private void OnDisable()
        {
            if (_director != null && _director.Orbit != null)
            {
                _director.Orbit.PointerOverUiPredicate = null;
                _director.Orbit.KeyboardCapturedByUiPredicate = null;
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
        /// 装飾用の要素は picking-mode=Ignore にしてあるので、その上ではドラッグが通る。
        /// </summary>
        public bool IsPointerOverUI(Vector2 screenPosition)
        {
            return DemoUiFocus.IsPointerOverUi(_root, screenPosition);
        }

        /// <summary>
        /// true の間は方向キーをカメラへ渡さない。
        /// </summary>
        public bool IsKeyboardCapturedByUI()
        {
            return DemoUiFocus.ConsumesArrowKeys(_root);
        }

        private bool EnsureBound()
        {
            if (_bound) return true;
            if (_director == null) return false;

            _root = _document != null ? _document.rootVisualElement : null;
            if (_root == null) return false;

            QueryElements();
            InitializeValues();
            RegisterCallbacks();

            _guide = FramingGuideView.TryCreate(_root);

            if (_director.Orbit != null)
            {
                _director.Orbit.PointerOverUiPredicate = IsPointerOverUI;
                _director.Orbit.KeyboardCapturedByUiPredicate = IsKeyboardCapturedByUI;
            }

            _bound = true;
            return true;
        }

        private void UpdateGuide()
        {
            if (_guide == null) return;

            var follower = _director.Follower;
            if (follower == null) _guide.Hide();
            else _guide.Show(follower.CurrentMargin);
        }

        private void QueryElements()
        {
            _modePose = _root.Q<Button>("mode-pose");
            _modeAnimation = _root.Q<Button>("mode-animation");
            _poseGroup = _root.Q<VisualElement>("pose-group");
            _animationGroup = _root.Q<VisualElement>("animation-group");
            _poseLabel = _root.Q<Label>("pose-label");

            _smoothingToggle = _root.Q<Toggle>("smoothing-toggle");
            _presetDropdown = _root.Q<DropdownField>("preset-dropdown");
            _speedSlider = _root.Q<Slider>("speed-slider");

            _marginLeftSlider = _root.Q<Slider>("margin-left-slider");
            _marginRightSlider = _root.Q<Slider>("margin-right-slider");
            _marginBottomSlider = _root.Q<Slider>("margin-bottom-slider");
            _marginTopSlider = _root.Q<Slider>("margin-top-slider");
            _alignHDropdown = _root.Q<DropdownField>("align-h-dropdown");
            _alignVDropdown = _root.Q<DropdownField>("align-v-dropdown");

            _sensitivitySlider = _root.Q<Slider>("sensitivity-slider");
            _autoOrbitToggle = _root.Q<Toggle>("auto-orbit-toggle");
            _resetViewButton = _root.Q<Button>("reset-view-button");
        }

        private void InitializeValues()
        {
            var follower = _director.Follower;
            var orbit = _director.Orbit;
            var pose = _director.Pose;

            SetChoices(_presetDropdown, PresetNames);
            SetChoices(_alignHDropdown, AlignmentNames);
            SetChoices(_alignVDropdown, AlignmentNames);

            _smoothingToggle?.SetValueWithoutNotify(_director.SmoothingEnabled);
            if (_presetDropdown != null && follower != null)
            {
                _presetDropdown.index = (int)follower.Preset;
            }

            if (_speedSlider != null && pose != null) _speedSlider.SetValueWithoutNotify(pose.PlaybackSpeed);

            _marginLeftSlider?.SetValueWithoutNotify(_director.BaseMarginLeft);
            _marginRightSlider?.SetValueWithoutNotify(_director.BaseMarginRight);
            _marginBottomSlider?.SetValueWithoutNotify(_director.BaseMarginBottom);
            _marginTopSlider?.SetValueWithoutNotify(_director.BaseMarginTop);

            if (follower != null)
            {
                if (_alignHDropdown != null) _alignHDropdown.index = (int)follower.HorizontalAlignment;
                if (_alignVDropdown != null) _alignVDropdown.index = (int)follower.VerticalAlignment;
            }

            if (_sensitivitySlider != null && orbit != null)
            {
                _sensitivitySlider.SetValueWithoutNotify(orbit.Sensitivity);
            }

            if (_autoOrbitToggle != null && orbit != null)
            {
                _autoOrbitToggle.SetValueWithoutNotify(orbit.AutoOrbitEnabled);
            }
        }

        private void RegisterCallbacks()
        {
            if (_modePose != null) _modePose.clicked += () => _director.SetMode(AutoFramingDemoMode.Pose);
            if (_modeAnimation != null)
                _modeAnimation.clicked += () => _director.SetMode(AutoFramingDemoMode.Animation);

            if (_root.Q<Button>("pose-prev") is Button prev) prev.clicked += _director.RequestPrevPose;
            if (_root.Q<Button>("pose-next") is Button next) next.clicked += _director.RequestNextPose;

            _smoothingToggle?.RegisterValueChangedCallback(e => _director.SmoothingEnabled = e.newValue);

            _presetDropdown?.RegisterValueChangedCallback(_ =>
            {
                var index = Mathf.Clamp(_presetDropdown.index, 0, PresetNames.Length - 1);
                _director.SmoothingPreset = (FramingSmoothingPreset)index;
            });

            _speedSlider?.RegisterValueChangedCallback(e =>
            {
                if (_director.Pose != null) _director.Pose.PlaybackSpeed = e.newValue;
            });

            // Director が反対側との間隔でマージンを丸めるので、実値をスライダーへ映し戻す。
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
                if (_director.Follower == null) return;
                var index = Mathf.Clamp(_alignHDropdown.index, 0, AlignmentNames.Length - 1);
                _director.Follower.HorizontalAlignment = (FramingAxisAlignment)index;
                _director.Follower.ResetSmoothing();
            });

            _alignVDropdown?.RegisterValueChangedCallback(_ =>
            {
                if (_director.Follower == null) return;
                var index = Mathf.Clamp(_alignVDropdown.index, 0, AlignmentNames.Length - 1);
                _director.Follower.VerticalAlignment = (FramingAxisAlignment)index;
                _director.Follower.ResetSmoothing();
            });

            _sensitivitySlider?.RegisterValueChangedCallback(e =>
            {
                if (_director.Orbit != null) _director.Orbit.Sensitivity = e.newValue;
            });

            _autoOrbitToggle?.RegisterValueChangedCallback(e =>
            {
                if (_director.Orbit != null) _director.Orbit.AutoOrbitEnabled = e.newValue;
            });

            if (_resetViewButton != null) _resetViewButton.clicked += _director.ResetView;
        }

        private void RefreshDynamicState()
        {
            var isPose = _director.Mode == AutoFramingDemoMode.Pose;

            SetSelected(_modePose, isPose);
            SetSelected(_modeAnimation, !isPose);
            SetVisible(_poseGroup, isPose);
            SetVisible(_animationGroup, !isPose);

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
