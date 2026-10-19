using System;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using DG.Tweening.Core.Easing;
using UnityEngine;
using UnityEngine.UI;

#if FAIRYGUI_TMPRO
using TextComponent = TMPro.TextMeshProUGUI;
#else
using TextComponent = OpenFairy.UGUI.TextField;
#endif

namespace OpenFairy.UGUI
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
        Skew,
        Icon,
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
        public UnityEngine.Component component; // 烘焙的目标组件：CanvasGroup/MovieClip/嵌套 Transition/文字/Graph/Graphic/ColorAdjust
        public int playTimes = 1;
        public AudioClip sound;
        public Sprite icon;
        public float volume = 1;
        public Vector2 positionOffset;  // FairyGUI xy → anchoredPosition 的固定偏移（pivot/anchor/拉伸锚点修正）
        public float[] pathData;

        [NonSerialized] internal TransitionPath Path;
        [NonSerialized] internal Action Hook;
        [NonSerialized] internal Action EndHook;
        [NonSerialized] internal bool HookStarted;
        [NonSerialized] internal bool HookEnded;
        [NonSerialized] internal bool Applied;
        [NonSerialized] internal bool Started;
        [NonSerialized] internal Vector4 ResolvedStart;
        [NonSerialized] internal Vector4 ResolvedEnd;
        [NonSerialized] internal Vector2 ShakeBase;
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

        // 复刻 Transition.OnOwnerAddedToStage/OnOwnerRemovedFromStage：显示时自动播放，隐藏时原地停止。
        private void OnEnable()
        {
            if (autoPlay && !playing)
                Play(autoPlayTimes, autoPlayDelay).Forget();
        }

        private void OnDisable() => Stop(false);

        // 倒放（复刻 FairyGUI PlayReverse）：时间轴翻转，各 item 由末态回到起态。用于窗口关闭等"进场倒放"。
        public UniTask PlayReverse(int times = 1, float delay = 0) => PlayFrom(true, times, delay);

        public UniTask Play(int times = 1, float delay = 0) => PlayFrom(false, times, delay);

        // 播完、被 Stop/新播放打断或对象销毁后完成；无限循环需由后三者之一结束。
        private async UniTask PlayFrom(bool reversed, int times, float delay)
        {
            Stop();
            _reversed = reversed;
            var version = ++_playVersion;
            _playing = true;
            _smoothStart = true;
            _remainingTimes = times;
            _time = -delay;
            _totalDuration = 0;
            _hasInfinite = false;
            ResetItems();
            foreach (var item in items)
            {
                if (item.pathData is { Length: > 0 })
                    item.Path ??= new TransitionPath(item.pathData);
                var tail = Tail(item);
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

        public void ChangePlayTimes(int times) => _remainingTimes = times;

        // 生成代码的标签枚举经此按 item 序号绑定。
        public void SetHook(int itemIndex, bool tweenEnd, Action callback)
        {
            if (tweenEnd)
                items[itemIndex].EndHook = callback;
            else
                items[itemIndex].Hook = callback;
        }


        public void ClearHooks()
        {
            foreach (var item in items)
                item.Hook = item.EndHook = null;
        }

        // 复刻 FairyGUI Stop(setToComplete)：完成时未完成 item 落到终态（倒放落回起态），否则原地停止。
        // Shake 必须归位——否则下次 Play 把偏移位当新基准，反复播放/停止位置越漂越远。
        public void Stop(bool setToComplete = true)
        {
            if (!_playing)
                return;
            _playing = false;
            _playVersion++;
            foreach (var item in items)
                if (item.type is TransitionItemType.Sound or TransitionItemType.Nested)
                {
                    if (item.Applied && item.component is Transition nested)
                        nested.Stop(setToComplete);
                    item.Applied = true; // 停止时不补播声音、不启动未到点的嵌套
                }
                else if (!setToComplete && item.type == TransitionItemType.Shake && item.Started && !item.Applied)
                    Target(item).anchoredPosition = item.ShakeBase;
            if (!setToComplete)
                return;
            // 倒放逆序求值：终态以时间轴最早的 item 为准。无限循环 item 无终态（对齐 FairyGUI ToComplete）。
            var effectiveTime = _reversed ? 0 : _totalDuration;
            for (var i = 0; i < items.Length; i++)
            {
                var item = items[_reversed ? items.Length - 1 - i : i];
                if (!item.tween || item.repeat >= 0)
                    EvaluateValue(item, effectiveTime - item.time);
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
            var version = _playVersion;
            for (var i = 0; i < items.Length; i++)
            {
                var item = items[_reversed ? items.Length - 1 - i : i];
                Evaluate(item, effectiveTime - item.time);
                // Hooks can stop or replace playback. Do not finish or evaluate the replaced timeline.
                if (version != _playVersion || !_playing)
                    return;
            }

            if (_hasInfinite || _time < _totalDuration)
                return;
            if (_remainingTimes < 0 || --_remainingTimes > 0)
            {
                _time = 0;
                ResetItems();
            }
            else
                _playing = false;
        }

        private void ResetItems()
        {
            foreach (var item in items)
            {
                item.Applied = false;
                item.Started = false;
                item.HookStarted = false;
                item.HookEnded = false;
            }
        }

        // item 自 time 起的活动时长。
        private static float Tail(TransitionItem item) =>
            item.type == TransitionItemType.Shake ? item.start[1]
            : !item.tween ? 0
            : item.repeat < 0 ? float.PositiveInfinity
            : item.duration * (item.repeat + 1);

        private void EnsureResolved(TransitionItem item)
        {
            if (item.Started)
                return;
            item.Started = true;
            item.ResolvedStart = Resolve(item, item.start);
            item.ResolvedEnd = Resolve(item, item.end);
        }

        private void Evaluate(TransitionItem item, float local)
        {
            var tail = Tail(item);
            var reached = _reversed ? local <= tail : local >= 0;
            var version = _playVersion;
            if (item.tween && reached && !item.HookStarted)
            {
                // Resolve unspecified values before the start hook, like the tween's OnStart.
                EnsureResolved(item);
                item.HookStarted = true;
                item.Hook?.Invoke();
                if (version != _playVersion || !_playing)
                    return;
            }
            EvaluateValue(item, local);
            if (!item.tween && reached && !item.HookStarted)
            {
                item.HookStarted = true;
                item.Hook?.Invoke();
                if (version != _playVersion || !_playing)
                    return;
            }
            if (item.tween && item.HookStarted && !item.HookEnded
                && !float.IsPositiveInfinity(tail) && (_reversed ? local <= 0 : local >= tail))
            {
                item.HookEnded = true;
                item.EndHook?.Invoke();
            }
        }

        private void EvaluateValue(TransitionItem item, float local)
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

            // 复刻 Transition.PlayItem：倒放交换起止值而缓动仍正向推进，elapsed 是该 tween 已走的时间。
            var tail = Tail(item);
            var elapsed = _reversed && !float.IsPositiveInfinity(tail) ? tail - local : local;
            if (elapsed < 0)
                return;
            if (elapsed >= tail)
            {
                // 帧步长可能跳过终点，补一次精确终态。
                if (item.Applied)
                    return;
                item.Applied = true;
                elapsed = tail;
            }
            EnsureResolved(item);

            float t;
            if (item.repeat != 0)
            {
                var cycle = Mathf.FloorToInt(elapsed / item.duration);
                if (item.repeat > 0 && cycle > item.repeat)
                {
                    cycle = item.repeat;
                    t = item.duration;
                }
                else
                    t = elapsed - cycle * item.duration;
                if (item.yoyo && (cycle & 1) == 1)
                    t = item.duration - t;
            }
            else
                t = Mathf.Min(elapsed, item.duration);

            var ratio = EaseManager.Evaluate(item.ease, null, t, item.duration, 1.70158f, 0);
            var from = _reversed ? item.ResolvedEnd : item.ResolvedStart;
            if (item.Path != null)
            {
                // GTweener 的路径点不随起止交换反向，只以交换后的起点为基准。
                var point = item.Path.GetPointAt(ratio);
                Target(item).anchoredPosition = new Vector2(from.x + point.x, -(from.y + point.y)) + item.positionOffset;
                return;
            }

            ApplyValue(item, Vector4.LerpUnclamped(from, _reversed ? item.ResolvedStart : item.ResolvedEnd, ratio));
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
                    var movieClip = (MovieClip)item.component;
                    movieClip.playing = item.start[1] != 0;
                    if (item.start[0] >= 0)
                        movieClip.frame = (int)item.start[0];
                    break;
                }
                case TransitionItemType.Sound:
                    if (item.sound != null && isActiveAndEnabled)
                    {
                        var source = Root.inst != null ? Root.inst.soundSource : audioSource;
                        if (source.isActiveAndEnabled)
                            source.PlayOneShot(item.sound, item.volume);
                    }
                    break;
                case TransitionItemType.Nested when item.component is Transition nested: // 引用已失效的嵌套项不生效
                {
                    if (item.playTimes == 0) // 复刻 FairyGUI：播放次数 0 是嵌套动效的停止点
                        nested.Stop(false);
                    else if (_reversed)
                        nested.PlayReverse(item.playTimes).Forget();
                    else
                        nested.Play(item.playTimes).Forget();
                    break;
                }
                case TransitionItemType.Nested:
                    break;
                case TransitionItemType.Icon: // 复刻 GObject.icon：按钮/标签换图标，装载器换图
                    if (item.component is ButtonBase button) button.icon = item.icon;
                    else if (item.component is Label label) label.icon = item.icon;
                    else ((Loader)item.component).sprite = item.icon;
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
                    ((TextComponent)item.component).text = item.stringValue;
                    break;
                default:
                    ApplyValue(item, Resolve(item, item.start));
                    break;
            }
        }

        private void ApplyValue(TransitionItem item, Vector4 value)
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
                    ((CanvasGroup)item.component).alpha = value[0];
                    break;
                case TransitionItemType.Rotation:
                    rt.localEulerAngles = new Vector3(0, 0, -value[0]);
                    break;
                case TransitionItemType.Skew:
                    var skewShape = (Graph)item.component; // 目前仅 Graph 支持逐顶点 skew
                    skewShape.skew = new Vector2(value[0], value[1]);
                    skewShape.SetVerticesDirty();
                    break;
                case TransitionItemType.Color:
                    ((Graphic)item.component).color = new Color(value[0], value[1], value[2], value[3]);
                    break;
                case TransitionItemType.ColorFilter:
                    // ColorAdjust 由 Migrate 烘焙在 ColorFilter 目标上。
                    ((ColorAdjust)item.component).Set(value[0], value[1], value[2], value[3]);
                    break;
            }
        }

        private Vector4 Resolve(TransitionItem item, float[] values)
        {
            var result = Vector4.zero;
            for (var i = 0; i < values.Length; i++)
                result[i] = float.IsNaN(values[i]) ? Current(item, i) : values[i];
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
                TransitionItemType.Alpha => ((CanvasGroup)item.component).alpha,
                TransitionItemType.Rotation => -rt.localEulerAngles.z,
                TransitionItemType.Skew => index == 0 ? ((Graph)item.component).skew.x : ((Graph)item.component).skew.y,
                _ => 0,
            };
        }

        private RectTransform Target(TransitionItem item) =>
            item.target != null ? item.target : (RectTransform)transform;
    }
}
