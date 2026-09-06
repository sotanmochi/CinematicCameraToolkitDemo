using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace CinematicCameraToolkitDemo.AutoFraming
{
    /// <summary>
    /// 操作パネルとカメラ操作でキーボードを取り合わないためのヘルパ。
    ///
    /// パネルのルートは picking-mode=Ignore なので、3D 側をクリックしても
    /// UI Toolkit はフォーカスを外してくれない。その部分だけを補う。
    /// </summary>
    public static class DemoUiFocus
    {
        /// <summary>
        /// スクリーン座標が操作可能な UI の上かを返す。
        ///
        /// RuntimePanelUtils.ScreenToPanel は Y を反転しない。入力側は左下原点、
        /// UI Toolkit のパネルは左上原点なので、渡す前に自分で反転させる必要がある。
        /// これを忘れると判定が上下逆の位置に出て、画面上側のパネルの上でドラッグが
        /// 素通りする（下側に別のパネルがある列だけ、たまたま当たって気づきにくい）。
        /// </summary>
        public static bool IsPointerOverUi(VisualElement root, Vector2 screenPosition)
        {
            var panel = root?.panel;
            if (panel == null) return false;

            var flipped = new Vector2(screenPosition.x, Screen.height - screenPosition.y);
            return panel.Pick(RuntimePanelUtils.ScreenToPanel(panel, flipped)) != null;
        }

        /// <summary>
        /// 矢印キーを自分で消費するコントロールにフォーカスがあるか。
        /// Button / Toggle は矢印を使わないので、押した後もカメラを回せる。
        /// </summary>
        public static bool ConsumesArrowKeys(VisualElement root)
        {
            var focused = root?.panel?.focusController?.focusedElement as VisualElement;
            for (var element = focused; element != null; element = element.parent)
            {
                if (element is Slider or SliderInt or TextField or DropdownField) return true;
            }

            return false;
        }

        /// <summary>
        /// UI の外を左クリックしたらフォーカスを外す。
        /// これが無いと、スライダーを一度触った後は矢印キーがカメラへ戻ってこない。
        /// </summary>
        public static void ReleaseFocusOnClickOutside(VisualElement root, Func<Vector2, bool> pointerOverUi)
        {
            var mouse = Mouse.current;
            if (mouse == null || !mouse.leftButton.wasPressedThisFrame) return;
            if (pointerOverUi != null && pointerOverUi(mouse.position.ReadValue())) return;

            root?.panel?.focusController?.focusedElement?.Blur();
        }
    }
}
