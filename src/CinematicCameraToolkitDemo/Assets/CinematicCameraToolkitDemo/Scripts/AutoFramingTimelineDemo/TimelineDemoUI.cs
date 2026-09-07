using CinematicCameraToolkit.Cinemachine;
using CinematicCameraToolkitDemo.AutoFraming;
using UnityEngine;
using UnityEngine.UIElements;

namespace CinematicCameraToolkitDemo.AutoFramingTimeline
{
    /// <summary>
    /// Timeline 版デモの画面表示。
    ///
    /// 再生そのものは Timeline に任せきりなので、ここが持つのは
    /// 「最初から流し直す」「マージンを枠として見せる」の 2 つだけ。
    /// メニューへ戻る・UI を隠すは全デモ共通なので DemoNavigationUI が持つ。
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class TimelineDemoUI : MonoBehaviour
    {
        [SerializeField] private TimelineDemoDirector _director;

        private UIDocument _document;
        private VisualElement _root;
        private bool _bound;

        private FramingGuideView _guide;

        private void Awake()
        {
            _document = GetComponent<UIDocument>();
        }

        private void OnDisable()
        {
            _bound = false;
            _guide = null;
        }

        private void Update()
        {
            if (!EnsureBound()) return;

            UpdateGuide(_director.ActiveFraming);
        }

        private bool EnsureBound()
        {
            if (_bound) return true;
            if (_director == null) return false;

            _root = _document != null ? _document.rootVisualElement : null;
            if (_root == null) return false;

            _guide = FramingGuideView.TryCreate(_root);

            if (_root.Q<Button>("restart-button") is { } restart) restart.clicked += _director.Restart;

            _bound = true;
            return true;
        }

        /// <summary>
        /// マージンのガイド枠を現在値へ合わせる。
        ///
        /// ブレンド中は実際の絵が 2 台のカメラの補間なので、枠は「いまライブな側」の値を示す。
        /// 枠が動くのはショット内でクリップが重なっている区間で、そこが Framing トラックの効果になる。
        /// </summary>
        private void UpdateGuide(CinemachineAutoFraming framing)
        {
            if (_guide == null) return;

            if (framing == null)
            {
                _guide.Hide();
                return;
            }

            _guide.Show(framing.CurrentMargin);
        }
    }
}
