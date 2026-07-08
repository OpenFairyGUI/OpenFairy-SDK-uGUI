# AGENTS.md

本项目的 AI 协作约定。开始工作前先读本文件，再读 `Docs/BasicsImplementationStatus.md` 了解当前实现状态；有实质进展时同步更新状态文档。

## 环境

- **代码检索**：如果仓库根目录存在 `.codegraph/`，优先用 CodeGraph MCP 的 `codegraph_explore` 理解代码；没有 MCP 时用 `codegraph explore "..."`。没有 `.codegraph/` 才用 `rg`/文件读取。
- **命令行工具**：已安装 `rg` 和 coreutils，直接使用。
- **项目类型**：Unity 项目。应优先通过 unity mcp 执行菜单项；如果当前会话没有 unity mcp，先提示用户安装/连接。
- **注意**：unity mcp 执行代码时不要使用 `System.Reflection` 命名空间。

## 项目目标

NanamiUI 是基于 uGUI 的 FairyGUI Runtime SDK。目标是让用户继续用 FairyGUI Editor 编辑 UI，再用 NanamiUI 将 FairyGUI 工程转换成 uGUI prefab 和 C# 代码，并尽量还原 FairyGUI 的渲染、动效和交互表现。

## 目录结构

- `UIProject/`：待转换的 FairyGUI 工程。
- `Assets/Plugins/NanamiUI/Runtime/`：运行时 SDK，命名空间 `NanamiUI`。
- `Assets/Plugins/NanamiUI/Editor/`：通用转换器和工程 postprocess，命名空间 `NanamiUI.Editor`。
- `Assets/Plugins/NanamiUI/Example/`：Basics demo 的运行时胶水。
- `Assets/Editor/NanamiUI/`：编辑器验证工具、测试 runner。
- `Assets/UIProject/Assets/`：转换生成的资源和 prefab，保持 FairyGUI 包结构。
- `Assets/UIProject/Scripts/`：转换生成的 C# 代码。
- `Assets/Tests/`：PlayMode parity 测试和测试支持代码。
- `Docs/RenderDiff/`：golden 生成时顺便输出的人工查看 diff 图。

## 代码结构

- Runtime 控件命名 = 对应 FairyGUI 控件**精确去掉 `G` 前缀**：`Component`(GComponent)、`Button`(GButton)、`Label`(GLabel)、`ComboBox`(GComboBox)、`Slider`(GSlider)、`Loader`(GLoader)、`MovieClip`(GMovieClip)、`ProgressBar`(GProgressBar)、`TextField`(GTextField)、`TextInput`(GTextInput)、`Graph`(GGraph)、`Image`(GImage)、`List`(GList)、`Root`(GRoot)。历史遗留的 `GList`/`GRoot`/`Text`/`InputText`/`Shape`/`FlipImage` 已统一为 `List`/`Root`/`TextField`/`TextInput`/`Graph`/`Image`。
  - `Line` 不是 FairyGUI 控件（GGraph 内含线绘制），是 Graph 演示用的内部折线渲染 helper，保留原名。`ButtonBase` 是 GButton 的非泛型面（复用其 onClick/Title），非 FairyGUI 类。
  - `List` 与 `System.Collections.Generic.List<T>` 按泛型元数区分共存（`List.Fill` 是本类静态；`List<T>` 仍是 BCL 泛型），不冲突。
  - `Image`(←GImage, `: UnityEngine.UI.Image`)、`TextField`(`: UnityEngine.UI.Text`) 与 uGUI 同基类同短名：NanamiUI 命名空间内**裸 `Image`/`Text` 指本类**，要引用 uGUI 基类须全限定 `UnityEngine.UI.Image`/`UnityEngine.UI.Text`（改名时已把所有裸 uGUI 用法全限定；`Image.Type`/`Image.FillMethod` 等静态访问也要限定）。`Run.Image`(int 字段)、`DisplayKind.Image`/`ResourceKind.Image`(enum) 不是类型引用，别误改。codegen 里 image 字段类型仍发 `UnityEngine.UI.Image`（`NanamiUI.Image` IS-A，GetComponent 取得到），故生成脚本不引用 `NanamiUI.Image`，改名不触发 codegen 死锁。
  - `Root` 与 `Window.Root` 属性同名：`Window.cs` 里引用单例用全限定 `NanamiUI.Root.inst`。
  - 重命名会引发 codegen 死锁坑：runtime 改名后，`Assets/UIProject/Scripts` 里**已生成**的组件脚本仍引用旧类型名 → Assembly-CSharp 编译失败 → 编辑器程序集(含 Migrate)无法重编译 → Migrate 跑的是**旧缓存程序集**，会把生成脚本再刷回旧名，死循环。解法：改名后先 `sed` 把生成脚本 + `Assets/Editor/NanamiUI`(BasicsRenderDiff 用 `using FairyGUI`，易漏) 里的旧类型名一并替换，再 `CompilationPipeline.RequestScriptCompilation(RequestScriptCompilationOptions.CleanBuildCache)` 强制干净重编译(普通 Refresh 不够，增量缓存会残留旧程序集)，确认 0 error 后再跑 Migrate(此时幂等)。
  - `Root` 与 `Window.Root` 属性同名：`Window.cs` 里引用单例用全限定 `NanamiUI.Root.inst` 避开属性遮蔽。
  - `NanamiUI.Editor` 里引用 FairyGUI 的同类须全限定 `FairyGUI.GRoot`/`FairyGUI.GList`（BasicsRenderDiff 已如此）；改名后 NanamiUI 侧是 `Root`/`List`，与 FairyGUI 的 `GRoot`/`GList` 不再同名，天然不遮蔽。
