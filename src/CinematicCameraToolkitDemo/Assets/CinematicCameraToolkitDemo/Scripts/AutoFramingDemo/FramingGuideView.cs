using CinematicCameraToolkit;
using UnityEngine.UIElements;

namespace CinematicCameraToolkitDemo.AutoFraming
{
    /// <summary>
    /// マージンガイドの枠と、各辺に重ねる現在値。3 つのデモで同じ見せ方をするためここへ集約する。
    /// UXML 側の名前は framing-guide と guide-{left,right,top,bottom}。
    ///
    /// RenderTargetMargin は描画先に対する百分率なので、UI Toolkit の left/right/top/bottom へ
    /// 変換を挟まずそのまま渡せる。数値表示にも単位を付けて、% であることを明示する。
    /// </summary>
    public sealed class FramingGuideView
    {
        private readonly VisualElement _guide;
        private readonly Label _left;
        private readonly Label _right;
        private readonly Label _top;
        private readonly Label _bottom;

        private FramingGuideView(VisualElement root, VisualElement guide)
        {
            _guide = guide;
            _left = root.Q<Label>("guide-left");
            _right = root.Q<Label>("guide-right");
            _top = root.Q<Label>("guide-top");
            _bottom = root.Q<Label>("guide-bottom");
        }

        /// <summary>
        /// ガイドがまだ見つからなければ null。呼び出し側は束縛未了として扱う。
        /// </summary>
        public static FramingGuideView TryCreate(VisualElement root)
        {
            var guide = root?.Q<VisualElement>("framing-guide");
            return guide != null ? new FramingGuideView(root, guide) : null;
        }

        public void Hide()
        {
            _guide.style.display = DisplayStyle.None;
        }

        public void Show(in RenderTargetMargin margin)
        {
            _guide.style.display = DisplayStyle.Flex;
            _guide.style.left = Length.Percent(margin.Left);
            _guide.style.right = Length.Percent(margin.Right);
            _guide.style.top = Length.Percent(margin.Top);
            _guide.style.bottom = Length.Percent(margin.Bottom);

            SetEdgeText(_left, "L", margin.Left);
            SetEdgeText(_right, "R", margin.Right);
            SetEdgeText(_top, "T", margin.Top);
            SetEdgeText(_bottom, "B", margin.Bottom);
        }

        private static void SetEdgeText(Label label, string edge, float percentage)
        {
            if (label != null) label.text = $"{edge} {percentage:F1}%";
        }
    }
}
