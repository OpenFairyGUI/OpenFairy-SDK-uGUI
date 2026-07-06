using System;
using UnityEngine;

namespace NanamiUI
{
    public interface IDisplayGear
    {
        bool On { get; }
        int Condition { get; }
    }

    // gearDisplay 与 gearDisplay2 共用本类：两者互为 partner 时按 condition(0=AND,1=OR) 组合可见性。
    [Serializable]
    public class GearDisplay<T> : Gear<T>, IDisplayGear where T : struct, Enum
    {
        public int condition;
        [SerializeReference]
        public IDisplayGear partner;
        public bool on = true;

        public bool On => on;
        public int Condition => condition;

        public override void Apply(T page)
        {
            on = pages.Length == 0 || Array.IndexOf(pages, page) >= 0;
            var visible = partner == null ? on
                : condition == 1 || partner.Condition == 1 ? on || partner.On
                : on && partner.On;
            target.SetActive(visible);
        }
    }
}