- Runtime 组件是 `UIBehaviour` 子类；需要时可继承 uGUI 更具体的基类，以保持实现简单。
- `Controller<T>` 是 `struct`，约束 `where T : struct, Enum`，不是 `MonoBehaviour`。
- `Gear` 及子类不是 `MonoBehaviour`，按对应 controller enum 泛型化。
- `Button<T>` / `ComboBox<T>` 使用对应 controller enum。
- codegen 为每个自定义组件生成一个类，继承 `Component` 或对应基础组件；子物体引用字段用 `m_` 前缀。
- Controller 不生成单独类：在组件类里生成同名 enum，再生成 `Controller<该 enum>` 字段。

## 代码风格

**最简化**
- 用最少代码完成明确需求，优先直接实现。
- 少封装，不为“以后可能用到”提前抽象。
- 同一语义在不同体系下需要区分时，优先用命名空间或嵌套类型表达归属关系，不靠前后缀制造并列概念。
- 不写防御性代码：假设输入数据合法，不做多余 null 检查、参数校验和异常兜底。
- 不加无关注释、日志、配置或临时菜单。
- 保持周围命名、缩进和 Unity 序列化字段风格一致。

**现代化**
- 使用最新版本的写法，充分利用现代特性。
- 如果最新版本 API 在当前环境下不支持，应该先寻求看有没有办法让它支持 (例如升级环境、安装某个 nuget 包、引入 Polyfill 等)。实在没有办法使用新 API 的情况下才考虑退回旧版本，并且发生了这种情况一定要告知用户。

**静态化**
- 静态结构优于动态解析；不要滥用 `string`。只有真实文本内容才用 `string`，有限选项用 enum，结构化值用 struct/class 字段等能保持静态引用和编译期检查的表达方式。
- 能通过明确的正向函数调用完成的流程，不引入 `Action` 回调等动态分发方式。

## 标准工具入口

Unity 菜单 `Tools/NanamiUI` 下只保留三个公开功能：

1. `Tools/NanamiUI/Migrate`
2. `Tools/NanamiUI/Generate Golden References`
3. `Tools/NanamiUI/Run PlayMode Tests`

不要新增长期存在的调试菜单。临时排查代码在提交前删除，或保留为非菜单 helper。

### Migrate

用途：把 `UIProject/` 转成 `Assets/UIProject/Assets/` 下的 prefab/资源，以及 `Assets/UIProject/Scripts/` 下的生成代码。

AI 操作流程：

1. 通过 unity mcp 执行菜单 `Tools/NanamiUI/Migrate`。
2. 如果本次修改影响生成脚本，Migrate 会设置 `NanamiUI.Migrate.Pending`，等待 Unity 域重载后自动继续构建 prefab；这段期间不要插入其它 Unity 命令打断 `DidReloadScripts` 的 `delayCall`。
3. Migrate 完成后会自动跑 `[MigratePostProcess]`，例如 Basics demo 的 prefab 引用配置，不要手动调用旧的配置菜单。
4. 关注 Console 里的 `NanamiUI migrated ...` 日志；若失败，先修编译错误或转换器错误，再重跑 Migrate。

### Generate Golden References

用途：生成 FairyGUI 参照图和交互几何参照，用于 PlayMode parity 测试；同一趟也会输出人工查看的 render diff。

