# AGENTS.md

本项目的 AI 协作约定。开始工作前请先阅读本文件。

## 环境

- **代码检索**: 如果当前目录下存在 .codegraph 目录，请优先使用 codegraph MCP 来检索代码。否则运行 `codegraph init` 初始化 (可能用时较长请耐心等待)。如果连 codegraph 命令行也不存在，用 npm 安装：
  - `npm i -g @colbymchenry/codegraph`
- **命令行工具**：系统已安装 `ripgrep`（`rg`）和 `coreutils`，有需要请直接调用。如果命令找不到，用 winget 安装：
  - `winget install BurntSushi.ripgrep.MSVC`
  - `winget install uutils.coreutils`
- **项目类型**：这是一个 **Unity** 项目。你应该已经连接到 unity mcp；如果没有，请提示用户安装。使用 unity mcp 执行代码时请注意 unity mcp 禁止 System.Reflection 命名空间。

## 项目目标

本项目的目标是实现是一个 FairyGUI 的三方 SDK，叫做 NanamiUI，基于 uGUI。希望能完美还原 FairyGUI 的效果，让用户可以继续用 FairyGUI Editor 做编辑器，用基于 uGUI 的 NanamiUI SDK 做 Runtime。
每个组件转换后是一个 prefab。
可以像 FairyGUI 自带的生成代码功能那样根据 FairyGUI 工程结构生成出对应的 C# 代码。

## 目录结构

待转换的 FairyGUI 项目目录在 UIProject。
插件脚本根目录在 Assets/Plugins/NanamiUI。有下列子目录：
Editor：转换的脚本，命名空间为 NanamiUI.Editor。
Runtime：运行时 SDK，命名空间为 NanamiUI。包含基础组件的定义，例如 Controller 等。

转换生成的产物根目录应该在 Assets/{FairyGUI项目名，这里叫UIProject}。有下列子目录：
Assets：需要用到的字体、图片等资源文件，也包含转换后的组件，每个组件是一个 prefab。保持以前的目录结构。
Scripts：codegen 生成的代码。

## 代码结构
SDK (Assets/Plugins/NanamiUI/Runtime) 里包含每个基础组件的定义，跟 FairyGUI SDK 里的 GObject 子类 1:1 对应，有 Text、Button、Label 等。
他们都是 UIBehaviour 的子类，也可以继承 UnityEngine.UI.Selectable、UnityEngine.UI.Button 这些更具体的，看怎样弄会让SDK代码最简单。
Controller 是 struct Controller<T> where T : struct, Enum，不是 MonoBehaviour。
Gear 及其子类也不是 MonoBehaviour，并且是泛型，泛型类型是对应 Controller 的 enum。
Button 也是泛型，泛型类型是对应 Controller 的 enum。
Assets/Editor/NanamiUI/BasicsRenderDiff.cs 截图脚本，保存截图到 Docs/RenderDiff
Docs/BasicsImplementationStatus.md 是目前的进度，开始工作前要读取它以获知目前进度状态，有新的进展后也应更新这个文件。

## codegen
类似于 FairyGUI 自带的 codegen 功能一样，NanamiUI 也可以根据 FairyGUI 工程结构，预先生成脚本。
每个工程里自定义组件会生成出一个类，继承自 Component。里面会有每个子物体的引用，通过 prefab 序列化的方式预先挂上去，字段名加 m_ 前缀。
每个 Controller 不再生成单独的类。组件类里直接生成一个同名 enum，枚举值对应 Controller 的每个页；组件类里再生成 Controller<该 enum> 字段，字段名加 m_ 前缀。
Button 组件继承 Button<对应 Controller enum>。

## 代码风格

**最简化。** 核心原则是用最少的代码把功能实现出来。

- 能少写就少写，优先最直接的实现。
- 尽量少封装，不要为了"以后可能用到"而提前抽象。
- **不写防御性代码**：假设传入的数据合法，不做多余的空判断、参数校验、异常兜底。
- 不写无关的注释、日志和配置。
- 与周围代码保持一致的命名和风格。

