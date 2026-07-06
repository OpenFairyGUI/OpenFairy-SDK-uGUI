using UnityEngine;

namespace NanamiUI
{
    public abstract class Gear : MonoBehaviour
    {
        public int[] pages;

        public abstract void Apply(int page);
    }
}
