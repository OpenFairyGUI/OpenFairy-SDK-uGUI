using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace NanamiUI
{
    public enum ProgressTitleType
    {
        Percent,
        ValueAndMax,
        Value,
        Max,
    }

    // ProgressBar 与 Slider 共用的标题格式化（复刻 FairyGUI GProgressBar/GSlider 的 titleType 分支）。
    public static class ProgressTitle
    {
        public static string Format(ProgressTitleType type, float value, float min, float max)
        {
            var percent = Mathf.Clamp01((value - min) / (max - min));
            return type switch
            {
                ProgressTitleType.Percent => Mathf.FloorToInt(percent * 100) + "%",
                ProgressTitleType.ValueAndMax => Mathf.Round(value) + "/" + Mathf.Round(max),
                ProgressTitleType.Value => "" + Mathf.Round(value),
                _ => "" + Mathf.Round(max),
            };
        }
    }

    public class ProgressBar : Component
    {
        public float value = 50;
        public float max = 100;
        public float min;
        public ProgressTitleType titleType;
        public bool reverse;
        public TextField title;
        public RectTransform bar;
        public RectTransform barV;
        public MovieClip ani;
        public float barMaxWidthDelta;
        public float barMaxHeightDelta;
        public float barStartX;
        public float barStartY;

        [System.NonSerialized] private Tweener _tweener;

        // 复刻 FairyGUI GProgressBar.TweenValue：从当前值平滑过渡到目标值。
        public void TweenValue(float target, float duration, Ease ease = Ease.Linear)
        {
            _tweener?.Kill();
            var start = value;
            _tweener = DOTween.To(() => 0f, t =>
            {
                value = Mathf.Lerp(start, target, t);
                Apply();
            }, 1f, duration).SetEase(ease).SetLink(gameObject).OnComplete(() => _tweener = null);
        }

        public void Apply()
        {
            var percent = Mathf.Clamp01((value - min) / (max - min));
            if (title != null)
                title.text = ProgressTitle.Format(titleType, value, min, max);

            var rect = ((RectTransform)transform).rect;
            if (bar != null && !SetFillAmount(bar, percent))
            {
                var fullWidth = rect.width - barMaxWidthDelta;
                var w = Mathf.RoundToInt(fullWidth * percent);
                bar.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, w);
                if (reverse)
                    bar.anchoredPosition = new Vector2(barStartX + (fullWidth - w), bar.anchoredPosition.y);
            }
            if (barV != null && !SetFillAmount(barV, percent))
            {
                var fullHeight = rect.height - barMaxHeightDelta;
                var h = Mathf.RoundToInt(fullHeight * percent);
                barV.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, h);
                if (reverse)
                    barV.anchoredPosition = new Vector2(barV.anchoredPosition.x, -(barStartY + (fullHeight - h)));
            }
            if (ani != null)
                ani.SetFrame(Mathf.RoundToInt(percent * 100));
        }

        private bool SetFillAmount(RectTransform barRt, float percent)
        {
            var image = barRt.GetComponent<UnityEngine.UI.Image>();
            if (image == null || image.type != UnityEngine.UI.Image.Type.Filled)
                return false;
            image.fillAmount = reverse ? 1 - percent : percent;
            return true;
        }
    }
}
