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
        [NonSerialized] private IDisplayGear _lockedDisplay;

        // 杀 tween 并释放本 gear 持有的 display lock（DOTween.Kill 不触发 OnComplete，故显式释放，避免锁泄漏）。
        private void KillTween()
        {
            _tweener?.Kill();
            _tweener = null;
            if (_lockedDisplay != null)
            {
                _lockedDisplay.ReleaseLock();
                _lockedDisplay = null;
            }
        }

        public override void Apply(T page)
        {
            KillTween();
            var index = Array.IndexOf(pages, page);
            target.GetComponent<Graphic>().color = index >= 0 ? values[index] : defaultValue;
        }

        public override void Apply(T page, bool animate)
        {
            KillTween();
            var index = Array.IndexOf(pages, page);
            var color = index >= 0 ? values[index] : defaultValue;
            var graphic = target.GetComponent<Graphic>();
            if (!animate || !tween || graphic.color == color)
            {
                graphic.color = color;
                return;
            }
            if (displayLock != null) // 有同 target 的 GearDisplay：tween 期间保持显示（复刻 GearColor AddDisplayLock）
            {
                displayLock.AddLock();
                _lockedDisplay = displayLock;
            }
            var start = graphic.color;
            _tweener = DOTween.To(() => 0f, t => graphic.color = Color.Lerp(start, color, t), 1f, duration)
                .SetEase(ease).SetDelay(delay).SetLink(graphic.gameObject, LinkBehaviour.KillOnDestroy).OnComplete(() =>
                {
                    _tweener = null;
                    if (_lockedDisplay != null)
                    {
                        _lockedDisplay.ReleaseLock();
                        _lockedDisplay = null;
                    }
                });
        }
    }
}
