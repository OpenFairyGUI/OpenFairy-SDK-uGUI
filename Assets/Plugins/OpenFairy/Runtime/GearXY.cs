using System;
using DG.Tweening;
using UnityEngine;

namespace OpenFairy.UGUI
{
    [Serializable]
    public class GearXY<T> : Gear<T> where T : struct, Enum
    {
        public Vector2[] values;
        public Vector2 defaultValue;

        [NonSerialized] private Vector2 _end;

        public override void Apply(T page) => Apply(page, false);

        public override void Apply(T page, bool animate)
        {
            var rt = (RectTransform)target.transform;
            var index = Array.IndexOf(pages, page);
            var end = index >= 0 ? values[index] : defaultValue;
            if (!animate || !tween) // 复刻 _constructing 门
            {
                KillTween();
                rt.anchoredPosition = end;
                return;
            }
            if (ContinueTween(end == _end) || rt.anchoredPosition == end)
                return;
            _end = end;
            // 用核心 DOTween.To 而非 rt.DOAnchorPos 扩展：后者定义在 DOTween 的松散 UI 模块 .cs（编进
            // Assembly-CSharp），Runtime 独立成程序集后就够不到了。核心 To 在 DOTween.dll 里，自动引用可用。
            TrackTween(DOTween.To(() => rt.anchoredPosition, v => rt.anchoredPosition = v, end, duration)
                .SetEase(ease).SetDelay(delay).SetLink(rt.gameObject, LinkBehaviour.KillOnDestroy));
        }
    }
}
