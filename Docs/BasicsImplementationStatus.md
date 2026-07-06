# Basics 页面实现状态

本文件记录 `Assets/FairyGUI/Examples/Basics` 对应到 NanamiUI 示例的覆盖情况。截图对比脚本会把 FairyGUI、NanamiUI、diff 三张图输出到 `Docs/RenderDiff`。

运行方式：Unity 菜单 `Tools/NanamiUI/Capture Basics Render Diff`。

| 页面 | NanamiUI 状态 | 说明 |
| --- | --- | --- |
| Main | 基础完成 | 已生成 Main prefab，并接入 `NanamiUI.Example.BasicsMain`。支持点击按钮进入已迁移页面，Back 返回。 |
| Button | 基础完成 | 支持标题、图标、Common/Check/Radio、hover/down/selected/grayed 状态。示例里的 RadioGroup 和 tab 已接业务逻辑。 |
| Image | 基础完成 | 支持普通图片、九宫格、平铺、flipX/flipY、透明度、置灰。 |
| Graph | 部分完成 | 已生成页面和基础矩形 graph；FairyGUI 示例里的 pie、polygon、line mesh 等运行时代码尚未实现。 |
| MovieClip | 未迁移 | Main 中保留入口；NanamiUI 暂无对应 Demo_MovieClip prefab。 |
| Depth | 未迁移 | Main 中保留入口；NanamiUI 暂无对应 Demo_Depth prefab。 |
| Loader | 部分完成 | 支持静态图片和 movieclip 首帧；动态加载、播放、外部 URL 行为尚未完整实现。 |
| List | 未迁移 | Main 中保留入口；NanamiUI 暂无 List runtime 与 Demo_List prefab。 |
| ProgressBar | 未迁移 | Main 中保留入口；NanamiUI 暂无 ProgressBar runtime 与 Demo_ProgressBar prefab。 |
| Slider | 未迁移 | Main 中保留入口；NanamiUI 暂无 Slider runtime 与 Demo_Slider prefab。 |
| ComboBox | 未迁移 | Main 中保留入口；NanamiUI 暂无 ComboBox runtime 与 Demo_ComboBox prefab。 |
| Clip&Scroll | 未迁移 | Main 中保留入口；NanamiUI 暂无 ScrollPane/裁剪滚动示例。 |
| Controller | 未迁移 | Main 中保留入口；Controller 结构存在，但 Demo_Controller prefab 尚未纳入迁移。 |
| Relation | 未迁移 | Main 中保留入口；基础 relation 已支持一部分，Demo_Relation prefab 尚未纳入迁移。 |
| Label | 基础完成 | 支持 title、titleColor、icon；movieclip icon 取首帧。 |
| Popup | 未迁移 | Main 中保留入口；NanamiUI 暂无 Popup/Window 管理实现。 |
| Window | 未迁移 | Main 中保留入口；NanamiUI 暂无 Window 示例。 |
| Drag&Drop | 未迁移 | Main 中保留入口；NanamiUI 暂无拖拽逻辑。 |
| Component | 未迁移 | Main 中保留入口；Demo_Component prefab 尚未纳入迁移。 |
| Grid | 未迁移 | Main 中保留入口；NanamiUI 暂无 Grid/List runtime。 |
| Text | 部分完成 | 支持基础动态字体文字、对齐、描边；rich text、link、input text 等交互尚未完整实现。 |

## 截图输出约定

每个页面会生成：

- `{Page}_fairygui.png`
- `{Page}_nanami.png`
- `{Page}_diff.png`

其中 `Clip&Scroll` 会写成 `Clip_Scroll`，`Drag&Drop` 会写成 `Drag_Drop`。
