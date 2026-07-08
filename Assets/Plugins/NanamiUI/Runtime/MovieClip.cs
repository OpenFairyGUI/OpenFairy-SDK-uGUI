using System;
using UnityEngine;
using UnityEngine.UI;

namespace NanamiUI
{
    // 复刻 FairyGUI MovieClip：逐帧播放，支持 swing（往返）、repeatDelay（回到首帧的额外停顿）、
    // timeScale、SetPlaySettings（播放区间/次数/结束帧）。单帧步进（同 FairyGUI OnTimer，非一次排空）。
    public class MovieClip : UnityEngine.UI.Image
    {
        public Sprite[] frames;
        public float interval = 0.1f;
        public float[] addDelays;
        public bool playing = true;
        public bool swing;
        public float repeatDelay;
        public float timeScale = 1;
        public int frame;

        [NonSerialized] public Action onPlayEnd;

        private float _frameElapsed;
        private bool _reversed;
        private int _repeatedCount;
        private int _start;
        private int _end = -1;
        private int _endAt = -1;
        private int _times;
        private int _status; // 0 播放中，1 下一轮，2 结束中，3 已结束

        public void SetFrame(int value)
        {
            if (frames != null && frames.Length > 0)
            {
                frame = Mathf.Clamp(value, 0, frames.Length - 1); // 钳进有效范围，避免自播 Update 越界索引 addDelays[frame]
                sprite = frames[frame];
            }
            else
                frame = value;
        }

        // 从 start 帧播到 end 帧（-1=末帧），重复 times 次（0=无限循环），结束停在 endAt 帧（-1=end）。
        public void SetPlaySettings(int start = 0, int end = -1, int times = 0, int endAt = -1)
        {
            _start = start;
            _end = end < 0 || end > frames.Length - 1 ? frames.Length - 1 : end;
            _times = times;
            _endAt = endAt < 0 ? _end : endAt;
            _status = 0;
            _reversed = false;
            _repeatedCount = 0;
            _frameElapsed = 0;
            SetFrame(start);
            playing = true;
        }

        public void Rewind()
        {
            SetFrame(0);
            _frameElapsed = 0;
            _reversed = false;
        }

        private void Update()
        {
            if (!playing || frames == null || frames.Length < 2 || _status == 3)
                return;

            _frameElapsed += Time.deltaTime * timeScale;
            var tt = interval + addDelays[frame];
            if (frame == 0 && _repeatedCount > 0)
                tt += repeatDelay;
            if (_frameElapsed < tt)
                return;

            _frameElapsed -= tt;
            if (_frameElapsed > interval)
                _frameElapsed = interval;

            if (swing)
            {
                if (_reversed)
                {
                    frame--;
                    if (frame <= 0)
                    {
                        frame = 0;
                        _repeatedCount++;
                        _reversed = !_reversed;
                    }
                }
                else
                {
                    frame++;
                    if (frame > frames.Length - 1)
                    {
                        frame = Mathf.Max(0, frames.Length - 2);
                        _repeatedCount++;
                        _reversed = !_reversed;
                    }
                }
            }
            else
            {
                frame++;
                if (frame > frames.Length - 1)
                {
                    frame = 0;
                    _repeatedCount++;
                }
            }

            if (_status == 1) // 新一轮
            {
                frame = _start;
                _frameElapsed = 0;
                _status = 0;
                SetFrame(frame);
            }
            else if (_status == 2) // 结束
            {
                frame = _endAt;
                _frameElapsed = 0;
                _status = 3;
                SetFrame(frame);
                onPlayEnd?.Invoke();
            }
            else
            {
                SetFrame(frame);
                if (frame == _end)
                {
                    if (_times > 0)
                    {
                        _times--;
                        if (_times == 0)
                            _status = 2;
                        else
                            _status = 1;
                    }
                    else if (_start != 0)
                        _status = 1;
                }
            }
        }
    }
}
