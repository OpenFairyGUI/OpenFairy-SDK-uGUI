using System;
using System.Collections.Generic;
using UnityEngine;

namespace OpenFairy.UGUI
{
    [Serializable]
    public struct Controller<T> where T : struct, Enum
    {
        [SerializeField]
        private T _page;
        [SerializeField]
        private bool _initialized;
        [SerializeReference]
        public Gear<T>[] gears;
        [SerializeReference]
        public ControllerAction<T>[] actions;

        [NonSerialized]
        private bool _displayLocksBound;
        [NonSerialized]
        private bool _actionsInitialized;
        [NonSerialized]
        private bool _changing;

        public event Action onChanged;
        public bool changing => _changing;

        internal void Bake(T value)
        {
            _page = value;
            _initialized = true;
            Apply(value);
        }

        public T page
        {
            get => _page;
            set
            {
                if (_initialized && EqualityComparer<T>.Default.Equals(_page, value))
                    return;
                var previous = _page;
                var hasPrevious = _initialized;
                _page = value;
                _initialized = true;
                _actionsInitialized = true;
                var wasChanging = _changing;
                _changing = true;
                try
                {
                    Apply(value);
                    RunActions(previous, value, hasPrevious);
                    if (Application.isPlaying)
                        onChanged?.Invoke();
                }
                finally
                {
                    _changing = wasChanging;
                }
            }
        }

        // Called after the baked fields are assigned, before ordinary page changes.
        public void Initialize()
        {
            if (_actionsInitialized)
                return;
            _actionsInitialized = true;
            _changing = true;
            try
            {
                RunActions(default, _page, false);
            }
            finally
            {
                _changing = false;
            }
        }

        private void RunActions(T previous, T current, bool hasPrevious)
        {
            foreach (var action in actions ?? Array.Empty<ControllerAction<T>>())
                action.Run(previous, current, hasPrevious);
        }

        private void Apply(T value)
        {
                var animate = Application.isPlaying; // 烘焙(编辑态)=false 直接置位；运行时=true 缓动
                var list = gears ?? Array.Empty<Gear<T>>();
                // 复刻 FairyGUI HandleControllerChanged：给每个 tween gear 注入同 target 的 GearDisplay，
                // 先 apply 非 display gear（起 tween 并加 display lock），再 apply display gear（此时锁已就位，
                // 本应隐藏的 target 会保持显示到 tween 结束，即"旧页飞出再消失"而非立即消失）。
                if (animate && !_displayLocksBound)
                {
                    foreach (var gear in list)
                        if (gear is not GearDisplay<T>)
                            gear.displayLock = FindDisplay(list, gear.target);
                    _displayLocksBound = true;
                }
                foreach (var gear in list)
                    if (gear is not GearDisplay<T>)
                        gear.Apply(value, animate);
                foreach (var gear in list)
                    if (gear is GearDisplay<T>)
                        gear.Apply(value, animate);
        }

        private static IDisplayGear FindDisplay(Gear<T>[] gears, GameObject target)
        {
            foreach (var gear in gears)
                if (gear is GearDisplay<T> display && display.Target == target)
                    return display;
            return null;
        }
    }
}
