using System;
using System.Collections.Generic;
using CinematicCameraToolkit.Cinemachine;
using CinematicCameraToolkitDemo.AutoFraming;
using Unity.Cinemachine;
using UnityEngine;

namespace CinematicCameraToolkitDemo.AutoFramingCinemachine
{
    /// <summary>
    /// 各ショットのフレーミング入力を外側から差し込む。<see cref="FramingTargetBinder"/> の Cinemachine 版。
    ///
    /// AutoFraming では「寄り／引き」は距離ではなく「どのレンダラーを画面に収めるか」で決まるため、
    /// ショットごとの違いはこの対象集合に現れる。
    /// </summary>
    public sealed class CinemachineFramingTargetBinder : MonoBehaviour
    {
        [Serializable]
        public struct Binding
        {
            public CinemachineCamera Camera;

            /// <summary>
            /// 拡張は LookAt を計算の参照点にするため、対象集合から遠いボーンを指すと解が破綻する。
            /// 顔だけを収めるショットで腰を参照点にすると、カメラが頭の裏へ回り込む。
            /// </summary>
            public HumanBodyBones ReferenceBone;

            [Tooltip("画面に収めるレンダラー名。空なら全対象を使う")]
            public string[] IncludeRendererNames;
        }

        [SerializeField] private Animator _animator;

        [Tooltip("フレーミング対象を集める起点。未設定なら Animator の GameObject を使う")]
        [SerializeField] private Transform _targetRoot;

        [SerializeField] private Binding[] _bindings = Array.Empty<Binding>();

        /// <summary>
        /// 代入しただけでは反映されない。<see cref="Apply"/> を呼ぶこと。
        /// </summary>
        public Binding[] Bindings
        {
            get => _bindings;
            set => _bindings = value;
        }

        private void Awake()
        {
            Apply();
        }

        public void Apply()
        {
            var all = FramingTargetBinder.CollectTargets(
                FramingTargetBinder.ResolveTargetRoot(_targetRoot, _animator));

            foreach (var binding in _bindings)
            {
                if (binding.Camera == null) continue;

                var reference = FramingTargetBinder.ResolveBone(_animator, binding.ReferenceBone, this);
                if (reference != null)
                {
                    binding.Camera.LookAt = reference;
                    binding.Camera.Follow = reference;
                }

                var framing = binding.Camera.GetComponent<CinemachineAutoFraming>();
                if (framing != null) framing.SetTargets(Filter(all, binding.IncludeRendererNames));
            }
        }

        private static List<Renderer> Filter(List<Renderer> source, string[] includeNames)
        {
            if (includeNames == null || includeNames.Length == 0) return source;

            var result = new List<Renderer>(source.Count);
            foreach (var renderer in source)
            {
                if (Array.IndexOf(includeNames, renderer.name) >= 0) result.Add(renderer);
            }

            // 名前が 1 つも一致しないまま空リストを渡すと拡張が無効化され、
            // 「切り替えたら構図が固まる」という分かりにくい症状になる。全対象へ退避する。
            return result.Count > 0 ? result : source;
        }
    }
}
