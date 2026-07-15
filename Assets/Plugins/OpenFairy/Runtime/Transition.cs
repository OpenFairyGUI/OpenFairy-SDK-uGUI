using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using DG.Tweening.Core.Easing;
using UnityEngine;
using UnityEngine.UI;
using ZLinq;

namespace OpenFairy.UGUI
{
    internal readonly struct TransitionValue
    {
        private readonly Vector4 _value;
        public readonly int Count;

        public TransitionValue(Vector4 value, int count)
        {
            _value = value;
            Count = count;
        }

        public float this[int index] => _value[index];

        public static TransitionValue Lerp(TransitionValue start, TransitionValue end, float ratio) =>
            new(Vector4.LerpUnclamped(start._value, end._value, ratio), start.Count);
    }

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
        Skew,
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
        public int playTimes = 1;
        public AudioClip sound;
        public float volume = 1;
        public Vector2 positionOffset;  // FairyGUI xy → anchoredPosition 的固定偏移（pivot/anchor/拉伸锚点修正）
        public float[] pathData;

        [NonSerialized] internal TransitionPath Path;
        [NonSerialized] internal bool Applied;
        [NonSerialized] internal bool Started;
        [NonSerialized] internal TransitionValue ResolvedStart;
        [NonSerialized] internal TransitionValue ResolvedEnd;
        [NonSerialized] internal Vector2 ShakeBase;
        [NonSerialized] internal UnityEngine.Component RuntimeTarget;
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
        public AudioSource audioSource;

        private bool _playing;
        private bool _smoothStart;
        private bool _reversed;
        private float _time;
        private int _remainingTimes;
        private float _totalDuration;
        private bool _hasInfinite;
        private int _playVersion;

        private static readonly List<Transition> NestedTransitions = new();

        private void Start()
        {
            if (autoPlay)
                Play(autoPlayTimes, autoPlayDelay).Forget();
        }

        // 倒放（复刻 FairyGUI PlayReverse）：时间轴翻转，各 item 由末态回到起态。用于窗口关闭等"进场倒放"。
        public UniTask PlayReverse(int times = 1, float delay = 0) => PlayFrom(true, times, delay);

        public UniTask Play(int times = 1, float delay = 0) => PlayFrom(false, times, delay);

