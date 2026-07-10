using System;
using UnityEngine;

namespace NanamiUI
{
    // gearDisplay2 与 partner 的可见性组合方式（FairyGUI XML condition：0=AND，1=OR）。
    public enum DisplayCondition
    {
        And,
        Or,
    }

    public interface IDisplayGear
    {
        bool On { get; }
        DisplayCondition Condition { get; }
        GameObject Target { get; }
        // display lock：同 target 的 GearXY/GearSize 在 tween 期间加锁，令"本应隐藏"的 target 保持显示到 tween 结束，
        // 复刻 FairyGUI AddDisplayLock/ReleaseDisplayLock（否则旧页会立即消失而非飞出）。
        void AddLock();
        void ReleaseLock();
    }

    // gearDisplay 与 gearDisplay2 共用本类：两者互为 partner 时按 condition 组合可见性。
    [Serializable]
    public class GearDisplay<T> : Gear<T>, IDisplayGear where T : struct, Enum
    {
        public DisplayCondition condition;
        [SerializeReference]
        public IDisplayGear partner;
        public bool on = true;

        [NonSerialized] private int _locks;

        public bool On => on;
        public DisplayCondition Condition => condition;
        public GameObject Target => target;

        public void AddLock()
        {
            _locks++;
            UpdateVisible();
        }

        public void ReleaseLock()
        {
            if (_locks > 0)
                _locks--;
            UpdateVisible();
        }

        public override void Apply(T page)
        {
            on = pages.Length == 0 || Array.IndexOf(pages, page) >= 0;
            UpdateVisible();
        }

        private void UpdateVisible()
        {
            var visible = partner == null ? on
                : condition == DisplayCondition.Or || partner.Condition == DisplayCondition.Or ? on || partner.On
                : on && partner.On;
            if (!visible && _locks > 0)
                visible = true; // 被 tween 锁住：暂不隐藏，等 tween 结束 ReleaseLock 后再隐藏
            target.SetActive(visible);
        }
    }
}
