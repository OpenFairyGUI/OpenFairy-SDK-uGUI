using System;
using DG.Tweening;
using UnityEngine;

namespace NanamiUI
{
    [Serializable]
    public class GearSize<T> : Gear<T> where T : struct, Enum
    {
        public Vector2[] sizes;
        public Vector2 defaultSize;
        public Vector2[] scales;
        public Vector2 defaultScale;

        [NonSerialized] private Tweener _tweener;

        private (Vector2 Size, Vector2 Scale) Values(T page)
        {
            var index = Array.IndexOf(pages, page);
            return (index >= 0 ? sizes[index] : defaultSize, index >= 0 ? scales[index] : defaultScale);
        }

        private static void SetSize(RectTransform rt, Vector2 size)
        {
            rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, size.x);
            rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, size.y);
        }

        public override void Apply(T page)
        {
            _tweener?.Kill();
            var rt = (RectTransform)target.transform;
            var (size, scale) = Values(page);
            SetSize(rt, size);
            rt.localScale = new Vector3(scale.x, scale.y, 1);
        }

        public override void Apply(T page, bool animate)
        {
            var rt = (RectTransform)target.transform;
            var (size, scale) = Values(page);
            _tweener?.Kill();
            var startSize = rt.rect.size;
            var startScale = new Vector2(rt.localScale.x, rt.localScale.y);
            if (!animate || !tween || (startSize == size && startScale == scale))
            {
                SetSize(rt, size);
                rt.localScale = new Vector3(scale.x, scale.y, 1);
                return;
            }
            _tweener = DOTween.To(() => 0f, t =>
            {
                SetSize(rt, Vector2.Lerp(startSize, size, t));
                var s = Vector2.Lerp(startScale, scale, t);
                rt.localScale = new Vector3(s.x, s.y, 1);
            }, 1f, duration).SetEase(ease).SetDelay(delay).OnComplete(() => _tweener = null);
        }
    }
}
