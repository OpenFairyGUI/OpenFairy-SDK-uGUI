using System.Runtime.CompilerServices;

// 烘焙接线字段（titleText/relatedOwner/_selected 等）对用户 API 面收敛为 internal，
// Migrate（OpenFairy.UGUI.Editor）、编辑器工具与 parity 测试仍可直写。
[assembly: InternalsVisibleTo("OpenFairy.UGUI.Editor")]
[assembly: InternalsVisibleTo("Assembly-CSharp-Editor")]
[assembly: InternalsVisibleTo("OpenFairy.UGUI.Tests.PlayMode")]
[assembly: InternalsVisibleTo("OpenFairy.UGUI.TestSupport")]
