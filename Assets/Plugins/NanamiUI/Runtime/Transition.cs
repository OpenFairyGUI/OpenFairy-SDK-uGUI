using System;
using System.Linq;
using DG.Tweening;
using DG.Tweening.Core.Easing;
using UnityEngine;
using UnityEngine.UI;

namespace NanamiUI
{
    public enum TransitionItemType
    {
        XY,
        Size,
        Scale,
        Pivot,
        Alpha,
        Rotation,
        Color,
        Animation,
        Visible,
        Sound,
        Nested,
        Shake,
        ColorFilter,
        Text,
    }

    [Serializable]
    public class TransitionItem
    {
        public float time;
        public RectTransform target;    // null = 组件根
        public TransitionItemType type;
        public bool tween;
        public float duration;
        public Ease ease = Ease.OutQuad;
        public int repeat;              // -1 无限，n = 额外重复 n 次
        public bool yoyo;
        public float[] start;           // NaN = 保持当前值
        public float[] end;
        public string stringValue;
        public AudioClip sound;
        public Vector2 positionOffset;  // FairyGUI xy → anchoredPosition 的固定偏移（pivot/anchor/拉伸锚点修正）
        public float[] pathData;

        [NonSerialized] public TransitionPath Path;
        [NonSerialized] public bool Applied;
        [NonSerialized] public bool Started;
        [NonSerialized] public float[] ResolvedStart;
        [NonSerialized] public float[] ResolvedEnd;
        [NonSerialized] public Vector2 ShakeBase;
    }

    // 复刻 FairyGUI Transition：时间轴上的一组 item，逐帧求值。缓动直接用 DOTween 的 EaseManager
    //（与 FairyGUI 内置的 Penner 方程同源，数值一致）。
    public class Transition : MonoBehaviour
    {
        public string transitionName;
        public bool autoPlay;
        public int autoPlayTimes = 1;
        public float autoPlayDelay;
        public TransitionItem[] items;

        private bool _playing;
        private bool _smoothStart;
        private float _time;
        private int _remainingTimes;
        private Action _onComplete;
        private float _totalDuration;
        private bool _hasInfinite;

        private void Start()
        {
            if (autoPlay)
                Play(autoPlayTimes, autoPlayDelay);
        }

        public void Play(int times = 1, float delay = 0, Action onComplete = null)
        {
            _playing = true;
            _smoothStart = true;
            _remainingTimes = times;
            _onComplete = onComplete;
            _time = -delay;
            _totalDuration = 0;
            _hasInfinite = false;
            foreach (var item in items)
            {
                item.Applied = false;
                item.Started = false;
                if (item.pathData is { Length: > 0 })
                    item.Path ??= new TransitionPath(item.pathData);
                var tail = item.type == TransitionItemType.Shake ? item.start[1]
                    : !item.tween ? 0
                    : item.repeat < 0 ? float.PositiveInfinity
                    : item.duration * (item.repeat + 1);
                if (float.IsPositiveInfinity(tail))
                    _hasInfinite = true;
                else
                    _totalDuration = Mathf.Max(_totalDuration, item.time + tail);
            }
            Step(0);
        }

        public void Stop() => _playing = false;

        private void Update()
        {
            if (!_playing)
                return;
            var deltaTime = Time.deltaTime;
            if (_smoothStart)
            {
                // 复刻 GTweener 的 smoothStart：起播第一帧钳制 dt，吸收前置构建造成的帧尖峰
                _smoothStart = false;
                deltaTime = Mathf.Clamp(Time.unscaledDeltaTime, 0, Application.targetFrameRate > 0 ? 1f / Application.targetFrameRate : 0.016f);
            }
            Step(deltaTime);
        }

        public void Step(float deltaTime)
        {
            _time += deltaTime;
            if (_time < 0)
                return;

            foreach (var item in items)
                Evaluate(item, _time - item.time);

            if (_hasInfinite || _time < _totalDuration)
                return;
            if (--_remainingTimes > 0)
            {
                _time = 0;
                foreach (var item in items)
                {
                    item.Applied = false;
                    item.Started = false;
                }
            }
            else
            {
                _playing = false;
                _onComplete?.Invoke();
            }
        }