**现代化** 使用最新的写法。
- 使用当前 Unity 版本能够支持的最新的写法。

## 验证与调试

**渲染对比**：Unity 菜单 `Tools/NanamiUI/Capture Basics Render Diff`（脚本 `Assets/Editor/NanamiUI/BasicsRenderDiff.cs`）进 Play 模式，把 FairyGUI 参照与 NanamiUI 产物逐页同帧渲染对比，输出到 `Docs/RenderDiff/`：
- `{page}_strip.png`：动效胶片条，上排 FairyGUI / 中排 NanamiUI / 下排像素 diff，列=采样时刻，用来看动效轨迹与相位是否一致。
- `{page}_fairygui|_nanami|_diff.png`：末帧全分辨率三联图，看静态精度。

改完组件/转换/动效后跑一次对着 diff 确认。diff 里红色=差异；**~1px 文字/精灵边缘晕是已知残留**（FairyGUI 直通 alpha vs uGUI 预乘 + 亚像素采样），可忽略。Shake 用随机数，两侧无法逐像素对齐，也不该追。

截图脚本直接 `UIPackage.CreateObject` 建 FairyGUI 参照视图，**不跑官方 demo 的初始化**，所以凡是 demo 里 `UIConfig.*` 配的东西都要在脚本里补上，否则产生假 diff。已补：`defaultFont` 以及滚动条资源 `UIConfig.verticalScrollBar/horizontalScrollBar = "ui://Basics/ScrollBar_VT|HZ"`（不补 FairyGUI 侧就没滚动条、viewport 用满宽）。

**动效同步的坑（重要）**：`Time.captureDeltaTime` 只锁 `Time.deltaTime`，**不锁 `Time.unscaledDeltaTime`**（已实测坐实）。FairyGUI 的 GTweener/Transition 默认 `ignoreEngineTimeScale=true` 用 unscaledDeltaTime、按真实墙钟推进；NanamiUI Transition 用 deltaTime。在确定性截图（captureDeltaTime）下两者会脱同步，表现为 FairyGUI 动效"一闪而过、中段帧空白"。对比脚本已在播放前把 FairyGUI transition 的 `ignoreEngineTimeScale` 设为 false 规避（会传播到 nested）。这跟 Player Settings 的 Run In Background 无关。另注：生产中若游戏改 `timeScale`，两侧语义仍分歧（NanamiUI 跟 timeScale、FairyGUI 不跟），要完全对齐或需让 NanamiUI Transition 改用 unscaledDeltaTime。

**文字排版**：`NanamiUI.Text` 在 `OnPopulateMesh` 里复刻 FairyGUI 公式（不用 TextGenerator 布局）。改排版要对照 `Assets/FairyGUI/Scripts/Core/Text/{TextField,DynamicFont}.cs`。

**平铺图的相位**：tile 图导入为独立贴图 + `wrapMode=Repeat`，Unity `Image.Type.Tiled` 会走单 quad repeat 优化（UV 从 0 铺到 N=瓦片数），但它把**纵向残缺格锚在顶部**；FairyGUI 从内容左上角起铺、残缺格落在右下。所以**任何分数格高度的平铺图（翻转与否）纵向都会差半格**（不只翻转的，同页不翻转的也偏，别被淡边缘晕骗了——要用逐像素/互相关量化验证）。`FlipImage.OnPopulateMesh` 因此对所有 `Type.Tiled` **自己生成单 quad**：`nx/ny = rect * multipliedPixelsPerUnit / sprite.rect`，flip 并进 UV 起止（`flipX? uv.z:uv.x` 等），再把顶边 `v1` 用 `Mathf.Floor(v1)-v1` 对齐到整格边界，残缺格回到底边。横向原点天然在左边、无需处理。非平铺的 Simple/Sliced 翻转仍走 base + 绕 `GetOuterUV` 镜像顶点。（注：Sliced+flip 即 9 宫格翻转是另一回事，FairyGUI 会调 gridRect，简单翻 UV 尚未完全对齐。）

## 交互几何测试（2026-07-08，交互 parity 层）

