using System;
using System.IO;
using UnityEngine;

namespace CinematicCameraToolkitDemo.Navigation
{
    /// <summary>
    /// URL でデモを直接指定する仕組み。WebGL 公開時に
    /// <c>https://.../?scene=AutoFramingTimelineDemo</c> のような形でリンクできるようにする。
    ///
    /// 値はシーン名（拡張子なし、大文字小文字は不問）。実際に読むパスはカタログ ( <see cref="DemoCatalog"/> ) から引くので、
    /// カタログに無いシーンや Build Settings に入っていないシーンへは飛ばない。
    /// 
    /// WebGL 以外では <see cref="Application.absoluteURL"/> が空なので、何もしない。
    /// </summary>
    public static class DemoDeepLink
    {
        public const string SceneParameter = "scene";

        public static bool TryResolveScenePath(DemoCatalog catalog, out string scenePath) =>
            TryResolveScenePath(Application.absoluteURL, catalog, out scenePath);

        /// <summary>
        /// URL を明示する版。テストと、実機以外から呼びたいとき用。
        /// </summary>
        public static bool TryResolveScenePath(string url, DemoCatalog catalog, out string scenePath)
        {
            scenePath = null;

            var requested = ReadParameter(url, SceneParameter);
            if (string.IsNullOrEmpty(requested)) return false;

            if (catalog != null)
            {
                var entries = catalog.Entries;
                for (var i = 0; i < entries.Count; i++)
                {
                    if (!SceneNameMatches(entries[i].ScenePath, requested)) continue;

                    scenePath = entries[i].ScenePath;
                    return true;
                }
            }

            Debug.LogWarning($"URL で指定されたデモ '{requested}' が台帳に見つかりません。メニューを表示します。");
            return false;
        }

        /// <summary>
        /// クエリ文字列から 1 つのパラメータを取り出す。
        /// Uri を通さないのは、WebGL 実機で来る URL の細かな不正で例外を出したくないため。
        /// </summary>
        public static string ReadParameter(string url, string key)
        {
            if (string.IsNullOrEmpty(url)) return null;

            var start = url.IndexOf('?');
            if (start < 0) return null;

            var fragment = url.IndexOf('#', start);
            var query = fragment < 0 ? url.Substring(start + 1) : url.Substring(start + 1, fragment - start - 1);

            foreach (var pair in query.Split('&'))
            {
                var separator = pair.IndexOf('=');
                if (separator < 0) continue;
                if (!string.Equals(pair.Substring(0, separator), key, StringComparison.OrdinalIgnoreCase)) continue;

                var value = Unescape(pair.Substring(separator + 1));
                return string.IsNullOrEmpty(value) ? null : value;
            }

            return null;
        }

        public static bool SceneNameMatches(string scenePath, string sceneName)
        {
            if (string.IsNullOrEmpty(scenePath)) return false;

            return string.Equals(Path.GetFileNameWithoutExtension(scenePath), sceneName, StringComparison.OrdinalIgnoreCase);
        }

        private static string Unescape(string value)
        {
            try
            {
                return Uri.UnescapeDataString(value);
            }
            catch (UriFormatException)
            {
                return value;
            }
        }
    }
}