AI 操作流程：

1. 通过 unity mcp 执行菜单 `Tools/NanamiUI/Generate Golden References`。
2. 工具会自动进入 Play Mode，逐页同帧驱动 FairyGUI 与 NanamiUI，输出：
   - `Assets/Tests/Golden/ReferenceImages/*.png`
   - `Assets/Tests/Golden/ReferenceImages/*.geo.json`
   - `Docs/RenderDiff/*_strip.png`
   - `Docs/RenderDiff/*_fairygui.png`
   - `Docs/RenderDiff/*_nanami.png`
   - `Docs/RenderDiff/*_diff.png`
3. `Assets/Tests/Golden/` 和 `Docs/RenderDiff/` 是现生成产物，按需查看，不要把它们当作手写源码。
4. 文字/精灵边缘约 1px 的红边通常是已知 alpha/亚像素差异；Shake 随机数不追逐像素完全一致。

### Run PlayMode Tests

用途：跑全部 PlayMode parity 测试。

AI 操作流程：

1. 通过 unity mcp 执行菜单 `Tools/NanamiUI/Run PlayMode Tests`。
2. 结果写入 `Temp/NanamiUIPlayModeResults.txt`，完成后读取该文件；第一行是整体状态，失败行带测试名和消息。
3. 若改了转换器或 golden 相关逻辑，先 `Migrate`，再 `Generate Golden References`，最后 `Run PlayMode Tests`。
4. 需要命令行跑 Unity Test Runner 时不要加 `-nographics`，URP 渲染测试需要 GPU。

## 验证原则

- 改 Runtime 渲染、动效、转换器或 demo 胶水后，至少跑 `Run PlayMode Tests`。
- 改会影响参照图/几何参照的逻辑时，先跑 `Generate Golden References` 再跑测试。
- 改转换输出结构、序列化字段或生成代码时，先跑 `Migrate`。
- 测试程序集访问不到生成的 `UI.{包}` 类型时，优先经非泛型 Runtime 面驱动，例如 `Slider`、`ButtonBase`、`IPointerClickHandler`。
- 静态 golden 证明像素终态；交互几何测试证明“到达正确状态”；动效轨迹需专门逐帧行为测试覆盖。

## 重要实现约定

- `BasicsRenderDiff` 直接 `UIPackage.CreateObject` 建 FairyGUI 参照视图，不跑官方 demo 初始化。需要 FairyGUI `UIConfig.*` 的配置必须在脚本里补齐。
- `Time.captureDeltaTime` 只锁 `Time.deltaTime`，不锁 `Time.unscaledDeltaTime`。截图对比中 FairyGUI transition/tweener 要关闭 `ignoreEngineTimeScale` 才能和 NanamiUI 同步。
- `NanamiUI.Text` 自己在 `OnPopulateMesh` 中复刻 FairyGUI 排版公式；改文字布局时对照 `Assets/FairyGUI/Scripts/Core/Text/{TextField,DynamicFont}.cs`。
- 平铺图由 `FlipImage.OnPopulateMesh` 自己生成 tiled quad，以匹配 FairyGUI 从左上铺、残缺格落在右下的相位。
- Runtime 类型移动程序集/命名空间会破坏 prefab 中 `[SerializeReference]` 的 gear 类型绑定；移动后必须重烘焙或迁移 serialized type。

## 发布范围与取舍（AI 经验记录）

本 SDK 走 **烘焙优先（bake-first）+ 复用 uGUI 原生**，不复刻 FairyGUI 的动态运行时对象模型。判定新需求是否要做时，先套下面的范围结论，别照搬 FairyGUI runtime API 清单。

**明确不做（用户已定，别再问）**
- Resource URL 加载、动态/异步资源加载：一律用 Unity 直接引用，`Loader`/`MovieClip` 的帧与内容都在 Migrate 期烘成 `Sprite`/prefab。
- Runtime API 一致性：能用 Unity/uGUI 原生 API（`RectMask2D`、`CanvasGroup`、`InputField`、锚点拉伸…）就直接用，不为对齐 FairyGUI 签名造并列封装。

**烘焙优先已覆盖、无需再造运行时组件**
- 响应式/容器关联（relation to container）：已在 `Migrate.SetRect` 里映射成 uGUI 锚点（`anchorMin/Max` + 拉伸/居中/贴边），容器改尺寸时子物体自动重排。只有小数百分比锚点（FairyGUI `usePercent` 的分数位）未映射，各 demo 未用到。
- `GGroup` 布局：子物体在烘焙期已绝对定位，组内相对布局在编辑器就解算完。组级 alpha/显隐用 `GearLook`/`GearDisplay` 逐 target 传播（`CanvasGroup`），不单造 runtime group。

