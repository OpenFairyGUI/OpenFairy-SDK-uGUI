using UnityEngine;

namespace OpenFairy.UGUI.Example
{
    // FairyGUI 的运行时字体（UIConfig.defaultFont）不在工程文件里：烘焙按 Common.json 的设计期字体落
    // fontNames，运行时由用户代码在实例化后把设计字体替换成目标字体（复刻官方 demo 启动代码的
    // UIConfig.defaultFont = "Microsoft YaHei"）。显式指定字体（Comic Sans 等）与位图字体不受影响。
    public static class DemoFont
    {
        public const string Design = "Heiti SC Medium";
        public const string Runtime = "Microsoft YaHei";

        private static Font _font;

        public static void Apply(GameObject root)
        {
            foreach (var text in root.GetComponentsInChildren<TextField>(true))
                if (text.fontNames == Design)
                {
                    text.fontNames = Runtime;
                    text.font = _font = _font ? _font : Font.CreateDynamicFontFromOSFont(Runtime, 16);
                }
        }
    }
}