静态 golden 已保证"任意状态渲成像素 = FairyGUI"。**交互测试因此只证"到达了正确状态"，比几何不比像素**（组合原理）。产物：`Assets/Tests/Support/GeometrySnapshot.cs`、`InteractionDriver.cs`、`ParityCatalog.InteractionCase[]`、`Assets/Tests/PlayMode/InteractionGeometryTests.cs`、`BasicsRenderDiff` 里的 FairyGUI 侧 dump。

- **快照**：以交互目标子树为根，每节点记 `path/rect(x,y,w,h)/active/text`，**相对目标左上角、页面设计像素、y 向下**（rig-independent，避开画布世界摆位与 y 翻转的跨引擎歧义）。FairyGUI 侧用手动累加 local `x/y/w/h`（不经 LocalToGlobal/LocalToRoot），故与 stage/GRoot/UIContentScaler 缩放无关。
- **参照 = FairyGUI**，由 `Generate Golden References` 同一趟生成 `{case}.geo.json`（gitignore、按需现生成、缺失即失败）。NanamiUI 侧现算再比。
- **比对**：要求 **参照 ⊆ 实测**（FairyGUI 每个节点都要在 NanamiUI 存在且几何/显隐/文本一致）；NanamiUI 多出的节点（描边/阴影/遮罩等渲染辅助）被忽略。几何容差 `epsilon`（默认 1.5px，暂定）。
- **驱动**：NanamiUI 只经非泛型 Runtime 面（`Slider`、`IPointerClickHandler`），因为测试程序集**够不到生成的 `UI.{包}` 类型**（在 Assembly-CSharp）。首批：Slider 拖到值（合成指针经真实 `OnDrag`，pressEventCamera 靠 rig 画布上的 `GraphicRaycaster`）、Checkbox 勾选（`OnPointerClick`）。

### 留给你看的判断/取舍（有争议，未与你确认直接定的）

1. **Controller 按名切页驱动：暂缓。** `Controller<T>` 是泛型 struct 字段（`m_{name}`），加上生成类型不可达，测试程序集无法泛型地按名字切页。要么反射、要么给 SDK 加个非泛型的按名取 controller 面（对标 FairyGUI `GComponent.GetController(name)`）。**我选暂缓**，放到 SDK 完善阶段一起做（那时顺手补 `GetController`）。注：Button 已在内部驱动自己的 controller+gears，故按钮态/其 gear 终态已被间接覆盖。
2. **Slider：几何 parity 与"指针→value 公式"分开了。** 几何测试用"由 NanamiUI 自己 OnDrag 反函数算出的指针"驱动——它验证了 `OnDrag`+`Apply`+ScreenPoint↔Local 往返、以及 value→bar 几何 = FairyGUI；但**没有**独立地拿 FairyGUI 校验"指针位置→value"公式本身（那需要 FairyGUI 自己的输入管线）。若要补，另加一条纯逻辑断言。
3. **不做命中测试、不做像素 spot-check（你定的）。** 因此"连续态几何真能被正确渲成像素"这一步不再验证，信静态 parity。
4. **FairyGUI 侧 dump 是同步一帧完成**，假设交互终态是瞬时的（slider value、checkbox 显隐）。**带缓动的** FairyGUI 交互态（如按钮 down 若挂 transition）需要在 dump 侧推帧，当前未做。
5. **快照暂无 color/alpha**，只有 rect+active+text。GearColor/GearLook 的 tint/alpha parity 等加对应 case 时再补字段。
6. **epsilon 默认 1.5px 是暂定值**，跑通后按实测收紧（同静态阈值的做法）。

### SDK 修复：asmdef 拆分导致 gears 运行时反序列化为 null（2026-07-08）

交互测试第一跑就抓到一个真 bug：点 checkbox → `Controller.page` setter 遍历 gears → **NRE**。根因：把 NanamiUI 从 `Assembly-CSharp-firstpass` 拆进 `NanamiUI.Runtime` 后，**拆分之前烘焙的 prefab** 里 `[SerializeReference]` 仍记旧程序集：
`type: {class: 'GearDisplay`1[[...]]', ns: NanamiUI, asm: Assembly-CSharp-firstpass}`。
Unity 在旧程序集找不到类型 → gears 数组长度保留但**每个元素反序列化成 null**（控制台 "Missing types referenced from component ..."）。静态 golden 没抓到，因为它在烘焙期把 gear **结果**烤进 prefab，运行时从不重跑 gears。