**FairyGUI 动态 runtime API：本版不实现（如需再评估）**
- FairyGUI `GList` 的 selection / 虚拟化 / `AddItemFromPool` / `numItems`+`itemRenderer` 等：本版列表是 `List.Fill` 烘焙静态填充，够 demo 用。真要做动态列表再单开。
- `GTree`/`TreeView`、`GObject.tooltips` 顶层浮层、`BlurFilter`、`TypingEffect` 逐字动画：无 demo 覆盖，未做。
- Controller actions（`ChangePageAction`/`PlayTransitionAction`）与 Button `relatedController`（点按钮翻别的 controller 页，如 tab 栏）：**故意暂缓**。当前 controller 是「组件类里内嵌 enum + `Controller<该 enum>`」的静态强类型设计，跨组件按名翻页需要一个非泛型 controller 面或反射，和静态化原则冲突；若将来要做，应先设计非泛型 controller 面，别用字符串反射硬接。

**本轮发布整改新增（已过 70/70）**
- `ComboBox.selectedIndex` 改为属性：程序化赋值也刷新标题并发 `onChanged`；新增 `values[]`/`text`/`value`。下拉按 `visibleItemCount` 裁剪：项数超出时把 `list` 裁到可见高并 `ScrollPane.Attach` 支持滚动，未超出时撑开显示全部（短下拉与旧行为一致，golden 不受影响）。
- `ScrollPane` 补全交互：鼠标滚轮（`IScrollHandler`）、滚动条 grip 拖动（`ScrollBarGrip`）、松手惯性衰减 + 越界橡皮筋回弹、`onScroll` 事件、`ScrollToView`。静态终态不受影响（无交互时内容静置在界内，`Update` 早退）。
- `ProgressBar.TweenValue` 加 `Ease` 参数；`Slider` 加 `onGripTouchBegin/End`（仅按 grip 才发，点轨道不发）。
- `MovieClip.SetFrame` 越界钳进有效帧范围，避免被 `ProgressBar` 等驱动到 100 帧时自播 `Update` 越界索引 `addDelays[frame]`。
- `Window.modal` + `Root` 模态层：模态窗打开时铺半透明 `ModalLayer`（`modalColor`，可接收射线拦截下层），铺在最上层模态窗之下、其余内容之上；`Root.HasModalWindow` 查询。
- `PopupMenu.AddItem` 改为返回项 `ButtonBase`（调用方直接设 grayed/勾选/Icon，不再逐个包 SetItemXxx）；加 `ClearItems`。
- `InputText` 加 `password`/`maxLength`/`editable`/`onSubmit`，并由 Migrate 从 FairyGUI `password`/`maxLength` 属性烘焙。

**转换器健壮性整改（对非 demo 的真实 FairyGUI 工程）**
- 包名/空名进 C# 标识符前统一过 `Identifier`：`namespace UI.{包}`、跨包组件类型、`FindType` 三处都 sanitize，避免带空格/连字符/数字开头的包名（如 `My UI`/`2048`）生成不可编译代码。
- list 的 `<item>` 不再假定是 button：`ConfigureButton` 找不到按钮面时跳过，支持 defaultItem 为普通 component/label 的列表。
- gear 的 `pages` 引到控制器已删除的陈旧页 id 时跳过该槽（`Array.IndexOf==-1` 不再越界），对齐 transition 侧已有的 stale-target 防护。
- `Image`/`RectTransform` 子物体字段类型改全限定（`UnityEngine.UI.Image`/`UnityEngine.RectTransform`），避免组件命名为 `Image`/`RectTransform` 时字段类型被外层类遮蔽。
- 内嵌 `ui://` 图片标签用 `TryResolve`，陈旧引用留 null 占位不抛。

**已知 codegen 约束（无法自动消解，命名时避免）**
同一包内两个组件/控制器名若仅差标点或大小写折叠后相同（`Panel-A` 与 `Panel A`、`2p` 与 `_2p`），会折叠成同一 C# 标识符 → 重复类/枚举编译冲突。这是静态强类型 codegen 的固有边界；FairyGUI 工程里避免用仅靠标点区分的同名组件/控制器。

