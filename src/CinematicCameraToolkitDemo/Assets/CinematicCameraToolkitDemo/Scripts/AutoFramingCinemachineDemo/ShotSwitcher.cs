using System;
using System.Collections.Generic;
using CinematicCameraToolkit.Cinemachine;
using Unity.Cinemachine;
using UnityEngine;

namespace CinematicCameraToolkitDemo.AutoFramingCinemachine
{
    /// <summary>
    /// 1 つのショット。CinemachineCamera と、そこに載る AutoFraming 拡張をまとめる。
    /// 対象レンダラーの絞り込みは <see cref="CinemachineFramingTargetBinder"/> が持つ。
    /// </summary>
    [Serializable]
    public sealed class ShotDefinition
    {
        [SerializeField] private string _displayName = "Shot";
        [SerializeField] private CinemachineCamera _camera;
        [SerializeField] private CinemachineAutoFraming _framing;

        public string DisplayName => string.IsNullOrEmpty(_displayName) ? "Shot" : _displayName;
        public CinemachineCamera Camera => _camera;
        public CinemachineAutoFraming Framing => _framing;
        public bool IsValid => _camera != null && _framing != null;
    }

    /// <summary>
    /// ショットの切り替えと、Brain のブレンド設定を担当する。
    ///
    /// 切り替えは Priority の操作だけで行い、実際の遷移は CinemachineBrain に任せる。
    /// ブレンド中は独立に解かれた 2 つのフレーミング解の間を Brain が補間するため、
    /// 「正しい構図から正しい構図へのドリー」になる。これは Cinemachine と
    /// 組み合わせて初めて得られる絵で、このシーンで見せたい部分になる。
    /// </summary>
    public sealed class ShotSwitcher : MonoBehaviour
    {
        [SerializeField] private CinemachineBrain _brain;
        [SerializeField] private List<ShotDefinition> _shots = new();
        [SerializeField] private int _initialIndex;

        [Header("Priority")]
        [SerializeField] private int _activePriority = 20;
        [SerializeField] private int _inactivePriority;

        private int _activeIndex = -1;

        /// <summary>
        /// アクティブショットが変わったときに発火する。
        /// </summary>
        public event Action ActiveShotChanged;

        public CinemachineBrain Brain => _brain;
        public IReadOnlyList<ShotDefinition> Shots => _shots;
        public int ActiveIndex => _activeIndex;

        public ShotDefinition ActiveShot =>
            _activeIndex >= 0 && _activeIndex < _shots.Count ? _shots[_activeIndex] : null;

        public bool IsBlending => _brain != null && _brain.IsBlending;

        /// <summary>
        /// ブレンドの進行度 0..1。ブレンドしていなければ 1。
        /// </summary>
        public float BlendProgress
        {
            get
            {
                var blend = _brain != null ? _brain.ActiveBlend : null;
                if (blend == null || blend.Duration <= 0f) return 1f;
                return Mathf.Clamp01(blend.TimeInBlend / blend.Duration);
            }
        }

        public string ActiveVirtualCameraName
        {
            get
            {
                var active = _brain != null ? _brain.ActiveVirtualCamera : null;
                return active != null ? active.Name : "-";
            }
        }

        public float BlendTime
        {
            get => _brain != null ? _brain.DefaultBlend.Time : 0f;
            set
            {
                if (_brain == null) return;
                _brain.DefaultBlend.Time = Mathf.Max(0f, value);
            }
        }

        public CinemachineBlendDefinition.Styles BlendStyle
        {
            get => _brain != null ? _brain.DefaultBlend.Style : CinemachineBlendDefinition.Styles.EaseInOut;
            set
            {
                if (_brain == null) return;
                _brain.DefaultBlend.Style = value;
            }
        }

        public void Select(int index)
        {
            if (_shots.Count == 0) return;

            index = Mathf.Clamp(index, 0, _shots.Count - 1);
            if (index == _activeIndex) return;

            _activeIndex = index;
            ApplyPriorities();
            ActiveShotChanged?.Invoke();
        }

        /// <summary>
        /// 全ショットの角度を揃える。ショットごとに独立していると切り替えのたびにカメラが
        /// 被写体の周りを回り込み、「対象集合だけが変わった」という比較にならない。
        /// Brain より前に呼ぶ必要がある。
        /// </summary>
        public void ApplyOrbit(float horizontalAngle, float verticalAngle)
        {
            for (var i = 0; i < _shots.Count; i++)
            {
                var camera = _shots[i].Camera;
                if (camera == null) continue;

                var follow = camera.GetComponent<CinemachineOrbitalFollow>();
                if (follow == null) continue;

                follow.HorizontalAxis.Value = follow.HorizontalAxis.ClampValue(horizontalAngle);
                follow.VerticalAxis.Value = follow.VerticalAxis.ClampValue(verticalAngle);
            }
        }

        private void Awake()
        {
            _activeIndex = Mathf.Clamp(_initialIndex, 0, Mathf.Max(0, _shots.Count - 1));
            ApplyPriorities();
        }

        private void ApplyPriorities()
        {
            for (var i = 0; i < _shots.Count; i++)
            {
                var camera = _shots[i].Camera;
                if (camera == null) continue;

                camera.Priority = i == _activeIndex ? _activePriority : _inactivePriority;
            }
        }
    }
}
