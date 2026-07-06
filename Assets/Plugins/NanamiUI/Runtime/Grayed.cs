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
            // 进出 Play Mode 会销毁运行时创建的材质，??= 不走 Unity 的重载判空，须显式判断。
            if (_material == null)
                _material = new Material(Shader.Find("NanamiUI/UI Grayscale"));
            foreach (var graphic in GetComponentsInChildren<Graphic>(true))
                graphic.material = _material;
        }
    }
}