**真跑才暴露的三个 bug + 测试教训（重要）**
之前的 smoke 测试直接 `Invoke onClick` / 直接写 `.text`，绕过了真实输入路径，所以三个只在真跑时出现的问题一直没被发现。已补 `InteractionRuntimeTests`：经真实 `GraphicRaycaster` 命中 + `ExecuteEvents` 派发点击、要求命中落在目标子树内（能抓到"被背景穿透/遮挡"），并断言 MovieClip 真的在推进帧。改交互/转换后必须让这类真射线测试过。
1. **MovieClip 渲染空白**：FairyGUI 编辑器工程里 movieclip 实例的显示标签用资源扩展名 `<jta>`（`.jta` 文件），不是 `<movieclip>`。反序列化的 `displayList` 没列 `jta` → 这些元素被整个丢弃，MovieClip 页几乎空白。修复：`FairyXml` 的 displayList 加 `[XmlArrayItem("jta")]`，并在 `Finish` 里把 `jta` 归一到 `movieclip` 类型。（同一 movieclip 在别的页用 `<movieclip>`、在 Demo_MovieClip 用 `<jta>`，两者都要认。）
2. **Popup/Window 按钮点不动**：FairyGUI 按钮整块可点、与内部图形无关；但 uGUI 只有 raycastTarget 图形盖住的地方才收点击。用 Shape 描边画的按钮（如 Button1）中心没有 raycast 面，点击穿过按钮打到背景 graph（Main 的 n26 灰底），`hits=1` 只命中背景。修复：Migrate 给所有 button 组件根加一张透明(alpha=0，不改渲染)、raycastTarget 的 `Image` 铺满 rect，保证整块可点。静态 golden 不受影响（透明不渲染）。
3. **Text 输入框不工作（点不动/不能编辑）**：根因**不是** Input System（uGUI `InputField` 在 `activeInputHandler=1` 新 Input System 下正常工作，保持 `1`；之前误判成要改 Both，已撤销）。真因是 **`NanamiUI.Text` 不该坐在 `InputField` 下面**：① `Text.OnEnable` 会执行 `raycastTarget = _onClickLink != null`，输入框的 `_onClickLink` 为 null → 运行时把烘焙的 `raycastTarget` 覆盖成 false → 它是 InputField 唯一的 targetGraphic → `GraphicRaycaster` 跳过 → 永远无法聚焦；② 它自绘 `OnPopulateMesh`（`vh.Clear()` + 自画 quad）不填 `cachedTextGenerator` → InputField 光标/选区定位失效，且空文本时无网格 → 该图形 `depth==-1` 连射线面都没有。修复：`Migrate.ConfigureInput` 改用原生结构——根上加一张透明常驻 `Image` 作 `targetGraphic`（稳定射线面），加一个普通 `UnityEngine.UI.Text` 子物体作 `textComponent`（原生渲染 + 填 generator，`LegacyRuntime.ttf` 内置可序列化字体）；prompt 仍用 `NanamiUI.Text`（非命中面、复刻 UBB 斜体灰）。输入框不追求字体排版一致（项目允许）；空文本静置显示的是 placeholder，故静态 Text golden 不受影响。真射线聚焦测试见 `InteractionRuntimeTests.TextInput_field_is_editable_and_readable`。
   - 坑：`Migrate` 里 `Resources` 是本类的资源字典字段，要用内置 `UnityEngine.Resources.GetBuiltinResource<Font>(...)` 必须全限定，否则撞名编译错。

**踩坑：别给烘焙的 scrollbar grip AddComponent 一个 `Graphic`**
给已有 CanvasRenderer 的 grip 再加 `Graphic` 子类（当时想做无图形 grip 的射线面）会破坏 grip 的 CanvasRenderer 状态，`MainNavigation` 里 Main 页含滚动列表 → 整页场景污染 → 14 个导航 case 连锁失败。结论：`ScrollBarGrip.Bind` 只接**已带 `Graphic`** 的 grip（烘焙的可见 grip 都带 Image），绝不 AddComponent。改 runtime 交互后必跑 `Run PlayMode Tests`。

## 当前已知边界

- Popup/ComboBox 外点关闭使用透明 blocker，表现为模态；这是为了避开新 Input System 下旧 `UnityEngine.Input` 的异常。
- `ScrollPane` 拖动/滚轮/滚动条 grip 拖动/惯性回弹已支持；仍无虚拟化、无分页吸附、无下拉刷新。
- ComboBox 下拉已按 `visibleItemCount` 裁剪滚动；无 button 控制器的 Dropdown 变体不接下拉。
- Grid demo 主要填可见文本，star/cb/mc 细节从简。
