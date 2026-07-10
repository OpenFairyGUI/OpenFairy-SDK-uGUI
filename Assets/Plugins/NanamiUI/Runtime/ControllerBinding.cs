namespace NanamiUI
{
    // 关联控制器驱动（复刻 FairyGUI relatedController）：codegen 为每个组件生成 index→Controller<T> 的静态 switch，
    // 非泛型 ButtonBase 无需反射、装箱或字符串字段名即可换页。
    internal static class ControllerBinding
    {
        public static void SetPage(Component owner, int controller, int page) =>
            owner.SetControllerPage(controller, page);

        public static int GetPage(Component owner, int controller) => owner.GetControllerPage(controller);
    }
}