- **修法（已用）**：把 36 个 prefab 里 `ns: NanamiUI, asm: Assembly-CSharp-firstpass` 文本改写为 `asm: NanamiUI.Runtime`（全项目仅此一种 firstpass 引用，安全），重导入即解析。`[MovedFrom(sourceAssembly:...)]` 对**泛型** SerializeReference 实测无效，别用。
- **未来**：现在的 `Tools/Migrate` 生成的 gears 直接绑 `NanamiUI.Runtime`，新产物无此问题。**若再见 gears 为 null / 运行时切页 NRE**：要么重跑 Migrate 重新烘焙，要么重复上面的文本改写。
- 提醒：任何把 Runtime 类型再挪程序集/命名空间的改动，都会让已烘焙 prefab 的 `[SerializeReference]`（gears、GearDisplay.partner）失效——挪完必须重烘焙或改写绑定。

### 无头跑 PlayMode 测试

`Assets/Editor/NanamiUI/TestRunner/`（独立 asmdef `NanamiUI.EditorTestRunner`，引 `UnityEditor/UnityEngine.TestRunner`）。菜单 `Tools/NanamiUI/Run PlayMode Tests`（或 MCP `ExecuteMenuItem`）跑全部 PlayMode 测试，结果写 `Temp/NanamiUIPlayModeResults.txt`（每行 `状态 全名 :: 消息`）。回调经 `[InitializeOnLoad]` 每次域重载重注册，跨进 Play 存活。

### SDK 修复：切页 display lock（旧页飞出再消失，2026-07-08）

用户实测发现：Main 点进 Demo / 点 Back 时，FairyGUI 旧页"飞出去再消失"，NanamiUI"立即消失、新页飞入"。**测试全绿却没抓到**——因为 Main 不在测试集、且交互几何测试只比 settle 后的终态（端点相同：旧页最终都不在了），**动效轨迹是另一根轴**。

根因：`Main.xml` 里 container/btns 组同时有 `gearDisplay`（切页时隐藏）+ `gearXY tween`。FairyGUI 用 **display lock**（`GearXY.Apply` 里 `AddDisplayLock` → tween 结束 `ReleaseDisplayLock`）令"本应隐藏"的对象在 tween 期间保持显示、结束后才隐藏。NanamiUI 的 `GearDisplay.Apply` 直接 `SetActive(false)` → 立即消失。

已复刻（`GearDisplay`/`Gear`/`GearXY`/`GearSize`/`Controller`）：
- `IDisplayGear` 加 `AddLock/ReleaseLock/Target`；`GearDisplay` 用锁计数，`_locks>0` 时即使 `on=false` 也保持 `SetActive(true)`。
- tween gear（GearXY/GearSize）起 tween 前，若同 target 有 GearDisplay 则 `AddLock`，OnComplete/Kill 时 `ReleaseLock`（`KillTween` 显式释放，避免 DOTween.Kill 不触发 OnComplete 导致锁泄漏）。
- `Controller.page` 切页时：先给每个 tween gear 注入同 target 的 GearDisplay，**先 apply 非 display gear（加锁 + 起 tween），再 apply display gear**（锁已就位 → 被锁 target 不立即隐藏），复刻 FairyGUI `HandleControllerChanged` + `CheckGearDisplay` 的顺序。
- **GearLook 暂未加锁**（alpha/rotation tween，Main 不涉及）；需要时按同法补。

验证：`PageTransitionTests`（PlayMode）驱动 Main c1 0→1，逐帧断言 btns 滑出途中保持 active、到位后才隐藏、container 滑入途中已显示。轨迹实测正确（btns f0-16 active 滑出、f17 到 x=1159 才隐藏）。23/23 通过。

### 测试盲区教训 & 轨迹测试

