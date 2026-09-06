using System;
using System.Collections.Generic;
using UnityEngine;

namespace CinematicCameraToolkitDemo.Navigation
{
    [CreateAssetMenu(
        fileName = "DemoCatalog",
        menuName = "Cinematic Camera Toolkit Demo/Demo Catalog")]
    public sealed class DemoCatalog : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            [Tooltip("ボタンに出す名前")]
            public string Title;

            [Tooltip("読み込むシーンのアセットパス。Build Settings に入っている必要がある")]
            public string ScenePath;
        }

        [SerializeField] private Entry[] _entries = Array.Empty<Entry>();

        public IReadOnlyList<Entry> Entries => _entries;
    }
}
