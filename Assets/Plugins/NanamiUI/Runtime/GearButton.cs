using System;

namespace NanamiUI
{
    // 复刻 GButton.HandleControllerChanged：关联控制器换页（含程序化设页）时同步按钮选中态。
    // 由 Migrate 为每个 <Button controller=.. page=..> 烘进该控制器的 gears，pages 即按钮的 relatedPage 页。
    [Serializable]
    public class GearButton<T> : Gear<T> where T : struct, Enum
    {
        public override void Apply(T page)
        {
            if (target.TryGetComponent(out ButtonBase button) && button.Mode != ButtonMode.Common)
                button.Selected = Array.IndexOf(pages, page) >= 0;
        }
    }
}
