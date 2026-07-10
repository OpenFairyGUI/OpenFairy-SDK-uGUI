using System;

namespace NanamiUI
{
    [Serializable]
    public class GearAni<T> : Gear<T> where T : struct, Enum
    {
        public int[] frames;
        public bool[] playings;
        public int defaultFrame;
        public bool defaultPlaying = true;

        [NonSerialized] private MovieClip _movieClip;

        public override void Apply(T page)
        {
            var index = Array.IndexOf(pages, page);
            var movieClip = _movieClip ??= target.GetComponent<MovieClip>();
            movieClip.playing = index >= 0 ? playings[index] : defaultPlaying;
            movieClip.SetFrame(index >= 0 ? frames[index] : defaultFrame);
        }
    }
}
