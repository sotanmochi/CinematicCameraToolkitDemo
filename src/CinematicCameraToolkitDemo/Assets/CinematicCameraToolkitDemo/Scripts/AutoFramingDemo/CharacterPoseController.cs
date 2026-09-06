using Unity.Animations.SpringBones;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace CinematicCameraToolkitDemo.AutoFraming
{
    /// <summary>
    /// キャラクターのポーズを生成して適用する。
    ///
    /// AnimatorController の State Machine は使わず、PlayableGraph に AnimationClipPlayable を
    /// 直接ぶら下げる。DirectorUpdateMode.Manual にしているため、Animator の通常評価フェーズに
    /// 依存せず Evaluate() を呼んだ時点でポーズが確定する。
    ///
    /// 時間は常に自前で SetTime して Evaluate(0) するため、
    /// ポーズモード（離散時刻）とアニメーションモード（連続再生）が同一の再生機構になる。
    /// </summary>
    public sealed class CharacterPoseController : MonoBehaviour
    {
        [Header("Character")]
        [Tooltip("Avatar 付きの Animator。プレハブのルートにあるもの")]
        [SerializeField] private Animator _animator;
        [SerializeField] private SpringManager _springManager;

        /// <summary>
        /// 分割数の下限。1 だと常に t=0 の 1 ポーズきりになるため 2 から。
        /// </summary>
        public const int MinPoseCount = 2;

        /// <summary>
        /// 分割数の上限。これ以上刻んでも隣のポーズとの差が見えない。
        /// </summary>
        public const int MaxPoseCount = 60;

        [Header("Animation")]
        [SerializeField] private AnimationClip _clip;
        [Tooltip("クリップを等間隔サンプリングしてポーズとして使う数")]
        [SerializeField] [Range(MinPoseCount, MaxPoseCount)] private int _poseCount = 12;

        [Header("Spring Bone")]
        [Tooltip("FastForward の上限秒数")]
        [SerializeField] private float _warmupTime = 20f;

        private PlayableGraph _graph;
        private AnimationClipPlayable _clipPlayable;
        private bool _graphCreated;

        public double CurrentTime { get; private set; }
        public float PlaybackSpeed { get; set; } = 1f;
        public float ClipLength => _clip != null ? _clip.length : 0f;
        /// <summary>
        /// クリップの分割数。実行中に変えてよいが、表示中のポーズは張り替わらないので
        /// 呼び出し側（Director）が改めて <see cref="ApplyPose"/> する。
        /// </summary>
        public int PoseCount
        {
            get => Mathf.Clamp(_poseCount, MinPoseCount, MaxPoseCount);
            set => _poseCount = Mathf.Clamp(value, MinPoseCount, MaxPoseCount);
        }

        public int CurrentPoseIndex { get; private set; } = -1;
        public string CurrentPoseName { get; private set; } = string.Empty;

        private void Awake()
        {
            if (_animator == null)
            {
                Debug.LogError($"[{nameof(CharacterPoseController)}] Animator が未設定です。", this);
                return;
            }

            if (_clip == null)
            {
                Debug.LogError($"[{nameof(CharacterPoseController)}] AnimationClip が未設定です。", this);
                return;
            }

            // ポーズを与える経路を PlayableGraph 一本に確定させる。
            // プレハブには State が空の AnimatorController が刺さっており、
            // 実害は無いが二重に Animator を駆動しうるため外しておく。
            _animator.runtimeAnimatorController = null;

            CreateGraph();

            // 更新は Director が明示的に駆動する。Brain / Follower との順序を確定させるため。
            if (_springManager != null) _springManager.automaticUpdates = false;
        }

        private void OnDestroy()
        {
            if (_graphCreated && _graph.IsValid()) _graph.Destroy();
            _graphCreated = false;
        }

        private void CreateGraph()
        {
            _graph = PlayableGraph.Create($"{nameof(CharacterPoseController)}.{name}");

            // 時間進行は自前で行う。Animator の通常評価フェーズに依存させない。
            _graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);

            _clipPlayable = AnimationClipPlayable.Create(_graph, _clip);
            _clipPlayable.SetApplyFootIK(false);
            _clipPlayable.SetApplyPlayableIK(false);

            var output = AnimationPlayableOutput.Create(_graph, "Animation", _animator);
            output.SetSourcePlayable(_clipPlayable);

            _graph.Play();
            _graphCreated = true;

            SampleAt(0d);
        }

        public void SampleAt(double time)
        {
            if (!_graphCreated) return;

            CurrentTime = time;

            // 2 回 SetTime することで前フレーム時刻も揃え、
            // Playable 内部のデルタ計算による意図しない補間・アニメーションイベント発火を防ぐ。
            _clipPlayable.SetTime(time);
            _clipPlayable.SetTime(time);

            // 常に Evaluate(0) を使い、時間の唯一の源を SetTime に統一する。
            _graph.Evaluate(0f);
        }

        /// <summary>
        /// 再生を dt 秒進める。クリップ長でラップするので import 設定の loopTime に依存しない。
        /// </summary>
        public void Advance(float deltaTime)
        {
            if (!_graphCreated) return;

            var length = ClipLength;
            if (length <= 0f) return;

            var time = CurrentTime + deltaTime * PlaybackSpeed;
            time %= length;
            if (time < 0d) time += length;

            SampleAt(time);
        }

        /// <summary>
        /// ポーズ番号を適用し、SpringBone を収束させる。
        /// </summary>
        public void ApplyPose(int index)
        {
            if (!_graphCreated) return;

            var count = PoseCount;
            index = ((index % count) + count) % count;
            CurrentPoseIndex = index;

            var length = ClipLength;
            var time = count > 1 ? (double)index / (count - 1) * length : 0d;

            SampleAt(time);
            SettleSpringBones();

            CurrentPoseName = $"{index + 1}/{count}  ({time:F1}s)";
        }

        /// <summary>
        /// SpringBone をリセットして収束させる。ポーズ切替やモード遷移の直後に呼ぶ。
        /// FastForward は SpringManager の自動更新をスキップさせるフラグを立てるが、
        /// 本デモは手動駆動なので、スキップの扱いは呼び出し側（Director）が行う。
        /// </summary>
        public void SettleSpringBones()
        {
            if (_springManager == null) return;

            _springManager.ResetAllBones();
            _springManager.FastForward(_warmupTime, 0f);
        }

        /// <summary>
        /// 1 フレーム分だけ進める。収束させたいときは <see cref="SettleSpringBones"/>。
        /// </summary>
        public void UpdateSpringBones()
        {
            if (_springManager == null) return;
            _springManager.UpdateDynamics();
        }

    }
}
