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
        public Color[] strokeValues;
        public Color defaultStroke;

        [NonSerialized] private Tweener _tweener;
        [NonSerialized] private IDisplayGear _lockedDisplay;
        [NonSerialized] private Graphic _graphic;
        [NonSerialized] private TextStroke _stroke;
        [NonSerialized] private bool _strokeResolved;

        private Graphic Graphic => _graphic ??= target.GetComponent<Graphic>();

        private void ApplyStroke(int index)
        {
            if (strokeValues == null)
                return;
            if (!_strokeResolved)
            {
                target.TryGetComponent(out _stroke);
                _strokeResolved = true;
            }
            if (_stroke != null)
                _stroke.color = index >= 0 ? strokeValues[index] : defaultStroke;
        }

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
            ApplyStroke(index);
            Graphic.color = index >= 0 ? values[index] : defaultValue;
        }

        public override void Apply(T page, bool animate)
        {
            KillTween();
            var index = Array.IndexOf(pages, page);
            var color = index >= 0 ? values[index] : defaultValue;
            ApplyStroke(index); // FairyGUI GearColor：描边色是离散态，主颜色可 tween
            var graphic = Graphic;
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
