using UnityEngine;
using UnityEngine.UI;

namespace OpenFairy.UGUI
{
    // Persist measured dimensions when baking; use the native fitter for dynamic content in players.
    public sealed class AuthoredSizeFitter : ContentSizeFitter
    {
#if UNITY_EDITOR
        public override void SetLayoutHorizontal()
        {
            if (Application.isPlaying) base.SetLayoutHorizontal();
            else Fit(0, horizontalFit);
        }

        public override void SetLayoutVertical()
        {
            if (Application.isPlaying) base.SetLayoutVertical();
            else Fit(1, verticalFit);
        }

        private void Fit(int axis, FitMode mode)
        {
            if (mode == FitMode.Unconstrained) return;
            var rect = (RectTransform)transform;
            rect.SetSizeWithCurrentAnchors((RectTransform.Axis)axis, mode == FitMode.MinSize
                ? LayoutUtility.GetMinSize(rect, axis) : LayoutUtility.GetPreferredSize(rect, axis));
        }
#endif
    }
}
