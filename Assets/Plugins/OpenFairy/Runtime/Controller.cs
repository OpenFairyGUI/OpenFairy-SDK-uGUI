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

        [NonSerialized]
        private bool _displayLocksBound;

        public T page
        {
            get => _page;
            set
            {
                if (_initialized && EqualityComparer<T>.Default.Equals(_page, value))
                    return;
                _page = value;
                _initialized = true;
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
