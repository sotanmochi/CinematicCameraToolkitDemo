using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace CinematicCameraToolkitDemo.Navigation
{
    /// <summary>
    /// 起動シーンのメニュー。<see cref="DemoCatalog"/>の並び順どおりにボタンを作るだけ。
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class DemoMenuUI : MonoBehaviour
    {
        [SerializeField] private DemoCatalog _catalog;

        private UIDocument _document;
        private bool _deepLinked;

        private void Awake()
        {
            _document = GetComponent<UIDocument>();

            // URL でデモを指定されていれば、メニューを組まずにそのまま入る。
            // UIDocument を止めるのは、遷移までの 1 フレームでメニューが映るのを防ぐため。
            if (!DemoDeepLink.TryResolveScenePath(_catalog, out var scenePath)) return;

            _document.enabled = false;
            _deepLinked = true;
            SceneManager.LoadScene(scenePath);
        }

        private void Start()
        {
            if (_deepLinked) return;

            var list = _document.rootVisualElement?.Q<VisualElement>("demo-list");
            if (list == null) return;

            list.Clear();

            var entries = _catalog != null ? _catalog.Entries : null;
            if (entries == null || entries.Count == 0)
            {
                list.Add(new Label("デモが登録されていません。DemoCatalog を確認してください。")
                {
                    name = "menu-empty",
                });
                return;
            }

            for (var i = 0; i < entries.Count; i++)
            {
                list.Add(CreateButton(entries[i]));
            }
        }

        private static Button CreateButton(DemoCatalog.Entry entry)
        {
            var scenePath = entry.ScenePath;
            var button = new Button(() => SceneManager.LoadScene(scenePath))
            {
                text = entry.Title,
            };
            button.AddToClassList("demo-button");
            return button;
        }
    }
}
