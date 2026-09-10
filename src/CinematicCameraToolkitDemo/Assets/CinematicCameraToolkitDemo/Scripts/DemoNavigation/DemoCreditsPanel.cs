using UnityEngine;
using UnityEngine.UIElements;

namespace CinematicCameraToolkitDemo.Navigation
{
    /// <summary>
    /// デモが使っているサードパーティ素材の権利表記を出すオーバーレイ。
    ///
    /// 表記の中身はどのデモでも同じなので、共通ヘッダ（<see cref="DemoNavigationUI"/>）から開く。
    /// デモ側の UXML やシーンには手を入れず、ここでコードから組む。
    ///
    /// 文面はリポジトリ直下の LICENSE / THIRD-PARTY-NOTICES.txt と、
    /// Assets/CinematicCameraToolkitDemo/LICENSE.txt に合わせてある。どれかを直したらここも直すこと。
    /// </summary>
    public sealed class DemoCreditsPanel
    {
        private const string UnityChanLicenseUrl = "https://unity-chan.com/contents/license_en/";

        /// <summary>
        /// リポジトリ直下の LICENSE と同じ、コンテンツ全体にかかる表記。ロゴと並べて先頭に出す。
        /// </summary>
        private const string ContentLicense =
            "This contents is provided under the Unity-Chan License Terms.\n" +
            "このコンテンツはユニティちゃんライセンス条項の元に提供されています。";

        private static readonly (string Title, string Body, string Url)[] Credits =
        {
            (
                "UnityChan KAGURA",
                "UnityChan KAGURA © Unity Technologies Japan/UCL\n" +
                "Unity-Chan License Terms: " + UnityChanLicenseUrl,
                "https://github.com/unity3d-jp/UnityChanKAGURA"
            ),
            (
                "Unity Chan Spring Bone",
                "MIT License\n" +
                "Copyright (c) 2018 Unity Technologies",
                "https://github.com/unity3d-jp/UnityChanSpringBone/tree/release/1.1"
            ),
            (
                "Unity Toon Shader",
                "Unity Toon Shader copyright © 2021 Unity Technologies\n" +
                "Licensed under the Unity Companion License for Unity-dependent projects.",
                "http://www.unity3d.com/legal/licenses/Unity_Companion_License"
            ),
            (
                "CinematicCameraToolkit",
                "MIT License\n" +
                "Copyright (c) 2026 Soichiro Sugimoto",
                "https://github.com/sotanmochi/CinematicCameraToolkit"
            ),
        };

        private readonly VisualElement _root;
        private bool _open;

        public DemoCreditsPanel(VisualElement parent)
        {
            _root = Build();

            // 他のパネルより後に積んで、開いている間は必ず手前に出るようにする。
            parent.Add(_root);
        }

        public bool IsOpen => _open;

        public void Toggle() => SetOpen(!_open);

        public void Close() => SetOpen(false);

        private void SetOpen(bool open)
        {
            _open = open;
            _root.style.display = open ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private VisualElement Build()
        {
            var overlay = new VisualElement { name = "credits-overlay" };
            overlay.AddToClassList("credits-overlay");
            overlay.style.display = DisplayStyle.None;

            // 背景（パネルの外）を触ったら閉じる。開いている間はカメラ操作も塞ぐ。
            overlay.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.target == overlay) Close();
            });

            var panel = new VisualElement { name = "credits-panel" };
            panel.AddToClassList("panel");
            panel.AddToClassList("credits-panel");
            overlay.Add(panel);

            var title = new Label("Licenses / 権利表記");
            title.AddToClassList("panel-title");
            panel.Add(title);

            var list = new ScrollView { name = "credits-list" };
            list.AddToClassList("credits-list");
            panel.Add(list);

            // ユニティちゃんライセンスのロゴ。UCL 2.0 は作品内でのロゴ表示を求めている。
            var logo = new VisualElement { name = "ucl-logo" };
            logo.AddToClassList("ucl-logo");
            list.Add(logo);

            // ロゴのすぐ下に、コンテンツ全体にかかる表記。個別のクレジットより先に読ませる。
            var content = new VisualElement { name = "credits-content-license" };
            content.AddToClassList("credits-entry");
            var contentLicense = new Label(ContentLicense);
            contentLicense.AddToClassList("credits-entry__body");
            content.Add(contentLicense);
            content.Add(CreateLinkButton(UnityChanLicenseUrl));
            list.Add(content);

            foreach (var credit in Credits)
            {
                list.Add(CreateEntry(credit.Title, credit.Body, credit.Url));
            }

            panel.Add(new Button(Close) { name = "credits-close", text = "Close" });

            return overlay;
        }

        private static VisualElement CreateEntry(string title, string body, string url)
        {
            var entry = new VisualElement();
            entry.AddToClassList("credits-entry");

            var titleLabel = new Label(title);
            titleLabel.AddToClassList("credits-entry__title");
            entry.Add(titleLabel);

            var bodyLabel = new Label(body);
            bodyLabel.AddToClassList("credits-entry__body");
            entry.Add(bodyLabel);

            entry.Add(CreateLinkButton(url));

            return entry;
        }

        /// <summary>
        /// URL は表示もするしリンクにもする。WebGL では別タブで開く。
        /// </summary>
        private static Button CreateLinkButton(string url)
        {
            var button = new Button(() => Application.OpenURL(url)) { text = url };
            button.AddToClassList("credits-link");
            return button;
        }
    }
}
