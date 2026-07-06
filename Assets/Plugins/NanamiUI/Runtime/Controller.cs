using System;
using UnityEngine;

namespace NanamiUI
{
    public abstract class Controller : MonoBehaviour
    {
        public string[] pageNames;
        public Gear[] gears;
        public int selected;

        public int SelectedIndex
        {
            get => selected;
            set
            {
                selected = value;
                foreach (var gear in gears)
                    gear.Apply(value);
            }
        }

        public string SelectedPage
        {
            get => pageNames[selected];
            set => SelectedIndex = Array.IndexOf(pageNames, value);
        }

        public bool HasPage(string page) => Array.IndexOf(pageNames, page) >= 0;
    }

    public abstract class Controller<T> : Controller where T : Enum
    {
        public T Selected
        {
            get => (T)Enum.ToObject(typeof(T), selected);
            set => SelectedIndex = Convert.ToInt32(value);
        }
    }
}