端点几何测试**测不到动效轨迹**（旧页飞出 vs 立即消失，终态相同）。已加 `PageTransitionTests` 作行为断言（NanamiUI-only、确定性、编码 FairyGUI 语义）覆盖此类。**通用 FairyGUI-同步轨迹对比**（逐帧几何 vs FairyGUI）已确认可行：FairyGUI gear tween 在 `GTweener.defaultIgnoreEngineTimeScale=false` 时用 `Time.deltaTime`（被 `captureDeltaTime` 锁定 → 确定性），可像 transition 那样在 BasicsRenderDiff 里推帧采样存参照。属下一步基建。

### 新增运行时：Depth(z序) + Draggable + DragDrop（2026-07-08，Demo 页 Depth/Drag&Drop）

按 blocked-subsystems 研究的 roadmap 做的头两项（与 GRoot 无关、可独立测）：
- `SortObject`(承载 order) + `Depth`(静态)：`SetSortingOrder`/`SortIndex` 复刻 FairyGUI GComponent 兄弟序（order 0 保插入序在前，order>0 升序在后，等值稳定；uGUI later sibling=画上面），`CreateRect` 运行时建 Shape 矩形（复刻 GGraph.DrawRect）。
- `Draggable`(统一超集)：保偏移拖动 + 逐轴 RoundToInt（y-down 空间钳制再取整）；`onDragStart` 返回 true=PreventDefault（改走 agent）；`dragBounds`(parent-local,y-down) 钳制。Depth 用其朴素路径，DragDrop 用其 agent 路径。
- `DropTarget` + `DragDropManager`(单例)：拖一个跟随指针、`raycastTarget=false`、置顶层 canvas 最后兄弟的 agent 图标；松手 `GraphicRaycaster.Raycast` 找 `DropTarget` 派发 sourceData。`Button.Icon` get/set 即 c.icon 收发面。
- Demo 胶水：`Example/BasicsMain` 加 `PlayDepth`/`PlayDragDrop`（反射 UI.Basics.* 字段，复刻 FairyGUI PlayDepth/PlayDragDrop）。
- 测试 `DragDepthTests`（4 例，直接驱动 Runtime，终点分析精确故无需 FairyGUI 参照）：sortingOrder 兄弟序、自由拖终点、dragBounds 钳制、drop 传递。**drop 测试用 Screen-Space-Overlay 画布**（真实 demo 配置），因 golden rig 的 WorldSpace+禁用相机下 `GraphicRaycaster.Raycast` 命中不可靠。
- **rig 加了 EventSystem**（`NanamiPageRenderer`）：drop 命中 raycast 要 `EventSystem.current` 非空。**不能加 StandaloneInputModule**（读旧版 `UnityEngine.Input`，本工程用新 Input System 会每帧抛异常、拖垮全部测试）——测试都直接调 handler，无需输入模块。
- **27/27 通过**（17 静态 + 5 交互 + 1 翻页 display-lock + 4 拖放/深度）。

### 剩余 roadmap（GRoot 覆盖层 → Window → PopupMenu；List/ComboBox；Text 链接）— 有实现级 spec，未做

一个研究 workflow 产出了各阻塞子系统的实现级 spec（FairyGUI 机制+源码行号、Runtime 类签名、bake 需求、demo 胶水、测试方案、依赖、风险）。**优先序**（按依赖+影响/成本）：
1. **GRoot 覆盖层 + `ButtonBase`/`IButton` 非泛型面**（共享基石，M）：根 Canvas 下全屏(1136×640 **设计**尺寸,非屏幕像素)host，自带 `Canvas(overrideSorting,高 sortingOrder)+GraphicRaycaster` 复用场景 EventSystem；popup 栈、CheckPopups(LateUpdate+RaycastAll 外点关闭,无 blocker,`_justClosed` 防同触重开)、Center、模态层；**必须有 `Create(designRoot)` seam** 让 PlayMode rig 指到 rig canvas。把 `onClick` 上移到非泛型 `ButtonBase` 令关闭按钮/菜单项无需反射。
2. **PopupMenu（+ ListSource bake，L）**：`Migrate.BuildList` 给 list 节点挂 `ListSource(itemPrefab,itemSize,gap)`（对空/动态 list 必需，对已工作的静态 list 无害），运行时按它实例化项；GetPopupPosition(down/flip-up/right-align)+栈去重+外点关闭。瞬时无 tween → 端点几何测试精确。
3. **GRoot-Window（M）**：ShowWindow/HideImmediately、contentPane/Center、closeButton；**Window2 缩放进出的 DOTween** 是翻页之后主要的动画 parity 风险。
4. **List/ComboBox 滚动、Text onClickLink、ProgressBar 值循环**：spec 里 GList-scroll/Text-links-progress 两个 agent 超 schema 重试上限没产出，需另补；ComboBox 下拉依赖 GRoot popup + ListSource。

