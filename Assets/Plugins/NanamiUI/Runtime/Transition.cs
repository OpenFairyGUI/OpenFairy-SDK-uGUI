using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using DG.Tweening.Core.Easing;
using UnityEngine;
using UnityEngine.UI;

namespace NanamiUI
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
        private Action _onComplete;
        private float _totalDuration;
        private bool _hasInfinite;
        private int _playVersion;

        private static readonly List<Transition> NestedTransitions = new();

        private void Start()
        {
            if (autoPlay)
                Play(autoPlayTimes, autoPlayDelay);
        }

        // 倒放（复刻 FairyGUI PlayReverse）：时间轴翻转，各 item 由末态回到起态。用于窗口关闭等"进场倒放"。
        public void PlayReverse(int times = 1, float delay = 0, Action onComplete = null)
        {
            _reversed = true;
            PlayFrom(times, delay, onComplete);
        }

        public void Play(int times = 1, float delay = 0, Action onComplete = null)
        {
            _reversed = false;
            PlayFrom(times, delay, onComplete);
        }

        private void PlayFrom(int times, float delay, Action onComplete)
        {
            var version = ++_playVersion;
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
            if (Application.isPlaying && _playing)
                Run(version).Forget();
        }

        public void Stop()
        {
            _playing = false;
            _playVersion++;
        }

        private async UniTask Run(int version)
        {
            while (true)
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

        public void Step(float deltaTime)
        {
            _time += deltaTime;
            if (_time < 0)
                return;

            // 倒放：把全局时间沿时间轴翻转，各 item 收到递减的 local，从末态求值回到起态。
            // 钳到 ≥0，保证收尾时 time=0 的 item 落在 local=0（= start 值），而非负 local 被提前 return 卡在中途。
            var effectiveTime = _reversed ? Mathf.Max(0, _totalDuration - _time) : _time;
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
            {
                _playing = false;
                var onComplete = _onComplete;
                _onComplete = null;
                onComplete?.Invoke();
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
                        movieClip.SetFrame((int)item.start[0]);
                    break;
                }
                case TransitionItemType.Sound:
                    if (item.sound != null)
                        audioSource.PlayOneShot(item.sound, item.volume);
                    break;
                case TransitionItemType.Nested:
                    ((Transition)item.RuntimeTarget).Play(item.playTimes);
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
            Transition result = null;
            foreach (var transition in NestedTransitions)
                if (transition.transitionName == name)
                {
                    result = transition;
                    break;
                }
            NestedTransitions.Clear();
            return result;
        }

        private RectTransform Target(TransitionItem item) =>
            item.target != null ? item.target : (RectTransform)transform;
    }
}