        private void Evaluate(TransitionItem item, float local)
        {
            if (local < 0)
                return;

            if (item.type == TransitionItemType.Shake)
            {
                var rt = Target(item);
                if (!item.Started)
                {
                    item.Started = true;
                    item.ShakeBase = rt.anchoredPosition;
                }
                if (local < item.start[1])
                    rt.anchoredPosition = item.ShakeBase + UnityEngine.Random.insideUnitCircle * (item.start[0] * (1 - local / item.start[1]));
                else if (!item.Applied)
                {
                    item.Applied = true;
                    rt.anchoredPosition = item.ShakeBase;
                }
                return;
            }

            if (!item.tween)
            {
                if (item.Applied)
                    return;
                item.Applied = true;
                ApplyInstant(item);
                return;
            }

            if (!item.Started)
            {
                item.Started = true;
                item.ResolvedStart = Resolve(item, item.start);
                item.ResolvedEnd = Resolve(item, item.end);
            }

            float t;
            if (item.repeat != 0)
            {
                var cycle = Mathf.FloorToInt(local / item.duration);
                if (item.repeat > 0 && cycle > item.repeat)
                {
                    cycle = item.repeat;
                    t = item.duration;
                }
                else
                    t = local - cycle * item.duration;
                if (item.yoyo && (cycle & 1) == 1)
                    t = item.duration - t;
            }
            else
                t = Mathf.Min(local, item.duration);

            var ratio = EaseManager.Evaluate(item.ease, null, t, item.duration, 1.70158f, 0);
            if (item.Path != null)
            {
                var point = item.Path.GetPointAt(ratio);
                Target(item).anchoredPosition = new Vector2(item.ResolvedStart[0] + point.x, -(item.ResolvedStart[1] + point.y)) + item.positionOffset;
                return;
            }

            var value = new float[item.ResolvedStart.Length];
            for (var i = 0; i < value.Length; i++)
                value[i] = item.ResolvedStart[i] + (item.ResolvedEnd[i] - item.ResolvedStart[i]) * ratio;
            ApplyValue(item, value);
        }

        private void ApplyInstant(TransitionItem item)
        {
            var rt = Target(item);
            switch (item.type)
            {
                case TransitionItemType.Visible:
                    rt.gameObject.SetActive(item.start[0] != 0);
                    break;
                case TransitionItemType.Animation:
                {
                    var movieClip = rt.GetComponent<MovieClip>();
                    movieClip.playing = item.start[1] != 0;
                    if (item.start[0] >= 0)
                        movieClip.SetFrame((int)item.start[0]);
                    break;
                }
                case TransitionItemType.Sound:
                    if (item.sound != null)
                        AudioSource.PlayClipAtPoint(item.sound, Vector3.zero);
                    break;
                case TransitionItemType.Nested:
                    GetComponents<Transition>().First(transition => transition.transitionName == item.stringValue).Play();
                    break;
                case TransitionItemType.Pivot:
                {
                    // 保持 rect 不动地改 pivot
                    var pivot = new Vector2(item.start[0], 1 - item.start[1]);
                    var delta = pivot - rt.pivot;
                    rt.pivot = pivot;
                    rt.anchoredPosition += new Vector2(delta.x * rt.rect.width, delta.y * rt.rect.height);
                    break;
                }
                case TransitionItemType.Text:
                    rt.GetComponent<Text>().text = item.stringValue;
                    break;
                default:
                    ApplyValue(item, Resolve(item, item.start));
                    break;
            }
        }

        private void ApplyValue(TransitionItem item, float[] value)
        {
            var rt = Target(item);
            switch (item.type)
            {
                case TransitionItemType.XY:
                    rt.anchoredPosition = new Vector2(value[0], -value[1]) + item.positionOffset;
                    break;
                case TransitionItemType.Size:
                    rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, value[0]);
                    rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, value[1]);
                    break;
                case TransitionItemType.Scale:
                    rt.localScale = new Vector3(value[0], value[1], 1);
                    break;
                case TransitionItemType.Alpha:
                {
                    var group = rt.GetComponent<CanvasGroup>();
                    if (group == null)
                        group = rt.gameObject.AddComponent<CanvasGroup>();
                    group.alpha = value[0];
                    break;
                }
                case TransitionItemType.Rotation:
                    rt.localEulerAngles = new Vector3(0, 0, -value[0]);
                    break;
                case TransitionItemType.Color:
                    rt.GetComponent<Graphic>().color = new Color(value[0], value[1], value[2], value.Length > 3 ? value[3] : 1);
                    break;
                case TransitionItemType.ColorFilter:
                {
                    var adjust = rt.GetComponent<ColorAdjust>();
                    if (adjust == null)
                        adjust = rt.gameObject.AddComponent<ColorAdjust>();
                    adjust.Set(value[0], value[1], value[2], value[3]);
                    break;
                }
            }
        }

        private float[] Resolve(TransitionItem item, float[] values)
        {
            var result = (float[])values.Clone();
            for (var i = 0; i < result.Length; i++)
                if (float.IsNaN(result[i]))
                    result[i] = Current(item, i);
            return result;
        }

        private float Current(TransitionItem item, int index)
        {
            var rt = Target(item);
            return item.type switch
            {
                TransitionItemType.XY => index == 0
                    ? rt.anchoredPosition.x - item.positionOffset.x
                    : -(rt.anchoredPosition.y - item.positionOffset.y),
                TransitionItemType.Size => index == 0 ? rt.rect.width : rt.rect.height,
                TransitionItemType.Scale => index == 0 ? rt.localScale.x : rt.localScale.y,
                TransitionItemType.Alpha => rt.GetComponent<CanvasGroup>() is { } group ? group.alpha : 1,
                TransitionItemType.Rotation => -rt.localEulerAngles.z,
                _ => 0,
            };
        }

        private RectTransform Target(TransitionItem item) =>
            item.target != null ? item.target : (RectTransform)transform;
    }
}
