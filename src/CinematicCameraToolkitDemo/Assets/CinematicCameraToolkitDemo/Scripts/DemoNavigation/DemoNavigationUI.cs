using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace CinematicCameraToolkitDemo.Navigation
{
    /// <summary>
    /// どのデモにも共通するヘッダ UI。デモ固有の操作パネルには手を出さず、その表示位置だけを決める。
    ///
    /// UXML を持たずコードでパネルを組むのは、デモが増えてもこのコンポーネントを
    /// 1 個置くだけで共通部分が揃うようにするため。
    /// </summary>
    public sealed class DemoNavigationUI : MonoBehaviour
    {
        /// <summary>
        /// 共通ヘッダの下（左列）に積みたいパネルに付けるクラス。位置はここが実測して入れる。
        /// </summary>
        private const string UnderNavigationClass = "panel--under-navigation";

        private const float NavigationGap = 12f;

        [Tooltip("タイトルの取得元。シーンパスが一致するエントリの表示名を使う")]
        [SerializeField] private DemoCatalog _catalog;

        [Tooltip("台帳に無いときのタイトル。空ならシーン名を出す")]
        [SerializeField] private string _titleOverride;

        [SerializeField] private string _menuScenePath = "Assets/CinematicCameraToolkitDemo/Scenes/DemoMenu.unity";

        [SerializeField] private Key _backKey = Key.Escape;
        [SerializeField] private Key _toggleVisibilityKey = Key.H;

        [Tooltip("ヘッダを差し込む UIDocument。未設定ならシーン内から探す")]
        [SerializeField] private UIDocument _document;

        private VisualElement _root;
        private VisualElement _navigationPanel;
        private bool _bound;
        private bool _uiHidden;

        /// <summary>
        /// 再配置が自分の起こしたレイアウト変更で再入するのを防ぐ。
        /// </summary>
        private bool _layouting;

        public void ReturnToMenu()
        {
            if (string.IsNullOrEmpty(_menuScenePath)) return;

            SceneManager.LoadScene(_menuScenePath);
        }

        /// <summary>
        /// UI 全体の表示を切り替える。収録用に画だけ残したいときのため。
        /// </summary>
        public void ToggleVisibility()
        {
            _uiHidden = !_uiHidden;
            _root.style.display = _uiHidden ? DisplayStyle.None : DisplayStyle.Flex;
        }

        private void OnDisable()
        {
            _bound = false;
        }

        private void Update()
        {
            if (!EnsureBound()) return;

            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            if (keyboard[_backKey].wasPressedThisFrame) ReturnToMenu();
            if (keyboard[_toggleVisibilityKey].wasPressedThisFrame) ToggleVisibility();
        }

        private bool EnsureBound()
        {
            if (_bound) return true;

            if (_document == null) _document = FindFirstObjectByType<UIDocument>();
            _root = _document != null ? _document.rootVisualElement : null;
            if (_root == null) return false;

            _navigationPanel = BuildNavigationPanel();

            // デモ側のパネルより先に積んで、常に左上に出るようにする。
            _root.Insert(0, _navigationPanel);

            // ヘッダもデモ側のパネルも高さが中身で変わるので、変わるたびに積み直す。
            _navigationPanel.RegisterCallback<GeometryChangedEvent>(_ => LayoutUnderNavigationPanels());
            foreach (var panel in _root.Query<VisualElement>(className: UnderNavigationClass).ToList())
            {
                panel.RegisterCallback<GeometryChangedEvent>(_ => LayoutUnderNavigationPanels());
            }

            _bound = true;
            return true;
        }

        private VisualElement BuildNavigationPanel()
        {
            var panel = new VisualElement { name = "navigation-panel" };
            panel.AddToClassList("panel");
            panel.AddToClassList("panel--navigation");

            var title = new Label(ResolveTitle()) { name = "navigation-title" };
            title.AddToClassList("panel-title");
            panel.Add(title);

            var menuButton = new Button(ReturnToMenu) { name = "menu-button", text = "← Menu" };
            panel.Add(menuButton);

            panel.Add(CreateKeyHint(_backKey, "メニューへ戻る"));
            panel.Add(CreateKeyHint(_toggleVisibilityKey, "UI の表示を切り替え"));

            return panel;
        }

        private static VisualElement CreateKeyHint(Key key, string description)
        {
            var row = new VisualElement();
            row.AddToClassList("key-hint");

            var keycap = new Label(key.ToString());
            keycap.AddToClassList("keycap");
            row.Add(keycap);

            var text = new Label(description);
            text.AddToClassList("key-hint__text");
            row.Add(text);

            return row;
        }

        private string ResolveTitle()
        {
            var scene = SceneManager.GetActiveScene();

            if (_catalog != null)
            {
                var entries = _catalog.Entries;
                for (var i = 0; i < entries.Count; i++)
                {
                    if (entries[i].ScenePath == scene.path && !string.IsNullOrEmpty(entries[i].Title))
                        return entries[i].Title;
                }
            }

            return string.IsNullOrEmpty(_titleOverride) ? scene.name : _titleOverride;
        }

        /// <summary>
        /// ヘッダの下に、UXML に書いた順でパネルを縦に積む。
        ///
        /// ヘッダの高さはタイトルの折り返しで、各パネルの高さは中身の出し入れで変わるので、
        /// USS の固定値ではなく毎回の実測で置く。
        /// </summary>
        private void LayoutUnderNavigationPanels()
        {
            if (_layouting) return;

            _layouting = true;
            try
            {
                var top = _navigationPanel.layout.yMax + NavigationGap;
                foreach (var panel in _root.Query<VisualElement>(className: UnderNavigationClass).ToList())
                {
                    panel.style.top = top;

                    var height = panel.layout.height;
                    if (float.IsNaN(height)) continue;

                    top += height + NavigationGap;
                }
            }
            finally
            {
                _layouting = false;
            }
        }
    }
}
