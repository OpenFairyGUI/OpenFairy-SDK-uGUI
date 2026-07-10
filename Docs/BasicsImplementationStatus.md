# Basics / Transition 实现状态

本文记录 FairyGUI `Basics` / `Transition` 示例在 NanamiUI 中的当前覆盖状态。历史开发过程和临时排查结论已整理掉；日常操作流程以 `AGENTS.md` 为准。

## 当前结论

- `Basics` demo 的静态渲染、主要交互、窗口/弹窗、拖拽、滚动、下拉、文本链接、列表填充、翻页动效已完成。
- `Transition` demo 的主要动效已完成，路径、嵌套 transition、滤镜、shake、循环/yoyo 等运行时行为已覆盖。
- 当前公开 Unity 菜单只保留：
  - `Tools/NanamiUI/Migrate`
  - `Tools/NanamiUI/Generate Golden References`
  - `Tools/NanamiUI/Run PlayMode Tests`
- 最近一次验证：2026-07-10 `Migrate` 成功迁移 `101` 个组件，`Run PlayMode Tests` 通过 `134/134`；静态 golden、真实射线交互、转换完整性、池化复用及 `RelationSide` 全枚举回归均通过。
- 2026-07-10 依赖替换：`TransitionPath` 的曲线求值改用官方 `com.unity.splines`（直线三等分控制点、二次贝塞尔升阶、CR 样条按标准 Catmull-Rom→三次贝塞尔换算，均精确等价，段长权重保持 FairyGUI 口径；1001 采样点新旧最大偏差 6.3e-5 px）；静态 golden 测试的像素对比改用官方 `com.unity.testframework.graphics` 的 `ImageAssert`（`IncorrectPixelsCount`+`DeltaGamma`、`PerPixelGammaThreshold=2/255`，口径与旧手写 `DiffRatio` 一致；截图/参照统一 ARGB32）。`Docs/RenderDiff` 的人工查看 diff 条仍由 `BasicsRenderDiff` 自产。测试失败时框架会写 `Assets/ActualImages/`（已 gitignore）。
- 本轮发布整改：修 `ComboBox.selectedIndex` 程序化赋值不刷新/不发事件的 bug（改属性 + 加 `values/text/value`，并支持从 XML `<item value>` 烘焙字符串值、程序化设置 `text`/`value`）+ 下拉按 `visibleItemCount` 裁剪滚动；`ScrollPane` 补滚轮/滚动条 grip 拖动/惯性回弹/`onScroll`/`ScrollToView`；`List.Fill` 在已挂 ScrollPane 后重填会保留并刷新 `__scrollHit`，同时自动重绑 `ListSelection` 且避免重复 listener；`ProgressBar.TweenValue` 加 ease、`Slider` 加 grip touch 事件；修 `MovieClip.SetFrame` 越界不钳导致自播 Update 越界的 bug；`Window.modal` + `Root` 模态层；`PopupMenu.AddItem` 返回项按钮 + `ClearItems`；`TextInput` 加 `password`/`maxLength`/`editable`/`onSubmit`。转换器健壮性整改（对非 demo 的真实工程）：包名/空名 sanitize 进命名空间、非 button list item、陈旧 gear 页 id、`Image`/`RectTransform` 字段全限定、内嵌 `ui://` 容错、生成控制器 enum 与 runtime 基类成员同名时显式 `new` 消除隐藏警告。
- 本轮性能优化：参照 FairyGUI `GObjectPool` 思路，`ListSource` 基于 `UnityEngine.Pool.ObjectPool<GameObject>` 复用动态列表项，`PopupMenu` 复用菜单项；`ComboBox` 下拉改为禁用 prefab 自带 `ListSelection`/`ScrollPaneHost`，并调用 `List.Fill(..., false)` 跳过普通列表选择重绑。进一步把 TextField 逐字符数据从整份 `Run` 复制压缩为 run 索引 + 缓存 advance，`Relation` 同帧合并 RectTransform 写入，列表项每轮只扫描/复位一次按钮；`Transition.Play`/`PlayReverse` 与 `MovieClip.Play` 统一返回 `UniTask`，删除平行完成回调；Popup 关闭改用池化 `AutoResetUniTaskCompletionSource`；PopupMenu 项点击统一走 `ButtonBase.onClick`；四种 tween Gear 的 tweener/display lock/完成委托生命周期上提基类复用。
- 2026-07-10 工程打磨：`FairyXml` 按 Schema 类型缓存反射字段/特性和 enum alias，避免每个 XML 节点重复扫描；生成脚本旧 `Text/Shape/InputText` 与裸 `Image/RectTransform` 死锁遗留已清理，并加全目录回归；全工程移除 `GetComponent() ?? AddComponent()`，规避 Unity 6.4 `UnityEngine.Object` 假 null 导致的 `MissingComponentException`；FairyGUI vendored SDK 的对象查找调用升级到 Unity 6.4 非弃用 API；PlayMode 结果文件会附失败堆栈。
- 真跑才暴露的三个 bug（已修 + 新增真射线交互测试 `InteractionRuntimeTests`）：① MovieClip 页空白（`<jta>` movieclip 标签被丢弃，现已识别）；② Popup/Window 按钮点不动（button 根加透明 raycast 面，整块可点）；③ Text 输入框不工作（真因是 `NanamiUI.TextField` 作 InputField 面时 `OnEnable` 清 raycastTarget + 自绘不填 generator；改用原生 InputField 结构：透明 Image 作 targetGraphic + 普通 UI.Text 作 textComponent，placeholder 仍 NanamiUI.TextField。`activeInputHandler` 保持 `1`）。详见 `AGENTS.md`。
- `InteractionRuntimeTests` 覆盖真射线交互：MovieClip 自播、Window 开关、Popup 开/点项收起、ComboBox 开下拉、Slider 可点跳值、TextInput 可编辑读写。
- 2026-07-09 交互烘焙完整化：让**转换产物在无 demo 胶水下也具备完整交互**（通用 SDK 发布前提）。补齐按钮关联控制器(tab/单选组)、`overflow=scroll` 自挂 ScrollPane、Slider `min/reverse/wholeNumbers/changeOnClick`、列表选择 + `onClickItem`、ComboBox onChanged 语义、输入框 `SetTextWithoutNotify`/只读/回车提交、Window frame 拖动、右键指针弹菜单、GearLook 置灰传按钮、Depth 前移越位修正。新增 `BakedInteractionTests`（直接实例化 prefab、无胶水、真射线驱动）证明通用工程已具交互。详见 `AGENTS.md`「交互烘焙完整化」。
- 当前 `Run PlayMode Tests` `134/134`。范围与取舍详见 `AGENTS.md`「发布范围与取舍」。

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
| List | 完成 | `ListSource` 已烘焙，动态填项改为池化复用，拖动滚动已覆盖。 |
| ProgressBar | 完成 | demo 值循环已覆盖。 |
| Slider | 完成 | 横/竖拖动几何参照已覆盖。 |
| ComboBox | 完成 | button 控制器版本与无 button 控制器的 Dropdown 变体均支持下拉；支持 `items/values/text/value`、`visibleItemCount` 裁剪滚动。 |
| Clip&Scroll | 基本完成 | ScrollPane 支持拖动、滚轮、滚动条 grip 拖动、轨道点按翻页、惯性回弹；无虚拟化/分页吸附。 |
| Controller | 完成 | 静态 golden 覆盖；按名非泛型 controller 面暂未提供。 |
| Relation | 完成 | 静态 golden 覆盖。 |
| Label | 完成 | 静态 golden 覆盖。 |
| Popup | 完成 | PopupMenu + Root 覆盖层已支持，菜单项池化复用；外点关闭使用透明 blocker。 |
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
- `ListSource`、ComboBox items/values/dropdown、Window1 列表已通过全量 Migrate 重烘焙；Example Grid 已改用通用 `List.Fill`，不再自挂 SDK 级滚动能力。动态 `List.Fill` 现复用 `ListSource` item 池，`PopupMenu` 现复用菜单项池。
- Basics demo 的工程胶水通过 `[MigratePostProcess]` 自动配置，不再作为菜单入口暴露。

## 已知边界

- Popup/ComboBox 外点关闭使用透明 blocker，表现为模态；这是为了避开新 Input System 下旧 `UnityEngine.Input` 的异常。
- `ScrollPane` 已支持拖动、滚轮、滚动条 grip 拖动、轨道点按翻页、惯性回弹；仍无虚拟化、分页吸附、下拉刷新。
- ComboBox 下拉已按 `visibleItemCount` 裁剪并滚动（项数超出时 `ScrollPane.Attach`）；无 button 控制器的 Dropdown 变体同样接下拉。
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
- 专项测试：`DragDepthTests`、`TextLinkTests`、`WindowPopupTests`、`MainNavigationTests`、`NewFeatureTests`；其中 `NewFeatureTests` 覆盖 ComboBox 程序化 `text/value` 设置与 `List.Fill` 动态重填后的 ScrollPane/ListSelection 刷新。
- 通用工程交互（无 demo 胶水）：`BakedInteractionTests` 直接实例化转换 prefab，真射线驱动，覆盖 tab 换页、单选组互斥、`overflow=scroll` 自挂滚动、输入框结构、Slider 连续拖动。

当前 PlayMode 测试结果为 `134/134` 通过。
