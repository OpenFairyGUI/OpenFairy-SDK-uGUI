using DG.Tweening;
using UnityEngine;
using UnityEngine.Serialization;
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
    // 显式 ToString：int+string 的 Concat 会装箱，TweenValue 期间每帧调用。
    internal static class ProgressTitle
    {
        public static string Format(ProgressTitleType type, float value, float min, float max)
        {
            var percent = Mathf.Clamp01((value - min) / (max - min));
            return type switch
            {
                ProgressTitleType.Percent => Mathf.FloorToInt(percent * 100).ToString() + "%",
                ProgressTitleType.ValueAndMax => Mathf.RoundToInt(value).ToString() + "/" + Mathf.RoundToInt(max).ToString(),
                ProgressTitleType.Value => Mathf.RoundToInt(value).ToString(),
                _ => Mathf.RoundToInt(max).ToString(),
            };
        }
    }

    public class ProgressBar : Component
    {
        [SerializeField, FormerlySerializedAs("value")] private float _value = 50;
        [SerializeField, FormerlySerializedAs("max")] private float _max = 100;
        [SerializeField, FormerlySerializedAs("min")] private float _min;
        public ProgressTitleType titleType;
        public bool reverse;

        // 烘焙接线（Migrate 写入）。
        [SerializeField] internal TextField title;
        [SerializeField] internal RectTransform bar;
        [SerializeField] internal RectTransform barV;
        [SerializeField] internal MovieClip ani;
        [SerializeField] internal float barMaxWidthDelta;
        [SerializeField] internal float barMaxHeightDelta;
        [SerializeField] internal float barStartX;
        [SerializeField] internal float barStartY;

        [System.NonSerialized] private Tweener _tweener;
        [System.NonSerialized] private UnityEngine.UI.Image _barImage;
        [System.NonSerialized] private UnityEngine.UI.Image _barVImage;

        public float value
        {
            get => _value;
            set
            {
                if (_value == value)
                    return;
                _value = value;
                Apply();
            }
        }

        public float max
        {
            get => _max;
            set
            {
                if (_max == value)
                    return;
                _max = value;
                Apply();
            }
        }

        public float min
        {
            get => _min;
            set
            {
                if (_min == value)
                    return;
                _min = value;
                Apply();
            }
        }

        // 复刻 FairyGUI GProgressBar.TweenValue：从当前值平滑过渡到目标值。
        public void TweenValue(float target, float duration, Ease ease = Ease.Linear)
        {
            _tweener?.Kill();
            var start = value;
            _tweener = DOTween.To(() => 0f, t =>
            {
                value = Mathf.Lerp(start, target, t);
            }, 1f, duration).SetEase(ease).SetLink(gameObject).OnComplete(() => _tweener = null);
        }

        internal void Apply()
        {
            var percent = Mathf.Clamp01((value - min) / (max - min));
            if (title != null)
                title.text = ProgressTitle.Format(titleType, value, min, max);

            var rect = ((RectTransform)transform).rect;
            if (bar != null && !SetFillAmount(bar, ref _barImage, percent))
            {
                var fullWidth = rect.width - barMaxWidthDelta;
                var w = Mathf.RoundToInt(fullWidth * percent);
                bar.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, w);
                if (reverse)
                    bar.anchoredPosition = new Vector2(barStartX + (fullWidth - w), bar.anchoredPosition.y);
            }
            if (barV != null && !SetFillAmount(barV, ref _barVImage, percent))
            {
                var fullHeight = rect.height - barMaxHeightDelta;
                var h = Mathf.RoundToInt(fullHeight * percent);
                barV.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, h);
                if (reverse)
                    barV.anchoredPosition = new Vector2(barV.anchoredPosition.x, -(barStartY + (fullHeight - h)));
            }
            if (ani != null)
                ani.frame = Mathf.RoundToInt(percent * 100);
        }

        private bool SetFillAmount(RectTransform barRt, ref UnityEngine.UI.Image image, float percent)
        {
            image ??= barRt.GetComponent<UnityEngine.UI.Image>();
            if (image == null || image.type != UnityEngine.UI.Image.Type.Filled)
                return false;
            image.fillAmount = reverse ? 1 - percent : percent;
            return true;
        }
    }
}
