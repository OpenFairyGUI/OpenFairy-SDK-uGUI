using System;
using DG.Tweening;
using UnityEngine;

namespace NanamiUI
{
    [Serializable]
    public abstract class Gear<T> where T : struct, Enum
    {
        public GameObject target;
        public T[] pages;

        // 复刻 FairyGUI GearTweenConfig 默认值（GearBase.cs:195-201）。
        public bool tween;
        public float duration = 0.3f;
        public Ease ease = Ease.OutQuad; // QuadOut
        public float delay;

        public abstract void Apply(T page);                            // 烘焙/编辑态：直接置位
        public virtual void Apply(T page, bool animate) => Apply(page); // 默认忽略 animate
    }
}
