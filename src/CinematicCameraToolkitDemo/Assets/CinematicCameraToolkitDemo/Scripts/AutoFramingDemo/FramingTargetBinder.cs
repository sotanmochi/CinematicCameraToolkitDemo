using System.Collections.Generic;
using CinematicCameraToolkit;
using UnityEngine;

namespace CinematicCameraToolkitDemo.AutoFraming
{
    /// <summary>
    /// フレーミングの入力（参照点と対象レンダラー）を外側から差し込む。
    ///
    /// 「何を基準に、何を画面に収めるか」はカメラの仕様ではなくシステムの都合なので、
    /// 決め方を変えたくなってもこのスクリプトと Inspector の配線だけで済むようにしてある。
    /// </summary>
    public sealed class FramingTargetBinder : MonoBehaviour
    {
        [SerializeField] private SmoothedFramingFollower _smoothedFramingFollower;
        [SerializeField] private Animator _animator;

        [Header("Reference Point")]
        [Tooltip("参照点にするヒューマノイドボーン。対象集合の重心付近を指すことを前提にしている")]
        [SerializeField] private HumanBodyBones _referenceBone = HumanBodyBones.Hips;

        [Header("Targets")]
        [Tooltip("フレーミング対象を集める起点。未設定なら Animator の GameObject を使う")]
        [SerializeField] private Transform _targetRoot;

        /// <summary>
        /// 代入しただけでは反映されない。<see cref="Apply"/> を呼ぶこと。
        /// </summary>
        public HumanBodyBones ReferenceBone
        {
            get => _referenceBone;
            set => _referenceBone = value;
        }

        private void Awake()
        {
            Apply();
        }

        public void Apply()
        {
            if (_smoothedFramingFollower == null) return;

            var reference = ResolveBone(_animator, _referenceBone, this);
            if (reference != null) _smoothedFramingFollower.ReferencePoint = reference;

            _smoothedFramingFollower.SetTargets(CollectTargets(ResolveTargetRoot(_targetRoot, _animator)));
        }

        /// <summary>
        /// 解決できなければ null。呼び出し側は既存の参照点を残したまま続行できる。
        /// </summary>
        public static Transform ResolveBone(Animator animator, HumanBodyBones bone, Object context)
        {
            if (animator == null)
            {
                Debug.LogError($"[{nameof(FramingTargetBinder)}] Animator が未設定です。", context);
                return null;
            }

            var transform = animator.GetBoneTransform(bone);
            if (transform == null)
            {
                Debug.LogError(
                    $"[{nameof(FramingTargetBinder)}] ボーン {bone} が Avatar にありません。", context);
            }

            return transform;
        }

        public static Transform ResolveTargetRoot(Transform targetRoot, Animator animator)
        {
            if (targetRoot != null) return targetRoot;
            return animator != null ? animator.transform : null;
        }

        /// <summary>
        /// MeshPointCollector が扱えるのは SkinnedMeshRenderer と MeshFilter 付き MeshRenderer だけなので、
        /// その 2 つに絞って集める。
        /// </summary>
        public static List<Renderer> CollectTargets(Transform root)
        {
            var result = new List<Renderer>();
            if (root == null) return result;

            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer is SkinnedMeshRenderer skinned)
                {
                    if (skinned.sharedMesh != null) result.Add(skinned);
                }
                else if (renderer is MeshRenderer meshRenderer)
                {
                    var filter = meshRenderer.GetComponent<MeshFilter>();
                    if (filter != null && filter.sharedMesh != null) result.Add(meshRenderer);
                }
            }

            return result;
        }
    }
}
