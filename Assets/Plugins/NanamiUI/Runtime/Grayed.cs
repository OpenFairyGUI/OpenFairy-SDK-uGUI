using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace NanamiUI
{
    [ExecuteAlways]
    public class Grayed : UIBehaviour
    {
        private static Material _material;

        protected override void OnEnable()
        {
            base.OnEnable();
            _material ??= new Material(Shader.Find("NanamiUI/UI Grayscale"));
            foreach (var graphic in GetComponentsInChildren<Graphic>(true))
                graphic.material = _material;
        }
    }
}