**隐藏动画 bug 待查（同 display-lock 类）**：Window Hide 必须整段 DOScale 后才 HideImmediately（否则瞬间消失，同"旧页飞出"）；Window2 SetPivot 要做 rect 保持补偿（同 Transition.ApplyInstant）；PopupMenu 重开的 Layout 要用 ctor 里存的 authored 高度重算（否则累加）；WindowB 的 t1(repeat 11 yoyo) OnShown 播/OnHide 停。

## 运行 Demo parity 全量完成（2026-07-08）——GRoot/Window/Popup/Scroll/Grid/ComboBox/Text/ProgressBar

前述"剩余 roadmap"已全部实现。**运行 Basics Demo 的各页交互现已复刻**，PlayMode 39/39 通过（含 6 个 DemoSmokeTest 端到端集成 + WindowPopupTests + TextLinkTests + DragDepthTests + 静态/交互/翻页）。

新增 Runtime（`Assets/Plugins/NanamiUI/Runtime/`）：
- `ButtonBase`（非泛型按钮基类，onClick+Title 上移）：让 GRoot/Window/PopupMenu 无需反射即可挂 onClick/设 Title；`Button<T> : ButtonBase`（序列化按字段名，旧 prefab 兼容）。
- `GRoot`（覆盖层单例，`Create(designRoot)` seam）+ `PopupDirection` + `PopupBlocker`：popup 栈、`ShowPopup`(GetPopupPosition down/flip-up/right-align)、`Center`、`ShowWindow`/`HideWindowImmediately`。**外点关闭用透明 blocker（IPointerClickHandler）而非输入轮询**——见争议 1。
- `Window`（+ Example 的 `NanamiWindow1/2`）：`Show/Hide`、Center、BindCloseButton；Window2 绕中心缩放进出（`SetPivotKeepPosition` + DOTween，隐藏整段 tween 后才 HideImmediately）+ 播内容 transition t1。
- `PopupMenu`：内容 pane + "list"，AddItem/Show/Layout(ResizeToFit)；itemPrefab 显式传入（避开重烘焙 ListSource）。
- `ScrollPane`（`Attach(scrollRoot)` 自配置）：把 viewport 子节点包进 content，拖动在边界内平移 + 移动滚动条 grip。**仅拖动滚动，无惯性/虚拟化**。
- `Text.onClickLink` + UBB `<a href>` 命中：Layout 累积链接矩形，`IPointerClickHandler` 命中即回调。

Demo 胶水（`Example/BasicsMain` + `BasicsExample` 序列化 prefab）：PlayWindow/PlayPopup/PlayProgressBar(值循环)/PlayText(链接+拷贝)/PlayGrid(填表)/PlayComboBox(下拉)/AttachScrollPanes；`Clickable` 中继。MovieClip 运行时自播（`Update` 推帧，`playing=true`），无需胶水。

