using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace OpenFairy.UGUI
{
    [Serializable]
    public class GearColor<T> : Gear<T> where T : struct, Enum
    {
        public Color[] values;
        public Color defaultValue;
        public Color[] strokeValues;
        public Color defaultStroke;

        [NonSerialized] private Graphic _graphic;
        [NonSerialized] private Color _end;

        public override void Apply(T page) => Apply(page, false);

        public override void Apply(T page, bool animate)
        {
            var graphic = _graphic ??= target.GetComponent<Graphic>();
            var index = Array.IndexOf(pages, page);
            var color = index >= 0 ? values[index] : defaultValue;
            graphic.SetOutlineColor(index >= 0 ? strokeValues[index] : defaultStroke); // FairyGUI GearColor：描边色是离散态，主颜色可 tween
            if (!animate || !tween)
            {
                KillTween();
                graphic.color = color;
                return;
            }
            if (ContinueTween(color == _end) || graphic.color == color)
                return;
            _end = color;
            var start = graphic.color;
            TrackTween(DOTween.To(() => 0f, t => graphic.color = Color.Lerp(start, color, t), 1f, duration)
                .SetEase(ease).SetDelay(delay).SetLink(graphic.gameObject, LinkBehaviour.KillOnDestroy));
        }
    }
}
