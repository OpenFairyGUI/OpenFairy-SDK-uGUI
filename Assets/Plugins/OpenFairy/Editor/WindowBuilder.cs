using UnityEngine;

namespace OpenFairy.UGUI.Editor
{
    public static class WindowBuilder
    {
        public static void Configure(Window window, Transition show, Transition hide, ButtonBase close = null, RectTransform dragArea = null)
        {
            window.showTransition = show;
            window.hideTransition = hide;
            window.closeButton = close;
            window.canvasGroup = window.GetComponent<CanvasGroup>();
            if (dragArea == null) return;
            if (!dragArea.TryGetComponent<WindowDragArea>(out var drag)) drag = dragArea.gameObject.AddComponent<WindowDragArea>();
            drag.window = window;
            if (!dragArea.TryGetComponent<UnityEngine.UI.Graphic>(out _))
            {
                var image = dragArea.gameObject.AddComponent<UnityEngine.UI.Image>();
                image.color = Color.clear;
            }
        }
    }
}
