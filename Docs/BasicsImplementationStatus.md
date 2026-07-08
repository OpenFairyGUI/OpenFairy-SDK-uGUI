# Basics / Transition 实现状态

本文记录 FairyGUI `Basics` / `Transition` 示例在 NanamiUI 中的当前覆盖状态。历史开发过程和临时排查结论已整理掉；日常操作流程以 `AGENTS.md` 为准。

## 当前结论

- `Basics` demo 的静态渲染、主要交互、窗口/弹窗、拖拽、滚动、下拉、文本链接、列表填充、翻页动效已完成。
- `Transition` demo 的主要动效已完成，路径、嵌套 transition、滤镜、shake、循环/yoyo 等运行时行为已覆盖。
- 当前公开 Unity 菜单只保留：
  - `Tools/NanamiUI/Migrate`
  - `Tools/NanamiUI/Generate Golden References`
  - `Tools/NanamiUI/Run PlayMode Tests`
- 最近一次验证：2026-07-08 `Migrate` 成功迁移 `101` 个组件，`Run PlayMode Tests` 通过 `60/60`。

## 标准验证流程

1. 修改转换器、生成脚本结构或序列化字段后，先跑 `Tools/NanamiUI/Migrate`。
2. 修改渲染参照、测试目录、FairyGUI 侧 dump 或首次缺少参照时，跑 `Tools/NanamiUI/Generate Golden References`。
3. 最后跑 `Tools/NanamiUI/Run PlayMode Tests`，结果写入 `Temp/NanamiUIPlayModeResults.txt`。

`Generate Golden References` 会同时输出：

- `Assets/Tests/Golden/ReferenceImages/*.png`
- `Assets/Tests/Golden/ReferenceImages/*.geo.json`
- `Docs/RenderDiff/*_strip.png`
- `Docs/RenderDiff/*_fairygui.png`
- `Docs/RenderDiff/*_nanami.png`
- `Docs/RenderDiff/*_diff.png`

这些都是现生成产物，不作为手写源码维护。

## Basics 覆盖状态

| 页面 | 状态 | 备注 |
| --- | --- | --- |
| Main | 完成 | 主页进/退 demo 使用真实 pointer 测试覆盖；翻页 display lock 语义已覆盖。 |
| Button | 完成 | 普通按钮、状态控制器、按下态几何已覆盖。 |
| Image | 完成 | 平铺图相位和翻转九宫格已对齐；仍有图集 duplicate padding / 渐变 banding 类视觉残差。 |
| Graph | 完成 | `GraphDemo` 同时供运行 demo 与截图对比复用；line fillEnd 同步驱动。 |
| MovieClip | 完成 | 运行时自播。 |
| Depth | 完成 | sorting order、动态矩形、拖动行为已覆盖。 |
| Loader | 完成 | 静态 golden 覆盖。 |
| List | 完成 | `ListSource` 已烘焙，动态填项和拖动滚动已覆盖。 |
| ProgressBar | 完成 | demo 值循环已覆盖。 |
| Slider | 完成 | 横/竖拖动几何参照已覆盖。 |
| ComboBox | 基本完成 | button 控制器版本已支持下拉；无 button 控制器的 Dropdown 变体不接下拉。 |
| Clip&Scroll | 基本完成 | ScrollPane 支持拖动滚动；无惯性/回弹/虚拟化。 |
| Controller | 完成 | 静态 golden 覆盖；按名非泛型 controller 面暂未提供。 |
| Relation | 完成 | 静态 golden 覆盖。 |
| Label | 完成 | 静态 golden 覆盖。 |
| Popup | 完成 | PopupMenu + GRoot 覆盖层已支持；外点关闭使用透明 blocker。 |
| Window | 完成 | Window1/Window2、关闭按钮、居中和缩放进出已覆盖。 |
| Drag&Drop | 完成 | agent 拖放、drop payload、dragBounds 已覆盖。 |
| Component | 完成 | 静态 golden 覆盖。 |
| Grid | 基本完成 | demo 填可见文本；star/cb/mc 细节从简。 |
| Text | 基本完成 | UBB 链接点击已覆盖；混合字号断行和行内图片基线仍有小差异。 |

