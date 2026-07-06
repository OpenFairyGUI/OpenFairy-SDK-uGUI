using UnityEngine;
using UnityEngine.UI;

namespace NanamiUI
{
    public class MovieClip : Image
    {
        public Sprite[] frames;
        public float interval = 0.1f;
        public float[] addDelays;
        public bool playing = true;
        public int frame;

        private float _time;

        public void SetFrame(int value)
        {
            frame = value;
            if (frames != null && frames.Length > 0)
                sprite = frames[Mathf.Clamp(frame, 0, frames.Length - 1)];
        }

        private void Update()
        {
            if (!playing || frames == null || frames.Length < 2)
                return;
            _time += Time.deltaTime;
            while (_time >= interval + addDelays[frame])
            {
                _time -= interval + addDelays[frame];
                SetFrame((frame + 1) % frames.Length);
            }
        }
    }
}
