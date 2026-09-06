using CinematicCameraToolkit.Cinemachine;
using Unity.Animations.SpringBones;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Playables;

namespace CinematicCameraToolkitDemo.AutoFramingTimeline
{
    /// <summary>
    /// Timeline 版デモの進行役。
    ///
    /// Timeline がカメラの切り替え（CinemachineTrack）、アングル（AnimationTrack）、
    /// マージンとアラインメント（CinematicAutoFramingTrack）、キャラクターのアニメーションを持ち、
    /// 被写体との距離と構図は各 vcam の AutoFraming 拡張が毎フレーム解く。
    /// つまり「何をどう配置するかは Timeline、寄り引きは AutoFraming」という分担になる。
    ///
    /// このコンポーネントが担うのは更新順の固定と、頭出しだけ。
    ///
    /// 更新順:
    ///   PlayableDirector（PreLateUpdate でアニメーション適用）
    ///     → 本コンポーネント（実行順 -100 の LateUpdate で SpringBone を進める）
    ///     → CinemachineBrain（既定 0 の LateUpdate でカメラ姿勢を計算）
    ///
    /// SpringManager の自動更新も LateUpdate なので、そのままでは Brain との前後関係が
    /// 決まらない。ビルダー側で automaticUpdates を切り、ここから明示的に駆動する。
    /// </summary>
    [DefaultExecutionOrder(-100)]
    [RequireComponent(typeof(PlayableDirector))]
    public sealed class TimelineDemoDirector : MonoBehaviour
    {
        [SerializeField] private SpringManager _springManager;
        [SerializeField] private CinemachineBrain _brain;

        private PlayableDirector _director;

        /// <summary>
        /// いまライブな vcam の AutoFraming 拡張。Timeline の Framing トラックが
        /// 書き込んでいる値をそのまま読めるので、ガイド枠の表示はこれ 1 つで足りる。
        /// </summary>
        public CinemachineAutoFraming ActiveFraming
        {
            get
            {
                var active = _brain != null ? _brain.ActiveVirtualCamera as CinemachineVirtualCameraBase : null;
                return active != null ? active.GetComponent<CinemachineAutoFraming>() : null;
            }
        }

        public void Restart()
        {
            if (_director == null) return;

            _director.time = 0d;
            _director.Evaluate();
            _director.Play();
        }

        private void Awake()
        {
            _director = GetComponent<PlayableDirector>();

            // SpringBone はここから駆動する。自動更新のままだと Brain との順序が決まらない。
            if (_springManager != null) _springManager.automaticUpdates = false;
        }

        private void LateUpdate()
        {
            // Timeline のアニメーション適用は PreLateUpdate なので、この時点の姿勢は最新。
            // Brain（実行順 0）より前にここで揺らしておく。
            if (_springManager != null) _springManager.UpdateDynamics();
        }
    }
}