        // 播完、被 Stop/新播放打断或对象销毁后完成；无限循环需由后三者之一结束。
        private async UniTask PlayFrom(bool reversed, int times, float delay)
        {
            _reversed = reversed;
            var version = ++_playVersion;
            _playing = true;
            _smoothStart = true;
            _remainingTimes = times;
            _time = -delay;
            _totalDuration = 0;
            _hasInfinite = false;
            foreach (var item in items)
            {
                item.Applied = false;
                item.Started = false;
                if (item.pathData is { Length: > 0 })
                    item.Path ??= new TransitionPath(item.pathData);
                item.RuntimeTarget ??= ResolveTarget(item);
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
            while (Application.isPlaying && this != null && version == _playVersion && _playing)
            {
                await UniTask.Yield(PlayerLoopTiming.Update);
                if (this == null || version != _playVersion || !_playing)
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
        }

        public bool playing => _playing;

        // 复刻 FairyGUI Stop() 默认 setToComplete：未完成 item 落到终态（倒放落回起态）。
        // Shake 必须归位——否则下次 Play 把偏移位当新基准，反复播放/停止位置越漂越远。
        public void Stop()
        {
            if (!_playing)
                return;
            _playing = false;
            _playVersion++;
            foreach (var item in items)
                if (item.type is TransitionItemType.Sound or TransitionItemType.Nested)
                {
                    if (item.Applied && item.type == TransitionItemType.Nested)
                        ((Transition)item.RuntimeTarget).Stop();
                    item.Applied = true; // 停止时不补播声音、不启动未到点的嵌套
                }
            // 倒放逆序求值：终态以时间轴最早的 item 为准。无限循环 item 无终态（对齐 FairyGUI ToComplete）。
            var effectiveTime = _reversed ? 0 : _totalDuration;
            for (var i = 0; i < items.Length; i++)
            {
                var item = items[_reversed ? items.Length - 1 - i : i];
                if (!item.tween || item.repeat >= 0)
                    Evaluate(item, effectiveTime - item.time);
            }
        }

        public void Step(float deltaTime)
        {
            _time += deltaTime;
            if (_time < 0)
                return;

            // 倒放：把全局时间沿时间轴翻转，各 item 收到递减的 local，从末态求值回到起态。
            // 钳到 ≥0，保证收尾时 time=0 的 item 落在 local=0（= start 值），而非负 local 被提前 return 卡在中途。
            // 倒放按数组逆序求值：同帧触发多个瞬时 item 时按时间轴倒序生效（items 按 time 升序烘焙）。
            var effectiveTime = _reversed ? Mathf.Max(0, _totalDuration - _time) : _time;
            if (_reversed)
                for (var i = items.Length - 1; i >= 0; i--)
                    Evaluate(items[i], effectiveTime - items[i].time);
            else
                foreach (var item in items)
                    Evaluate(item, effectiveTime - item.time);

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
                _playing = false;
        }

        private void Evaluate(TransitionItem item, float local)
        {
            if (item.type == TransitionItemType.Shake)
            {
                // 抖动窗口 [0, duration)：正放 local 递增（幅度衰减），倒放 local 递减（同一幅度曲线倒序）。
                // 窗口结束（正放上穿 duration / 倒放下穿 0）必须归位 ShakeBase，否则反复播放位置越漂越远。
                var duration = item.start[1];
                if (item.Applied || (_reversed ? local >= duration : local < 0))
                    return;
                var rt = Target(item);
                if (!item.Started)
                {
                    item.Started = true;
                    item.ShakeBase = rt.anchoredPosition;
                }
                if (_reversed ? local <= 0 : local >= duration)
                {
                    item.Applied = true;
                    rt.anchoredPosition = item.ShakeBase;
                }
                else
                    rt.anchoredPosition = item.ShakeBase + UnityEngine.Random.insideUnitCircle * (item.start[0] * (1 - local / duration));
                return;
            }

            if (!item.tween)
            {
                // 瞬时 item：正放在时间轴到达 item.time（local 上穿 0）时触发，倒放在翻转时间轴下穿 item.time 时触发。
                if (item.Applied || (_reversed ? local > 0 : local < 0))
                    return;
                item.Applied = true;
                ApplyInstant(item);
                return;
            }

            if (local < 0)
            {
                if (!_reversed || !item.Started || item.Applied)
                    return;
                // 倒放下穿 item 起点：帧步长可能跳过 local==0，补一次精确起态。
                item.Applied = true;
                local = 0;
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

            ApplyValue(item, TransitionValue.Lerp(item.ResolvedStart, item.ResolvedEnd, ratio));
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
                    var movieClip = (MovieClip)item.RuntimeTarget;
                    movieClip.playing = item.start[1] != 0;
                    if (item.start[0] >= 0)
                        movieClip.frame = (int)item.start[0];
                    break;
                }
                case TransitionItemType.Sound:
                    if (item.sound != null)
                        audioSource.PlayOneShot(item.sound, item.volume);
                    break;
                case TransitionItemType.Nested:
                {
                    var nested = (Transition)item.RuntimeTarget;
                    if (_reversed)
                        nested.PlayReverse(item.playTimes).Forget();
                    else
                        nested.Play(item.playTimes).Forget();
                    break;
                }
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
                    ((TextField)item.RuntimeTarget).text = item.stringValue;
                    break;
                default:
                    ApplyValue(item, Resolve(item, item.start));
                    break;
            }
        }

        private void ApplyValue(TransitionItem item, TransitionValue value)
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
                    ((CanvasGroup)item.RuntimeTarget).alpha = value[0];
                    break;
                case TransitionItemType.Rotation:
                    rt.localEulerAngles = new Vector3(0, 0, -value[0]);
                    break;
                case TransitionItemType.Skew:
                    var skewShape = (Graph)item.RuntimeTarget; // 目前仅 Graph 支持逐顶点 skew
                    skewShape.skew = new Vector2(value[0], value[1]);
                    skewShape.SetVerticesDirty();
                    break;
                case TransitionItemType.Color:
                    ((Graphic)item.RuntimeTarget).color = new Color(value[0], value[1], value[2], value.Count > 3 ? value[3] : 1);
                    break;
                case TransitionItemType.ColorFilter:
                    // ColorAdjust 由 Migrate 烘焙在 ColorFilter 目标上。
                    ((ColorAdjust)item.RuntimeTarget).Set(value[0], value[1], value[2], value[3]);
                    break;
            }
        }

        private TransitionValue Resolve(TransitionItem item, float[] values)
        {
            var result = Vector4.zero;
            for (var i = 0; i < values.Length; i++)
                result[i] = float.IsNaN(values[i]) ? Current(item, i) : values[i];
            return new TransitionValue(result, values.Length);
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
                TransitionItemType.Alpha => ((CanvasGroup)item.RuntimeTarget).alpha,
                TransitionItemType.Rotation => -rt.localEulerAngles.z,
                TransitionItemType.Skew => index == 0 ? ((Graph)item.RuntimeTarget).skew.x : ((Graph)item.RuntimeTarget).skew.y,
                _ => 0,
            };
        }

        private UnityEngine.Component ResolveTarget(TransitionItem item)
        {
            var target = Target(item);
            return item.type switch
            {
                TransitionItemType.Alpha => target.GetComponent<CanvasGroup>(),
                TransitionItemType.Animation => target.GetComponent<MovieClip>(),
                TransitionItemType.Nested => FindNested(target, item.stringValue),
                TransitionItemType.Text => target.GetComponent<TextField>(),
                TransitionItemType.Skew => target.GetComponent<Graph>(),
                TransitionItemType.Color => target.GetComponent<Graphic>(),
                TransitionItemType.ColorFilter => target.GetComponent<ColorAdjust>(),
                _ => null,
            };
        }

        private static Transition FindNested(RectTransform target, string name)
        {
            NestedTransitions.Clear();
            target.GetComponents(NestedTransitions);
            var result = NestedTransitions.AsValueEnumerable().FirstOrDefault(t => t.transitionName == name);
            NestedTransitions.Clear();
            return result;
        }

        private RectTransform Target(TransitionItem item) =>
            item.target != null ? item.target : (RectTransform)transform;
    }
}
