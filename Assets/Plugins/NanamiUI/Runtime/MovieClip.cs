using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace NanamiUI
{
    // 复刻 FairyGUI MovieClip：逐帧播放，支持 swing（往返）、repeatDelay（回到首帧的额外停顿）、
    // timeScale、Play（播放区间/次数/结束帧）。单帧步进（同 FairyGUI OnTimer，非一次排空）。
    public class MovieClip : UnityEngine.UI.Image
    {
        private enum Status
        {
            Playing,
            NextRound,
            Ending,
            Ended,
        }

        public Sprite[] frames;
        public float interval = 0.1f;
        public float[] addDelays;
        public bool swing;
        public float repeatDelay;
        public float timeScale = 1;

        [SerializeField, FormerlySerializedAs("frame")]
        private int _frame;

        [SerializeField, FormerlySerializedAs("playing")]
        private bool _playing = true;

        private float _frameElapsed;
        private bool _reversed;
        private int _repeatedCount;
        private int _start;
        private int _end = -1;
        private int _endAt = -1;
        private int _times;
        private Status _status;
        private int _playVersion;

        public bool playing
        {
            get => _playing;
            set
            {
                if (_playing == value)
                    return;
                _playing = value;
                RestartLoop().Forget();
            }
        }

        // 复刻 GMovieClip.frame：setter 即刷帧（钳进有效范围，避免自播 Advance 越界索引 addDelays）。
        public int frame
        {
            get => _frame;
            set
            {
                if (frames is { Length: > 0 })
                {
                    _frame = Mathf.Clamp(value, 0, frames.Length - 1);
                    sprite = frames[_frame];
                }
                else
                    _frame = value;
            }
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            RestartLoop().Forget();
        }

        protected override void OnDisable()
        {
            _playVersion++;
            base.OnDisable();
        }

        // 从 start 帧播到 end 帧（-1=末帧），重复 times 次（0=无限循环），结束停在 endAt 帧（-1=end）。
        public UniTask Play(int start = 0, int end = -1, int times = 1, int endAt = -1)
        {
            _start = start;
            _end = end < 0 || end > frames.Length - 1 ? frames.Length - 1 : end;
            _times = times;
            _endAt = endAt < 0 ? _end : endAt;
            _status = Status.Playing;
            _reversed = false;
            _repeatedCount = 0;
            _frameElapsed = 0;
            frame = start;
            _playing = true;
            return RestartLoop();
        }

        public void Rewind()
        {
            frame = 0;
            _frameElapsed = 0;
            _reversed = false;
        }

        private UniTask RestartLoop()
        {
            var version = ++_playVersion;
            return Application.isPlaying && isActiveAndEnabled && _playing && frames is { Length: > 1 } && _status != Status.Ended
                ? Run(version)
                : UniTask.CompletedTask;
        }

        private async UniTask Run(int version)
        {
            while (true)
            {
                await UniTask.Yield(PlayerLoopTiming.Update);
                if (this == null || version != _playVersion || !_playing || !isActiveAndEnabled || _status == Status.Ended)
                    return;
                Advance(Time.deltaTime * timeScale);
                if (_status == Status.Ended)
                    return;
            }
        }

        private void Advance(float deltaTime)
        {
            _frameElapsed += deltaTime;
            var tt = interval + addDelays[_frame];
            if (_frame == 0 && _repeatedCount > 0)
                tt += repeatDelay;
            if (_frameElapsed < tt)
                return;

            _frameElapsed -= tt;
            if (_frameElapsed > interval)
                _frameElapsed = interval;

            var next = _frame;
            if (swing)
            {
                if (_reversed)
                {
                    next--;
                    if (next <= 0)
                    {
                        next = 0;
                        _repeatedCount++;
                        _reversed = !_reversed;
                    }
                }
                else
                {
                    next++;
                    if (next > frames.Length - 1)
                    {
                        next = Mathf.Max(0, frames.Length - 2);
                        _repeatedCount++;
                        _reversed = !_reversed;
                    }
                }
            }
            else
            {
                next++;
                if (next > frames.Length - 1)
                {
                    next = 0;
                    _repeatedCount++;
                }
            }

            if (_status == Status.NextRound)
            {
                _frameElapsed = 0;
                _status = Status.Playing;
                frame = _start;
            }
            else if (_status == Status.Ending)
            {
                _frameElapsed = 0;
                _status = Status.Ended;
                frame = _endAt;
            }
            else
            {
                frame = next;
                if (_frame == _end)
                {
                    if (_times > 0)
                    {
                        _times--;
                        _status = _times == 0 ? Status.Ending : Status.NextRound;
                    }
                    else if (_start != 0)
                        _status = Status.NextRound;
                }
            }
        }
    }
}
