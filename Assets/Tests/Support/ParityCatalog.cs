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
        // 多数页残差落在 <2/255 的 alpha 边缘晕而计为 0。要重调：删 golden → Promote → 重扫 → 按实测抬阈值。
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

        public static string SafeName(string name) => name.Replace("&", "_").Replace("/", "_");

        public static string PrefabPath(ParityPage page) =>
            $"Assets/{OutputRoot}/Assets/{page.Package}/{page.Component}.prefab";

        public static string ReferenceDir => $"{Application.dataPath}/Tests/Golden/ReferenceImages";

        public static string GoldenPath(ParityPage page) => $"{ReferenceDir}/{SafeName(page.Name)}.png";

        public static Vector2Int PageSize(ParityPage page)
        {
            var xml = $"{OutputRoot}/assets/{page.Package}/{page.Component}.xml";
            var parts = XDocument.Load(xml).Root.Attribute("size").Value.Split(',');
            return new Vector2Int(int.Parse(parts[0]), int.Parse(parts[1]));
        }
    }
}