## Transition 覆盖状态

| 页面 | 状态 | 备注 |
| --- | --- | --- |
| BOSS | 完成 | 路径动画、嵌套动效、ColorFilter/Scale 循环逐帧一致。 |
| BOSS_SKILL | 完成 | 视觉与轨迹已对齐。 |
| TRAP | 完成 | Back.Out 旋转链逐帧一致。 |
| GoodHit | 基本完成 | Bounce、Shake、逐字位图字体可用；Shake 随机性不追求逐像素一致。 |
| PowerUp | 基本完成 | Expo 滑入、数字滚动、火焰特效、淡出位移可用；movieclip 帧相位可能有 ±1 帧残差。 |
| PathDemo | 完成 | 贝塞尔路径和无限循环动效逐帧一致。 |

## Runtime / 转换器现状

- Migrate 按 `package.xml` 的 `exported="true"` 发现组件，并通过依赖闭包收集引用组件、图片、movieclip、字体、声音。
- FairyGUI XML 通过 `XmlAttribute` / `XmlElement` / `XmlArrayItem` 标注的 `NanamiUI.Editor.Schema` 类型和轻量自定义 converter reader 反序列化为 `Schema.Package` / `Schema.Component` / `Schema.Display` 等结构化数据，`Vector2`、bool 默认值、派生字段和有限选项 enum 在 schema 层落型，转换器后续逻辑不再直接读取 XML attribute。
- 运行时配置来自 `settings/Common.json` 和可选的 `NanamiUISettings`，本工程限定导出 `Basics`、`Transition`。
- codegen 已支持 C# 关键字转义、controller enum 内嵌、`Button<T>`、`ComboBox<T>`、`ProgressBar`、`Slider` 等基类选择。
- `ListSource`、ComboBox items/dropdown、Window1 列表已通过全量 Migrate 重烘焙。
- Basics demo 的工程胶水通过 `[MigratePostProcess]` 自动配置，不再作为菜单入口暴露。

## 已知边界

- Popup/ComboBox 外点关闭使用透明 blocker，表现为模态；这是为了避开新 Input System 下旧 `UnityEngine.Input` 的异常。
- `ScrollPane` 只实现拖动滚动，无惯性、回弹、虚拟化。
- ComboBox 下拉当前撑开显示全部项，未按 `visibleItemCount` 裁剪滚动。
- Grid demo 主要填可见文本，部分装饰/交互细节从简。
- 文本仍有少量 UBB 混合字号断行、行内图片基线差异。
- GoodHit/PowerUp 中的 Shake 和部分 movieclip 相位差会放大像素 diff，但视觉表现已对齐。

## 渲染差异说明

以下差异属于当前可接受残差：

- 动态字体图集由两侧各自生成，边缘反锯齿会有 1px 左右差异。
- FairyGUI 图集带 duplicate padding，平铺图片边界可能出现约 1px 内容重叠；NanamiUI 使用独立原图平铺。
- 截图使用不透明背景色，避免 FairyGUI 直通 alpha 与 uGUI 预乘 alpha 在透明背景上的通道差异误判。
- Shake 使用随机偏移，两侧不追求同随机序列。

## 测试覆盖

- 静态 golden：`StaticGoldenTests` 比较 NanamiUI 末帧与 FairyGUI golden。
- 交互几何：`InteractionGeometryTests` 比较 slider、checkbox、按钮按下等交互终态几何。
- 轨迹行为：`PageTransitionTests` 覆盖翻页 display lock 动效轨迹。
- Main 导航：`MainNavigationTests` 覆盖 Main 初始页、20 个入口页真实 pointer 进入、子页面返回 Main，以及进出动效中态和终态。
- Demo smoke：覆盖 ComboBox、Depth/DragDrop、Grid、List、Popup、ProgressBar、Text、Window 等运行 demo 胶水。
- 专项测试：`DragDepthTests`、`TextLinkTests`、`WindowPopupTests`、`MainNavigationTests`。

当前 PlayMode 测试结果为 `60/60` 通过。
