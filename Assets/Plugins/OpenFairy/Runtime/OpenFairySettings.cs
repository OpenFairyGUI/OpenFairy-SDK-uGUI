using UnityEngine;

namespace OpenFairy.UGUI
{
    // 对应 FairyGUI 游戏代码里的 UIConfig.*：这些值由游戏运行时设置，不在 FairyGUI 工程文件里，
    // 需各工程自行提供。Migrate 读取它来烘焙；留空的字段回退到 settings/Common.json。
    [CreateAssetMenu(fileName = "OpenFairySettings", menuName = "OpenFairy/Settings")]
    public class OpenFairySettings : ScriptableObject
    {
        public string defaultFont;  // UIConfig.defaultFont（运行时字体）
        public string[] packages;   // 要导出的包名（类比 UIPackage.AddPackage）；留空则导出所有 exported 组件
    }
}
