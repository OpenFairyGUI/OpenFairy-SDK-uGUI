using System;
using Cysharp.Threading.Tasks;

namespace OpenFairy.UGUI
{
    [Serializable]
    public abstract class ControllerAction<T> where T : struct, Enum
    {
        // null is a wildcard; an empty array matches no page (a deleted page in the source).
        public T[] fromPages;
        public T[] toPages;

        internal void Run(T previous, T current, bool hasPrevious)
        {
            if ((fromPages == null || hasPrevious && Array.IndexOf(fromPages, previous) >= 0)
                && (toPages == null || Array.IndexOf(toPages, current) >= 0))
                Enter(current);
            else
                Leave();
        }

        protected abstract void Enter(T page);
        protected virtual void Leave() { }
    }

    [Serializable]
    public sealed class ChangePageAction<T> : ControllerAction<T> where T : struct, Enum
    {
        public Component target;
        public int controller;
        public T[] sourcePages;
        public int[] targetPages;

        protected override void Enter(T page)
        {
            var index = Array.IndexOf(sourcePages, page);
            if (index >= 0 && targetPages[index] >= 0 && !target.IsControllerChanging(controller))
                target.SetControllerPage(controller, targetPages[index]);
        }
    }

    [Serializable]
    public sealed class PlayTransitionAction<T> : ControllerAction<T> where T : struct, Enum
    {
        public Transition transition;
        public int playTimes = 1;
        public float delay;
        public bool stopOnExit;
        [NonSerialized] private bool _entered;

        protected override void Enter(T page)
        {
            if (!UnityEngine.Application.isPlaying)
                return;
            if (_entered && transition.playing)
                transition.ChangePlayTimes(playTimes);
            else
                transition.Play(playTimes, delay).Forget();
            _entered = true;
        }

        protected override void Leave()
        {
            if (stopOnExit && _entered)
            {
                transition.Stop();
                _entered = false;
            }
        }
    }
}
