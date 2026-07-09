using System;

namespace NanamiUI
{
    // 关联控制器驱动（复刻 FairyGUI relatedController）：把 owner 组件上名为 field 的 Controller<T> 结构设到第 page 页。
    // Controller<T> 是泛型 struct，非泛型面（ButtonBase/测试）只能经反射设页——与 Migrate 烘焙控制器同一条路径。
    public static class ControllerBinding
    {
        public static void SetPage(Component owner, string field, int page)
        {
            var fieldInfo = owner.GetType().GetField(field);
            if (fieldInfo == null)
                return;
            var controller = fieldInfo.GetValue(owner); // 装箱 Controller<T>
            var pageProp = controller.GetType().GetProperty("page");
            var values = Enum.GetValues(pageProp.PropertyType);
            if (page < 0 || page >= values.Length)
                return;
            var target = values.GetValue(page);
            if (Equals(pageProp.GetValue(controller), target))
                return; // 已在目标页：不重复跑 gears（复刻 Controller.selectedIndex 的同值早返回；不放进 page setter 是因烘焙需初次应用）
            pageProp.SetValue(controller, target); // setter 内跑 gears
            fieldInfo.SetValue(owner, controller); // 写回 struct 持久化 _page
        }
    }
}
