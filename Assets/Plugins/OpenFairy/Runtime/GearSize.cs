using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace OpenFairy.UGUI
{
    [Serializable]
    public class GearSize<T> : Gear<T> where T : struct, Enum
    {
        public Vector2[] sizes;
        public Vector2 defaultSize;
        public Vector2[] scales;
        public Vector2 defaultScale;

        [NonSerialized] private Vector4 _end;

        private static void SetSize(RectTransform rt, Vector2 size)
        {
            var fitter = rt.GetComponent<ContentSizeFitter>();
            if (fitter == null || fitter.horizontalFit == ContentSizeFitter.FitMode.Unconstrained)
                rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, size.x);
            if (fitter == null || fitter.verticalFit == ContentSizeFitter.FitMode.Unconstrained)
                rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, size.y);
        }

        private static void Set(RectTransform rt, Vector2 size, Vector2 scale)
        {
            SetSize(rt, size);
            rt.localScale = new Vector3(scale.x, scale.y, 1);
        }

        public override void Apply(T page) => Apply(page, false);

        public override void Apply(T page, bool animate)
        {
            var rt = (RectTransform)target.transform;
            var index = Array.IndexOf(pages, page);
            var size = index >= 0 ? sizes[index] : defaultSize;
            var scale = index >= 0 ? scales[index] : defaultScale;
            if (!animate || !tween)
            {
                KillTween();
                Set(rt, size, scale);
                return;
            }
            var end = new Vector4(size.x, size.y, scale.x, scale.y);
            if (ContinueTween(end == _end))
                return;
            var startSize = rt.rect.size;
            var startScale = (Vector2)rt.localScale;
            if (startSize == size && startScale == scale)
                return;
            _end = end;
            TrackTween(DOTween.To(() => 0f, t => Set(rt, Vector2.Lerp(startSize, size, t), Vector2.Lerp(startScale, scale, t)),
                1f, duration).SetEase(ease).SetDelay(delay).SetLink(rt.gameObject, LinkBehaviour.KillOnDestroy));
        }
    }
}
