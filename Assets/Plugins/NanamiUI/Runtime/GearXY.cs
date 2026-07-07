using System;
using DG.Tweening;
using UnityEngine;

namespace NanamiUI
{
    [Serializable]
    public class GearXY<T> : Gear<T> where T : struct, Enum
    {
        public Vector2[] values;
        public Vector2 defaultValue;

        [NonSerialized] private Tweener _tweener;

        private Vector2 End(T page)
        {
            var index = Array.IndexOf(pages, page);
            return index >= 0 ? values[index] : defaultValue;
        }

        public override void Apply(T page)
        {
            _tweener?.Kill();
            ((RectTransform)target.transform).anchoredPosition = End(page);
        }

        public override void Apply(T page, bool animate)
        {
            var rt = (RectTransform)target.transform;
            var end = End(page);
            _tweener?.Kill(); // 复刻 GearXY.cs:91-97 的杀旧重启
            if (!animate || !tween || rt.anchoredPosition == end) // 复刻 _constructing 门 + endPos!=origin
            {
                rt.anchoredPosition = end;
                return;
            }
            // 用核心 DOTween.To 而非 rt.DOAnchorPos 扩展：后者定义在 DOTween 的松散 UI 模块 .cs（编进
            // Assembly-CSharp），Runtime 独立成程序集后就够不到了。核心 To 在 DOTween.dll 里，自动引用可用。
            _tweener = DOTween.To(() => rt.anchoredPosition, v => rt.anchoredPosition = v, end, duration)
                .SetEase(ease).SetDelay(delay).OnComplete(() => _tweener = null);
        }
    }
}
