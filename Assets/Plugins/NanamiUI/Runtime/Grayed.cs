using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace NanamiUI
{
    [ExecuteAlways]
    public class Grayed : UIBehaviour
    {
        public Shader shader; // Migrate 烘焙

        private static Material _material;
        private readonly List<(Graphic graphic, Material original)> _restore = new();
        private readonly List<Graphic> _graphics = new();

        protected override void OnEnable()
        {
            base.OnEnable();
            if (shader == null)
                return; // Migrate AddComponent 时 OnEnable 先于字段注入
            // 进出 Play Mode 会销毁运行时创建的材质，??= 不走 Unity 的重载判空，须显式判断。
            if (_material == null)
                _material = new Material(shader);
            _restore.Clear();
            _graphics.Clear();
            GetComponentsInChildren(true, _graphics);
            foreach (var graphic in _graphics)
            {
                _restore.Add((graphic, graphic.material));
                graphic.material = _material;
            }
            _graphics.Clear();
        }

        // 组件被禁用（如 GearLook 切页取消置灰）→ 还原各 Graphic 原材质。
        protected override void OnDisable()
        {
            base.OnDisable();
            foreach (var (graphic, original) in _restore)
                if (graphic != null)
                    graphic.material = original;
            _restore.Clear();
        }
    }
}
