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
                foreach (var gear in gears ?? Array.Empty<Gear<T>>())
                    gear.Apply(value);
            }
        }
    }
}
