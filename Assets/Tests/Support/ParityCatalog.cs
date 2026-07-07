using System.Xml.Linq;
using UnityEngine;

namespace NanamiUI.TestSupport
{
    public readonly struct ParityPage
    {
        public readonly string Name;
        public readonly string Component;
        public readonly string Package;
        public readonly float Threshold;

        public ParityPage(string name, string component, float threshold, string package = "Basics")
        {
            Name = name;
            Component = component;
            Threshold = threshold;
            Package = package;
        }

        // NUnit 用 ToString 作 test case 名，否则同类型的 case 全叫 "ParityPage"。
        public override string ToString() => Name;
    }

    public static class ParityCatalog
    {
        public const string OutputRoot = "UIProject";

        // 末帧静置帧数（captureDeltaTime=1/60 下约 3.5s），需 ≥ 各页 t0 时长以保证收敛。
        public const int SettleFrames = 210;

        // 静态 golden 集：转换产物在末帧收敛、且两侧都无需 per-demo 运行时胶水的页面。
        // 排除动效轨迹页（Transition_*，属实时双跑层）、需胶水页（Graph 的 PlayGraph / Main 的 btn_Back）、
        // 含循环或墙钟计时器的页（MovieClip 相位 / ProgressBar 定时器）。
        // 阈值 = 2026-07-08 全扫实测差异占比（尾注）向上留余量。golden 是 FairyGUI 末帧、diff 用 >2/255 RGB，
        // 多数页残差落在 <2/255 的 alpha 边缘晕而计为 0。要重调：删 golden → Generate Golden References → 重扫 → 按实测抬阈值。
        public static readonly ParityPage[] StaticPages =
        {
            new("Image", "Demo_Image", 0.02f),              // 实测 0.00%
            new("Depth", "Demo_Depth", 0.01f),              // 0.00%
            new("Loader", "Demo_Loader", 0.02f),            // 0.87%
            new("List", "Demo_List", 0.05f),                // 3.22%
            new("Slider", "Demo_Slider", 0.01f),            // 0.03%
            new("ComboBox", "Demo_ComboBox", 0.01f),        // 0.00%
            new("Clip&Scroll", "Demo_Clip&Scroll", 0.02f),  // 0.58%
            new("Controller", "Demo_Controller", 0.02f),    // 0.22%
            new("Relation", "Demo_Relation", 0.01f),        // 0.00%
            new("Label", "Demo_Label", 0.02f),              // 0.21%
            new("Popup", "Demo_Popup", 0.01f),              // 0.00%
            new("Window", "Demo_Window", 0.01f),            // 0.00%
            new("Drag&Drop", "Demo_Drag&Drop", 0.01f),      // 0.01%
            new("Component", "Demo_Component", 0.02f),      // 0.09%
            new("Grid", "Demo_Grid", 0.02f),                // 0.92%
            new("Text", "Demo_Text", 0.02f),                // 0.00%
            new("Button", "Demo_Button", 0.01f),            // 0.03%
        };

        // 交互几何 case：实例化 Component、定位并驱动 Target 到某状态、快照 Target 子树几何、与 FairyGUI 参照比。
        // NanamiUI 侧只经非泛型 Runtime 面（Slider / IPointerClickHandler）驱动，故生成的 UI.{包} 类型不用可达。
        public enum ActionKind
        {
            SliderValue,     // Param = 目标值；NanamiUI 用合成指针经 OnDrag 达到该值，FairyGUI 设 asSlider.value
            ButtonSelected,  // NanamiUI 经 OnPointerClick（Check/Radio 会置 selected），FairyGUI 设 asButton.selected=true
            ButtonDown,      // NanamiUI 经 OnPointerDown（按住），FairyGUI 设 button controller 到 down 页
        }

        public readonly struct InteractionCase
        {
            public readonly string Name;      // 参照文件名
            public readonly string Component; // 实例化的页面组件
            public readonly string Package;
            public readonly string Target;    // 被驱动 + 被快照的子节点名（相对页面根）
            public readonly ActionKind Action;
            public readonly float Param;
            public readonly float Epsilon;    // 几何容差（px）

            public InteractionCase(string name, string component, string target, ActionKind action, float param, float epsilon, string package = "Basics")
            {
                Name = name;
                Component = component;
                Package = package;
                Target = target;
                Action = action;
                Param = param;
                Epsilon = epsilon;
            }

            public override string ToString() => Name;
        }

        // 首批：Slider 拖到某值（bar/grip 几何随 value 变）、Checkbox 勾选（勾选态子物体显隐+皮肤位移）。
        public static readonly InteractionCase[] Interactions =
        {
            new("Slider_n1_30", "Demo_Slider", "n1", ActionKind.SliderValue, 30f, 1.5f),
            new("Slider_n1_80", "Demo_Slider", "n1", ActionKind.SliderValue, 80f, 1.5f),
            new("Slider_n2_30", "Demo_Slider", "n2", ActionKind.SliderValue, 30f, 1.5f),  // 竖向 slider（barV / 竖向 OnDrag 分支）
            new("Checkbox_n4_on", "Demo_Button", "n4", ActionKind.ButtonSelected, 0f, 1.5f),
            new("Button_n3_down", "Demo_Button", "n3", ActionKind.ButtonDown, 0f, 1.5f),   // Common 按钮按下态（down 皮肤 gearDisplay）
        };

        public static string SafeName(string name) => name.Replace("&", "_").Replace("/", "_");

        public static string PrefabPath(string package, string component) =>
            $"Assets/{OutputRoot}/Assets/{package}/{component}.prefab";

        public static string PrefabPath(ParityPage page) => PrefabPath(page.Package, page.Component);

        public static string ReferenceDir => $"{Application.dataPath}/Tests/Golden/ReferenceImages";

        public static string GoldenPath(ParityPage page) => $"{ReferenceDir}/{SafeName(page.Name)}.png";

        public static string GeometryPath(InteractionCase c) => $"{ReferenceDir}/{SafeName(c.Name)}.geo.json";

        public static Vector2Int PageSize(string package, string component)
        {
            var xml = $"{OutputRoot}/assets/{package}/{component}.xml";
            var parts = XDocument.Load(xml).Root.Attribute("size").Value.Split(',');
            return new Vector2Int(int.Parse(parts[0]), int.Parse(parts[1]));
        }

        public static Vector2Int PageSize(ParityPage page) => PageSize(page.Package, page.Component);
    }
}
