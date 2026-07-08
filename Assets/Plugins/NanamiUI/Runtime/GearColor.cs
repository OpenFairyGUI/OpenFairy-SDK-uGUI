using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace NanamiUI
{
    [Serializable]
    public class GearColor<T> : Gear<T> where T : struct, Enum
    {
        public Color[] values;
        public Color defaultValue;

        [NonSerialized] private Tweener _tweener;

        public override void Apply(T page)
        {
            _tweener?.Kill();
            var index = Array.IndexOf(pages, page);
            target.GetComponent<Graphic>().color = index >= 0 ? values[index] : defaultValue;
        }

        public override void Apply(T page, bool animate)
        {
            _tweener?.Kill();
            var index = Array.IndexOf(pages, page);
            var color = index >= 0 ? values[index] : defaultValue;
            var graphic = target.GetComponent<Graphic>();
            if (!animate || !tween || graphic.color == color)
            {
                graphic.color = color;
                return;
            }
            var start = graphic.color;
            _tweener = DOTween.To(() => 0f, t => graphic.color = Color.Lerp(start, color, t), 1f, duration)
                .SetEase(ease).SetDelay(delay).SetLink(graphic.gameObject, LinkBehaviour.KillOnDestroy).OnComplete(() => _tweener = null);
        }
    }
}
