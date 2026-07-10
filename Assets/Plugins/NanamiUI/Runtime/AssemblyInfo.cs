using System.Runtime.CompilerServices;

// 烘焙接线字段（titleText/relatedOwner/_selected 等）对用户 API 面收敛为 internal，
// Migrate（Plugins/Editor → editor-firstpass）、编辑器工具与 parity 测试仍可直写。
[assembly: InternalsVisibleTo("Assembly-CSharp-Editor-firstpass")]
[assembly: InternalsVisibleTo("Assembly-CSharp-Editor")]
[assembly: InternalsVisibleTo("Assembly-CSharp-firstpass")]
[assembly: InternalsVisibleTo("NanamiUI.Tests.PlayMode")]
[assembly: InternalsVisibleTo("NanamiUI.TestSupport")]
