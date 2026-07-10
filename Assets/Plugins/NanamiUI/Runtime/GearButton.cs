using System;

namespace NanamiUI
{
    // 复刻 GButton.HandleControllerChanged：关联控制器换页（含程序化设页）时同步按钮选中态。
    // 由 Migrate 为每个 <Button controller=.. page=..> 烘进该控制器的 gears，pages 即按钮的 relatedPage 页。
    [Serializable]
    public class GearButton<T> : Gear<T> where T : struct, Enum
    {
        [NonSerialized] private ButtonBase _button;

        public override void Apply(T page)
        {
            if (_button == null)
                target.TryGetComponent(out _button);
            if (_button != null && _button.mode != ButtonMode.Common)
                _button.selected = Array.IndexOf(pages, page) >= 0;
        }
    }
}