### 争议/取舍（我替你定的，供你审阅）
1. **Popup/ComboBox 外点关闭用透明 blocker**（不是输入轮询）：本工程用**新 Input System**，读旧 `UnityEngine.Input` 会抛异常（`StandaloneInputModule` 亦然，测试 rig 也不能加）。blocker 令 popup **表现为模态**（点外面先被 blocker 吃掉、不透传到底层），与 FairyGUI 非模态略有差异，但"点外面即关"一致。要完全非模态需接新 Input System 或 FairyGUI 式全局捕获。
2. **ComboBox 下拉项在 demo 胶水里硬编码**（Item 1..N），因为 `<ComboBox>` 的 items 没被烘焙进生成组件。通用做法要改 `Migrate` 烘焙 items + 加 `NanamiUI.ComboBox` 运行时 → **需重跑 Migrate**（本会话一直规避重烘焙以免扰动已过的静态 parity）。同理 ComboBox 的 hover/press 视觉态未接（它不是 Button，无 pointer handler）。n4/n5（Dropdown 变体）未接，只接 n1/n6。
3. **ScrollPane 仅拖动滚动**：无惯性/回弹/虚拟化；运行时把 viewport 子节点包进 content 容器（不改烘焙）。
4. **Grid 填表**只设可见文本（t0/t1/t2/t3）；star/cb/mc 细节从简。数据随机、无法逐像素比，故只有 smoke（列表被填）。
5. **Window1 的 6 项列表留空**（需 GList 填充，同 Grid 可补）；窗体/组合框/关闭按钮照常显示。
6. **未重烘焙**：ListSource 通用动态列表、ComboBox items 烘焙都因规避重烘焙而未做——它们是"改 Migrate + 重跑"的一类，留给你决定是否重烘焙。

## 重烘焙完成（2026-07-08）——ComboBox items / ListSource / Window1 列表

之前标记"需重跑 Migrate"的都做了，**已实跑 `Tools/Migrate` 全量重烘焙**，重烘焙后 PlayMode 仍 **39/39**（静态 parity 完整保住，gears 直接绑对 `NanamiUI.Runtime`，text-rewrite 修复已成历史）。

- **`NanamiUI.ComboBox<T> : Button<T>`**（复用 up/down/over 视觉态 + hover/press，覆写 `OnPointerClick` 弹下拉）。Migrate：`baseType` 加 ComboBox 分支（有 button 控制器才做成 `ComboBox<T>`）；`ConfigureComboBox` 组件级烘焙 `dropdownPrefab`（ComboBoxPopup 资源）、实例级烘焙 `items`（`<item title>`）+ 默认标题；`Collect` 增补跟随根 `<ComboBox dropdown>`（否则 ComboBoxPopup 不被收集/构建）。下拉用 ComboBoxPopup 的 "list" + 其 `ListSource` 填 items、撑开容纳。demo 胶水里的硬编码 ComboBox/`Clickable` 已删。
- **`ListSource` bake**：`Migrate.BuildList` 给每个 list 节点挂 `ListSource(itemPrefab/itemSize/gap/layout)`；`GList.Fill` 据此运行时建项。Window1 的列表现由 ListSource 填 6 项（不再留空）。
- **踩坑（已解）**：`Dropdown` 组件有名为 "button" 的子节点且无 `<controller>` → 占位 enum 撞名 → `ComboBox<Dropdown.button>` 循环基类 CS0146。解法：ComboBox **无 button 控制器时退化为 Component**（Dropdown 即 Component）。故 Demo_ComboBox 的 n4/n5（Dropdown 变体、无控制器）仍不接下拉——是仅剩的 ComboBox 边角。
- **Migrate 两阶段坑**：脚本有变（新增 ComboBoxPopup）时 Execute 置 `Pending` 后等域重载再建 prefab；**期间别插 MCP RunCommand**（会打断 `[DidReloadScripts]` 的 delayCall，实测 pending 卡 True）。脚本无变时一趟直建。

### 仍存的取舍（非阻塞，最佳判断）
1. Popup/ComboBox 外点关闭用透明 blocker（新 Input System 不能读旧 `UnityEngine.Input`）→ 表现为模态。
2. ScrollPane 仅拖动无惯性/虚拟化；ComboBox 下拉撑开容纳全部项、不裁剪滚动（n6 的 visibleItemCount 上限未做）。
3. Grid 填表只设可见文本（star/cb/mc 从简）；ComboBox 的 Dropdown 变体（n4/n5，无控制器）不接下拉。
