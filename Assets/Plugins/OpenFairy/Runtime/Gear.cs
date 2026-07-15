using System;
using DG.Tweening;
using UnityEngine;

namespace OpenFairy.UGUI
{
    [Serializable]
    public abstract class Gear<T> where T : struct, Enum
    {
        public GameObject target;
        public T[] pages;

        // 同 target 的 GearDisplay（由 Controller 在切页时按 target 匹配注入）；tween gear 用它加/解 display lock。
        [NonSerialized] public IDisplayGear displayLock;

        // 复刻 FairyGUI GearTweenConfig 默认值（GearBase.cs:195-201）。
        public bool tween;
        public float duration = 0.3f;
        public Ease ease = Ease.OutQuad; // QuadOut
        public float delay;

        [NonSerialized] private Tweener _tweener;
        [NonSerialized] private IDisplayGear _lockedDisplay;
        [NonSerialized] private TweenCallback _completeTween;

        protected void KillTween()
        {
            _tweener?.Kill();
            _tweener = null;
            ReleaseLock();
        }

        protected void TrackTween(Tweener tweener)
        {
            if (displayLock != null)
            {
                displayLock.AddLock();
                _lockedDisplay = displayLock;
            }
            _tweener = tweener;
            _completeTween ??= CompleteTween;
            _tweener.OnComplete(_completeTween);
        }

        private void CompleteTween()
        {
            _tweener = null;
            ReleaseLock();
        }

        private void ReleaseLock()
        {
            if (_lockedDisplay == null)
                return;
            _lockedDisplay.ReleaseLock();
            _lockedDisplay = null;
        }

        public abstract void Apply(T page);                            // 烘焙/编辑态：直接置位
        public virtual void Apply(T page, bool animate) => Apply(page); // 默认忽略 animate
    }
}
