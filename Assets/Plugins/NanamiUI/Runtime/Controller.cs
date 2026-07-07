using System;
using UnityEngine;

namespace NanamiUI
{
    [Serializable]
    public struct Controller<T> where T : struct, Enum
    {
        [SerializeField]
        private T _page;
        [SerializeReference]
        public Gear<T>[] gears;

        public T page
        {
            get => _page;
            set
            {
                _page = value;
                var animate = Application.isPlaying; // 烘焙(编辑态)=false 直接置位；运行时=true 缓动
                foreach (var gear in gears ?? Array.Empty<Gear<T>>())
                    gear.Apply(value, animate);
            }
        }
    }
}
