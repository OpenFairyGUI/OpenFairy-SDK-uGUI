using System;
using UnityEngine;

namespace NanamiUI
{
    [Serializable]
    public abstract class Gear<T> where T : struct, Enum
    {
        public GameObject target;
        public T[] pages;

        public abstract void Apply(T page);
    }
}
