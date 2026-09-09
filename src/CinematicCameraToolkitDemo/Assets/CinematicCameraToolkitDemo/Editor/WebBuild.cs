using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace CinematicCameraToolkitDemo.Editor
{
    /// <summary>
    /// DXT と ASTC の WebGL ビルドを作り、両方の .data をデスクトップ版に同梱する。
    /// iOS / Android は DXT、一般的なデスクトップは ASTC をネイティブ対応していないため、
    /// 単一形式ではどちらかがテクスチャを RGBA32 に展開し、このデモの 8K テクスチャでは
    /// メモリ不足によるブラウザクラッシュを招く。そこで実行環境が対応する形式で読み込まれるようにする。
    /// </summary>
    public static class WebBuild
    {
        private const string DesktopOutputPath = "Builds/Web/CinematicCameraToolkitDemo";
        private const string MobileOutputPath = "Builds/WebMobile/CinematicCameraToolkitDemo";

        private const string InjectionAnchor =
            "document.querySelector(\"#unity-loading-bar\").style.display = \"block\";";

        private const string InjectionIndent = "      ";

        [MenuItem("CinematicCameraToolkit/Demo/Build Web (DXT + ASTC)")]
        public static void BuildAll()
        {
            var scenes = EditorBuildSettings.scenes
                .Where(scene => scene.enabled)
                .Select(scene => scene.path)
                .ToArray();

            if (scenes.Length == 0)
            {
                throw new InvalidOperationException("Build Settings に有効なシーンがありません。");
            }

            BuildOne(scenes, DesktopOutputPath, WebGLTextureSubtarget.DXT);
            BuildOne(scenes, MobileOutputPath, WebGLTextureSubtarget.ASTC);

            var mobileDataFileName = CopyMobileData();
            InjectDataUrlSwitch(mobileDataFileName);

            Debug.Log($"[WebBuild] 完了: {Path.GetFullPath(DesktopOutputPath)} ({mobileDataFileName} を同梱)");
        }

        private static void BuildOne(string[] scenes, string locationPathName, WebGLTextureSubtarget subtarget)
        {
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                target = BuildTarget.WebGL,
                locationPathName = locationPathName,
                subtarget = (int)subtarget,
            });

            if (report.summary.result != BuildResult.Succeeded)
            {
                throw new InvalidOperationException($"{subtarget} 版のビルドに失敗しました: {report.summary.result}");
            }
        }

        private static string CopyMobileData()
        {
            var source = FindDataFile(MobileOutputPath);

            var fileName = Path.GetFileName(source);
            var mobileFileName = fileName.Insert(fileName.IndexOf(".data", StringComparison.Ordinal), ".mobile");

            File.Copy(source, Path.Combine(DesktopOutputPath, "Build", mobileFileName), overwrite: true);

            return mobileFileName;
        }

        private static string FindDataFile(string outputPath)
        {
            var buildDirectory = Path.Combine(outputPath, "Build");
            var candidates = Directory.GetFiles(buildDirectory, "*.data*");

            if (candidates.Length != 1)
            {
                throw new InvalidOperationException(
                    $"{buildDirectory} の .data ファイルが 1 つに定まりません（{candidates.Length} 個）。");
            }

            return candidates[0];
        }

        private static void InjectDataUrlSwitch(string mobileDataFileName)
        {
            var indexPath = Path.Combine(DesktopOutputPath, "index.html");
            var html = File.ReadAllText(indexPath);

            var anchorIndex = html.IndexOf(InjectionAnchor, StringComparison.Ordinal);
            if (anchorIndex < 0 ||
                html.IndexOf(InjectionAnchor, anchorIndex + InjectionAnchor.Length, StringComparison.Ordinal) >= 0)
            {
                throw new InvalidOperationException(
                    "index.html の差し込み位置が一意ではありません。" +
                    $"{nameof(WebBuild)}.{nameof(InjectionAnchor)} を見直してください。");
            }

            // 1 行目は既存の行頭インデントに乗るので、行末側にだけインデントを足していく。
            var snippet = string.Concat(
                Line("// iOS / Android は DXT、一般的なデスクトップは ASTC をネイティブ対応していない。"),
                Line("// 未対応形式は RGBA32 に展開され、このデモの 8K テクスチャではメモリ不足によるクラッシュを招く。"),
                Line("// それを避けるため、実行環境が ASTC 対応なら ASTC 版 .data へ切り替える。"),
                Line("(function () {"),
                Line("  var canvas = document.createElement(\"canvas\");"),
                Line("  var gl = canvas.getContext(\"webgl2\") || canvas.getContext(\"webgl\");"),
                Line("  if (gl && gl.getExtension(\"WEBGL_compressed_texture_astc\")) {"),
                Line($"    config.dataUrl = buildUrl + \"/{mobileDataFileName}\";"),
                Line("  }"),
                Line("})();"),
                Line(string.Empty));

            File.WriteAllText(indexPath, html.Replace(InjectionAnchor, snippet + InjectionAnchor));
        }

        private static string Line(string text) => text + "\n" + InjectionIndent;
    }
}
