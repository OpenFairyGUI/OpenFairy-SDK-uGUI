# AGENTS.md

导入测试必须隔离源、输出和依赖目录并恢复设置，不能扫描或改写消费工程的真实资源。需要保护脚本路径/GUID 的测试使用预先编译的脚本，不在运行中的测试里导入临时 C# 触发意外程序集重载。C# enum 可以保留与类型相同的成员名；只处理实际不支持的标识符与作用域冲突。

本项目的 AI 协作约定。开始工作前先读本文件，再读 `Docs/BasicsImplementationStatus.md` 了解当前实现状态；有实质进展时同步更新状态文档。

2026-10-26 图片／序列帧的原生 `TouchDisabled` 与 `GObject.touchable` 是不同条件。迁移时应关闭它们的 raycastTarget，避免装饰外框阻挡后方按钮；Loader 和组件根继续保留命中。已有产物须 Migrate。保存重载后的真实射线及点击回归替换了原先“所有图片都拦截”的错误断言；6.6.7f1 Builtin 导入边界20/20、非批处理 PlayMode196/196。

2026-10-24 GroupLayout 的五参数 SetLayoutInputForAxis 从 Unity 6.6 / uGUI 2.6 起可用，不能以 UNITY_6000_7_OR_NEWER 为边界；保留更早版本的四参数分支。6.6.7f1 与 6.7.0b5 的 Builtin/TMP Runtime、Editor 程序集分别编译通过，Builtin 包级非批处理 PlayMode 各196/196；旧四参数参考程序集分支另编译通过，不等于完整旧版 Editor 验证。自动捕获和 WaitForEndOfFrame 测试须使用非批处理运行，批处理结果不能冒充完整通过。

Builtin TextField 使用自己的字形排版，原生 UI.Text 的 Best Fit 不会驱动该网格；Shrink/Ellipsis 必须走已烘焙的 overflowMode。缩放保留原始 text/fontSize，字体大小 gear、矩形和文本变化重新求解；行内图片保留作者尺寸。RebuildImages 烘焙所有已知图片及直接引用，包含初始被省略的项；运行时只更新位置与显隐，不重建节点。更新 SDK 后重跑 Migrate，不能只改旧 Prefab 的原生 overflow 标志。

新建语言表/集合先填充字段，再调用 CreateAsset；该 API 会立即写初始文件，不能依赖随后反射赋值和更晚的统一 SaveAssets 赶在首次重导之前。首写回归直接强制重导后检查翻译内容，不能只断言内存中的新对象。


2026-10-19 代码生成名称已统一到生成期作用域分配：标识符、同作用域去重、控制器页、动效 Hook、本地化及烘焙引用使用同一映射，原作者名称不改。最终 TMP 完整套件在交互 Editor 中为 Editor 215/215、PlayMode 215/215，无失败或跳过；TMP/Builtin 各四程序集另零错误编译。后台缺少渲染帧的失败保留，不能将交互结果外推为 batchmode 已通过。交互首轮另因外部命令请求超时日志为214/215；同版重跑215/215，未改SDK断言。传统 Demo/golden、Builtin 原生整套和 Player 未重跑。

当前能力以状态文档的最新日期为准，下文开发经验保留其历史背景。原 `ScrollPaneHost` 已删除，滚动交互由原生 `ScrollRect`/`Scrollbar` 承担；虚拟列表、循环索引与变高行已实现。输入框已有 Input System 撤销/重做与单行 Tab 实现；虚拟输入测试使用官方 `InputTestFixture` 隔离设备并恢复宿主状态，不能依赖 Editor 窗口焦点。按钮 `grayed` 只决定视觉，触摸/交互是独立通道，不再以灰显一律拦截点击。

2026-10-19 独立 Builtin 包级完整验证为 Editor 157/157、PlayMode 169/169，无失败或跳过；下文旧数量保留历史上下文。Button<T> 原生 Enter/Exit/Down/Up 与 Click 一样可覆写；消费方在派生方法中调用 base，避免隐藏接口方法而丢失 SDK 视觉状态。停用按钮清除瞬时按下和悬浮状态，保留选中与灰显；ExecuteEvents 回归覆盖全部按钮模式、选中、灰显、左右键与停启复用。未据此扩大 TMP/golden 的验证范围。

导入器的组件实例 `controller`、列表项 `controllers` 使用页ID而非序号，按原顺序应用后再结算最终布局。测试中需要挂在 GameObject/prefab 上的支持组件放运行时测试程序集，不能放 Editor 程序集；EditMode 测试经引用复用这些支持组件。

对象捕获/模糊/容器颜色滤镜保留显式同步刷新，Play 自动刷新使用 `Application.onBeforeRender`，晚于关系的 `PostLateUpdate` 同步，不能改回普通 `LateUpdate` 或 `willRenderCanvases` 捕获。最外层滤镜负责内层逆序刷新。重复无域/场景重载 Play 的测试按当前原生组件重新取得托管支持对象，不能把旧托管包装器里的观察列表当成当前组件；这一测试已通过，无需为夹具引用问题改生产生命周期。

较早 TMP 包内测试在 Unity Editor/Mono 的实际结果为 Editor **199/199**、PlayMode **199/199**，对应同一正式源码的完整两套运行，均无跳过或失败。TMP/Builtin 两后端各四个程序集另有离线零错误编译；Builtin 不据此视为原生行为通过。灰显、原生 SingleRow 布局/滚动查询、Hidden 列表裁剪、fitter 关系初态、共享 Transition 声音及十三种混合捕获均有回归。这不包含重新运行传统 Basics/Transition 全部 golden，也不代表稀疏工作树之外的 Demo 已通过。

静态文本与本地化由普通导出器生成 typed 方法、直接原生引用和语言资产；按钮 selected icon、声音和实例控制器覆盖在导入期烘焙。位图字体同域重导会失效查找缓存。Canvas overlay 使用 native anchors；非 Canvas 的兄弟 overlay 复制 anchors/pivot/rect/pose，并在 `LateUpdate` 检查矩形和变换矩阵变化，公开坐标仍从左上起算。

导出续建的 `delayCall` 依赖 Editor 活动状态，`Pending=false` 不等于 prefab 已完成；观察真正的完成日志，不清空其他系统回调。

原生 Window/popup 已采用组件与预烘焙 WindowBuilder，关闭等待 Transition、重开取消旧 session，外点由显式选择的 PopupInputModule 在原生派发前检查，不用透明拦截层重发事件。Root 必须继承设计根的 layer；两种相机模式的真实 GPU 对照已确认差异，永久回归通过。十项 popup 首轮失败来自夹具向不接受 null 的 EventSystem.current 赋空；清理仅恢复已有宿主，未给生产逻辑加兜底。

Loader 已支持预烘焙 Component content 与原矩形，按 native 尺寸事件整体缩放，不运行时解析 URL。image/movie/empty/content 切换与 mask/alpha GPU 检查通过。六项几何测试首轮把 height=60、pivot.y=.7 的顶边写成42，实际应为18；仅修独立测试数值。上述199/199检查点包含所有新增包内回归，不与历史总数累加，消费端和完整Demo/golden仍须单独验证。

整根 Overlay Canvas 停启保留已有 Relation 基线，避免 CanvasScaler 恢复期间的临时尺寸被重复累计；隐藏期间真实父尺寸和内容变化仍在恢复后同步。原生锚点只消化 anchor span 对应的尺寸差，fitter 自身改宽仍保持对齐。新增三项真实 prefab/Canvas 回归覆盖原生父缩放、同帧和隔帧内容变化及三种水平 pivot。

ContentSizeFitter 的 driven 尺寸不会完整序列化；Relation 首次启用时恢复并保留已烘焙的关系基准，随后接受真实重排；没有烘焙基准才 Record。不能用反序列化的零尺寸或 CanvasScaler 的临时尺寸覆盖作者基准。首次活动实例化时，父组件 OnEnable 可能早于子 fitter 原生启用；恢复已保存基线按 enabled 配置判断。self 坐标关系只叠加变化量，不能把自身 pivot 原点当绝对位移。动态修改 MaskableGraphic.maskable 的测试正控还须调用公开 RecalculateClipping，不能将未更新的 clip parent 当渲染结论。声音夹具在首次启用前配置 AudioSource，并为自身提供 AudioListener；不要因宿主无 listener 或不确定的构造时序给生产声音代码加兼容分支。

导入的 Image/MovieClip/Graph 与 FairyGUI 显示对象一样默认可命中，会挡住下层；组件按 opaque（缺省 true）在根节点烘焙透明命中面，opaque=false 时只有子对象可命中。ListSource 回收不能清除业务 Awake 注册的 UnityEvent；消费端 Fill 回调自行替换其每次绑定的业务监听。SDK 的选择中继只移除自己拥有的监听，并用绑定版本取消点击派发期间重填留下的在途回调。

Builtin InputField 在聚焦时会消费 EventSystem 的 updateSelected 导航事件；InputSubmit 必须在导入期持久绑定原生 onSubmit，不能把 ISubmitHandler 或 onEndEdit 当回车文字提交。测试保存并重新实例化 Prefab，再分别检查聚焦、失焦和提交的行为。

捕获必须按原生顺序运行完整材质 modifier 链，只跳过源隐藏 modifier。不能向 ColorAdjust/BlendModeEffect 的同一缓存传入去掉原生 Stencil 的不同输入，否则会释放源 CanvasRenderer 仍在使用的材质。源蒙版参数只在 owned 副本上恢复到 base 值，再由代理树重算本地遮罩；新增两项跨帧 GPU 回归及增强 TMP 图文混排覆盖源材质、pop、图集、外罩排除与内部蒙版。

## 环境

- **代码检索**：如果仓库根目录存在 `.codegraph/`，优先用 CodeGraph MCP 的 `codegraph_explore` 理解代码；没有 MCP 时用 `codegraph explore "..."`。没有 `.codegraph/` 才用 `rg`/文件读取。
- **命令行工具**：已安装 `rg` 和 coreutils，直接使用。
- **项目类型**：Unity 项目。应优先通过 unity mcp 执行菜单项；如果当前会话没有 unity mcp，先提示用户安装/连接。
- **注意**：unity mcp 执行代码时不要使用 `System.Reflection` 命名空间。

## 项目目标

OpenFairy 是基于 uGUI 的 FairyGUI Runtime SDK。目标是让用户继续用 FairyGUI Editor 编辑 UI，再用 OpenFairy 将 FairyGUI 工程转换成 uGUI prefab 和 C# 代码，并尽量还原 FairyGUI 的渲染、动效和交互表现。

## 目录结构

- `UIProject/`：待转换的 FairyGUI 工程。
- `Assets/Plugins/OpenFairy/Runtime/`：运行时 SDK，命名空间 `OpenFairy.UGUI`。
- `Assets/Plugins/OpenFairy/Editor/`：通用转换器和工程 postprocess，命名空间 `OpenFairy.UGUI.Editor`。
- `Assets/Scripts/`：Basics demo 的运行时胶水（本工程的用户脚本，不随包发布）。
- `Assets/Scripts/Editor/OpenFairy/`：编辑器验证工具、测试 runner。
- `Assets/UIProject/Assets/`：转换生成的资源和 prefab，保持 FairyGUI 包结构。
- `Assets/UIProject/Scripts/`：转换生成的 C# 代码。
- `Assets/Tests/`：PlayMode parity 测试和测试支持代码。
- `Docs/RenderDiff/`：golden 生成时顺便输出的人工查看 diff 图。

## 代码结构

- Runtime 控件命名 = 对应 FairyGUI 控件**精确去掉 `G` 前缀**：`Component`(GComponent)、`Button`(GButton)、`Label`(GLabel)、`ComboBox`(GComboBox)、`Slider`(GSlider)、`Loader`(GLoader)、`MovieClip`(GMovieClip)、`ProgressBar`(GProgressBar)、`TextField`(GTextField)、`TextInput`(GTextInput)、`Graph`(GGraph)、`Image`(GImage)、`List`(GList)、`Root`(GRoot)。历史遗留的 `GList`/`GRoot`/`Text`/`InputText`/`Shape`/`FlipImage` 已统一为 `List`/`Root`/`TextField`/`TextInput`/`Graph`/`Image`。
  - `Line` 不是 FairyGUI 控件（GGraph 内含线绘制），是 Graph 演示用的内部折线渲染 helper，保留原名。`ButtonBase` 是 GButton 的非泛型面（复用其 onClick/title），非 FairyGUI 类；`IComboBox` 同理是 ComboBox 的非泛型面（items/values/selectedIndex/text/value）。
  - `List` 与 `System.Collections.Generic.List<T>` 按泛型元数区分共存（`List.Container` 是本类静态；`List<T>` 仍是 BCL 泛型），不冲突。列表动态填充入口是 `ListSource.Fill`（实例方法，状态与烘焙描述同在 ListSource 上）。
  - 公共属性统一 camelCase（与所继承的 uGUI 基类及 FairyGUI 一致）：`title`/`icon`/`selected`/`grayed`/`frame`/`size`/`hasAnyPopup`…；方法保持 PascalCase。烘焙接线字段（titleText/relatedOwner/bar/grip/dropdownPrefab 等）为 `[SerializeField] internal`，经 `InternalsVisibleTo`（OpenFairy.UGUI.Editor/Assembly-CSharp-Editor/tests）供 Migrate 与测试直写，不进用户 API 面。
  - `Image`(←GImage, `: UnityEngine.UI.Image`)、`TextField`(`: UnityEngine.UI.Text`) 与 uGUI 同基类同短名：OpenFairy.UGUI 命名空间内**裸 `Image`/`Text` 指本类**，要引用 uGUI 基类须全限定 `UnityEngine.UI.Image`/`UnityEngine.UI.Text`（改名时已把所有裸 uGUI 用法全限定；`Image.Type`/`Image.FillMethod` 等静态访问也要限定）。`Run.Image`(int 字段)、`DisplayKind.Image`/`ResourceKind.Image`(enum) 不是类型引用，别误改。codegen 里 image 字段类型仍发 `UnityEngine.UI.Image`（`OpenFairy.UGUI.Image` IS-A，GetComponent 取得到），故生成脚本不引用 `OpenFairy.UGUI.Image`，改名不触发 codegen 死锁。
  - 重命名会引发 codegen 死锁坑：runtime 改名后，`Assets/UIProject/Scripts` 里**已生成**的组件脚本仍引用旧类型名 → Assembly-CSharp 编译失败 → 编辑器程序集(含 Migrate)无法重编译 → Migrate 跑的是**旧缓存程序集**，会把生成脚本再刷回旧名，死循环。解法：改名后先 `sed` 把生成脚本 + `Assets/Scripts/Editor/OpenFairy`(BasicsRenderDiff 用 `using FairyGUI`，易漏) 里的旧类型名一并替换，再 `CompilationPipeline.RequestScriptCompilation(RequestScriptCompilationOptions.CleanBuildCache)` 强制干净重编译(普通 Refresh 不够，增量缓存会残留旧程序集)，确认 0 error 后再跑 Migrate(此时幂等)。
  - `Window.contentPane` 是窗口内容根（复刻 FairyGUI Window.contentPane；旧的 `Window.Root` 属性已删，不再遮蔽 `OpenFairy.UGUI.Root` 类型）。
  - `OpenFairy.UGUI.Editor` 里引用 FairyGUI 的同类须全限定 `FairyGUI.GRoot`/`FairyGUI.GList`（BasicsRenderDiff 已如此）；改名后 OpenFairy 侧是 `Root`/`List`，与 FairyGUI 的 `GRoot`/`GList` 不再同名，天然不遮蔽。
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
- 一次性的局部表达尽量内联，不引入变量或函数；只有“起个名字”能显著提升可读性，代码与业务上下文相对独立且字面难懂时，才为非复用代码提取变量或函数。
- 复用的逻辑应提取，避免重复代码。
- 同一语义在不同体系下需要区分时，优先用命名空间或嵌套类型表达归属关系，不靠前后缀制造并列概念。
- 不写防御性代码：假设输入数据合法，不做多余 null 检查、参数校验和异常兜底。
- 不加无关注释、日志、配置或临时菜单。
- 保持周围命名、缩进和 Unity 序列化字段风格一致。
- 不要重复造轮子。尽可能利用官方库 (Unity自带，.net 自带，Unity 官方包、.net 官方包)、三方库来减少代码量、让代码更清晰。

**现代化**
- 使用最新版本的写法，充分利用现代特性。
- 如果最新版本 API 在当前环境下不支持，应该先寻求看有没有办法让它支持 (例如升级环境、安装某个 nuget 包、引入 Polyfill 等)。实在没有办法使用新 API 的情况下才考虑退回旧版本，并且发生了这种情况一定要告知用户。
- 项目已引入 **ZLinq**（`using ZLinq;`，程序集 `ZLinq.Unity`）和 **UniTask**（`using Cysharp.Threading.Tasks;`，程序集 `UniTask`），两者零 GC。LINQ 能比手动循环表达得更清晰的场合，用 ZLinq（`.AsValueEnumerable()` 起链）替换手动循环；`await` 能比回调/协程/轮询表达得更简洁优雅的场合，用 UniTask。不要用 `System.Linq`。
- 缓动/补间用 **DOTween**（`DG.Tweening`，缓动直接调 `DG.Tweening.Core.Easing.EaseManager.Evaluate`）；曲线求值用官方 **com.unity.splines**（`UnityEngine.Splines.BezierCurve`+`CurveUtility`，`TransitionPath` 在其上保持 FairyGUI GPath 的参数化与段长口径，勿换成 Spline 类的真弧长求值——那会偏离 FairyGUI 轨迹）；golden 图像断言用官方 **com.unity.testframework.graphics** 的 `ImageAssert`（口径见 `StaticGoldenTests`），不要再手写像素对比。

**静态化**
- 静态结构优于动态解析；不要滥用 `string`。只有真实文本内容才用 `string`，有限选项用 enum，结构化值用 struct/class 字段等能保持静态引用和编译期检查的表达方式。
- 能通过明确的正向函数调用完成的流程，不引入 `Action` 回调等动态分发方式。
- 能在烘焙 prefab 阶段生成并序列化保存的数据，不放到运行时生成。
- 尽量减少运行时动态创建和销毁 Unity Object，能在烘焙时确定的就在烘焙时创建，暂时不需要就 SetActive(false)。
- 偏好使用栈上内存而不是堆内存。尽量减少运行时 GC。
- 使用更容易在编译期间发现错误的写法，而不是把错误推到运行时，更不要写防御性代码隐藏掉错误。

## 标准工具入口

Unity 菜单 `Tools/OpenFairy` 下只保留三个公开功能：

1. `Tools/OpenFairy/Migrate`
2. `Tools/OpenFairy/Generate Golden References`
3. `Tools/OpenFairy/Run PlayMode Tests`

不要新增长期存在的调试菜单。临时排查代码在提交前删除，或保留为非菜单 helper。

### Migrate

用途：把 `UIProject/` 转成 `Assets/UIProject/Assets/` 下的 prefab/资源，以及 `Assets/UIProject/Scripts/` 下的生成代码。

AI 操作流程：

1. 通过 unity mcp 执行菜单 `Tools/OpenFairy/Migrate`。
2. 如果本次修改影响生成脚本，Migrate 会设置 `OpenFairy.Migrate.Pending`，等待 Unity 域重载后自动继续构建 prefab；这段期间不要插入其它 Unity 命令打断 `DidReloadScripts` 的 `delayCall`。
3. Migrate 完成后会自动跑 `[MigratePostProcess]`，例如 Basics demo 的 prefab 引用配置，不要手动调用旧的配置菜单。
4. 关注 Console 里的 `OpenFairy migrated ...` 日志；若失败，先修编译错误或转换器错误，再重跑 Migrate。

### Generate Golden References

用途：生成 FairyGUI 参照图和交互几何参照，用于 PlayMode parity 测试；同一趟也会输出人工查看的 render diff。

AI 操作流程：

1. 通过 unity mcp 执行菜单 `Tools/OpenFairy/Generate Golden References`。
2. 工具会自动进入 Play Mode，逐页同帧驱动 FairyGUI 与 OpenFairy，输出：
   - `Assets/Tests/Golden/ReferenceImages/*.png`
   - `Assets/Tests/Golden/ReferenceImages/*.geo.json`
   - `Docs/RenderDiff/*_strip.png`
   - `Docs/RenderDiff/*_fairygui.png`
   - `Docs/RenderDiff/*_openfairy.png`
   - `Docs/RenderDiff/*_diff.png`
3. `Assets/Tests/Golden/` 和 `Docs/RenderDiff/` 是现生成产物，按需查看，不要把它们当作手写源码。
4. 文字/精灵边缘约 1px 的红边通常是已知 alpha/亚像素差异；Shake 随机数不追逐像素完全一致。

### Run PlayMode Tests

用途：跑全部 PlayMode parity 测试。

AI 操作流程：

1. 通过 unity mcp 执行菜单 `Tools/OpenFairy/Run PlayMode Tests`。
2. 结果写入 `Temp/OpenFairyPlayModeResults.txt`，完成后读取该文件；第一行是整体状态，失败行带测试名和消息。
3. 若改了转换器或 golden 相关逻辑，先 `Migrate`，再 `Generate Golden References`，最后 `Run PlayMode Tests`。
4. 需要命令行跑 Unity Test Runner 时不要加 `-nographics`，URP 渲染测试需要 GPU。

## 验证原则

- 改 Runtime 渲染、动效、转换器或 demo 胶水后，至少跑 `Run PlayMode Tests`。
- 改会影响参照图/几何参照的逻辑时，先跑 `Generate Golden References` 再跑测试。
- 改转换输出结构、序列化字段或生成代码时，先跑 `Migrate`。
- 测试程序集访问不到生成的 `{包}` 类型时，优先经非泛型 Runtime 面驱动，例如 `Slider`、`ButtonBase`、`IPointerClickHandler`。
- 静态 golden 证明像素终态；交互几何测试证明“到达正确状态”；动效轨迹需专门逐帧行为测试覆盖。

## 重要实现约定

- `BasicsRenderDiff` 直接 `UIPackage.CreateObject` 建 FairyGUI 参照视图，不跑官方 demo 初始化。需要 FairyGUI `UIConfig.*` 的配置必须在脚本里补齐。
- `Time.captureDeltaTime` 只锁 `Time.deltaTime`，不锁 `Time.unscaledDeltaTime`。截图对比中 FairyGUI transition/tweener 要关闭 `ignoreEngineTimeScale` 才能和 OpenFairy 同步。
- `OpenFairy.UGUI.TextField` 自己在 `OnPopulateMesh` 中复刻 FairyGUI 排版公式；改文字布局时对照 `Assets/FairyGUI/Scripts/Core/Text/{TextField,DynamicFont}.cs`。
- 平铺图由 `Image.OnPopulateMesh` 自己生成 tiled quad，以匹配 FairyGUI 从左上铺、残缺格落在右下的相位。
- Runtime 类型移动程序集/命名空间会破坏 prefab 中 `[SerializeReference]` 的 gear 类型绑定；移动后必须重烘焙或迁移 serialized type。

## 发布范围与取舍（AI 经验记录）

本 SDK 走 **烘焙优先（bake-first）+ 复用 uGUI 原生**，不复刻 FairyGUI 的动态运行时对象模型。判定新需求是否要做时，先套下面的范围结论，别照搬 FairyGUI runtime API 清单。

**明确不做（用户已定，别再问）**
- Resource URL 加载、动态/异步资源加载：一律用 Unity 直接引用，`Loader`/`MovieClip` 的帧与内容都在 Migrate 期烘成 `Sprite`/prefab。
- Runtime API 一致性：能用 Unity/uGUI 原生 API（`RectMask2D`、`CanvasGroup`、`InputField`、锚点拉伸…）就直接用，不为对齐 FairyGUI 签名造并列封装。

**烘焙优先已覆盖、无需再造运行时组件**
- 响应式/容器关联（relation to container）：已在 `Migrate.SetRect` 里映射成 uGUI 锚点（`anchorMin/Max` + 拉伸/居中/贴边），容器改尺寸时子物体自动重排。只有小数百分比锚点（FairyGUI `usePercent` 的分数位）未映射，各 demo 未用到。
- `GGroup` 布局：子物体在烘焙期已绝对定位，组内相对布局在编辑器就解算完。组级 alpha/显隐用 `GearLook`/`GearDisplay` 逐 target 传播（`CanvasGroup`），不单造 runtime group。

**转换器健壮性整改（对非 demo 的真实 FairyGUI 工程）**
- 包名/空名进 C# 标识符前统一过 `Identifier`：`namespace {包}`、跨包组件类型、`FindType` 三处都 sanitize，避免带空格/连字符/数字开头的包名（如 `My UI`/`2048`）生成不可编译代码；组件字段（含同包）统一使用 `global::{包}.{组件}`，避免目标包命名空间被同名组件类或生成的嵌套枚举遮蔽；包命名空间进入全局后，既有同名外部类型也必须全限定（如 `UnityEngine.RenderTexture`、`FairyGUI.Emoji`/`Transition`/`TypingEffect`）。
- list 的 `<item>` 不再假定是 button：`ConfigureButton` 找不到按钮面时跳过，支持 defaultItem 为普通 component/label 的列表。
- gear 的 `pages` 引到控制器已删除的陈旧页 id 时跳过该槽（`Array.IndexOf==-1` 不再越界），对齐 transition 侧已有的 stale-target 防护。
- `Image`/`RectTransform` 子物体字段类型改全限定（`UnityEngine.UI.Image`/`UnityEngine.RectTransform`），避免组件命名为 `Image`/`RectTransform` 时字段类型被外层类遮蔽。
- 内嵌 `ui://` 图片标签用 `TryResolve`，陈旧引用留 null 占位不抛。
- font 资源只有 `.fnt` 才走 `ImportFont`（ttf/otf 如 TextMeshPro 包的 SDF 字体按文件名当动态字体，二进制被当 fnt 文本读会因无 char 行崩溃）。
- gearSize 值兼容老格式 `"w,h"`（无 scale 分量时回退 default 的 scale，复刻 GearSize.Init）。
- 2026-07-15 起 `UIProject/` 含 FairyGUI 官方 demo 全部 30+ 包，Migrate 全量 199 个组件成功；parity 测试仍只覆盖 Basics/Transition。

**生成式名称的冲突分配**
不同作者名转义为相同标识符时，由导出器在对应作用域分配唯一名称，保留所有对象及直接引用。C# 允许 enum 与成员同名，不为此追加后缀；仍避开保留的 value__。完整索引相同的源和依赖包后生成脚本，并在编译完成后重烘相应 prefab 和语言资产。

**踩坑：别给烘焙的 scrollbar grip AddComponent 一个 `Graphic`**
给已有 CanvasRenderer 的 grip 再加 `Graphic` 子类（当时想做无图形 grip 的射线面）会破坏 grip 的 CanvasRenderer 状态，`MainNavigation` 里 Main 页含滚动列表 → 整页场景污染 → 14 个导航 case 连锁失败。结论：`ScrollBarGrip.Bind` 只接**已带 `Graphic`** 的 grip（烘焙的可见 grip 都带 Image），绝不 AddComponent。改 runtime 交互后必跑 `Run PlayMode Tests`。

**踩坑：烘焙进 prefab 的 MonoBehaviour 必须独立成同名文件**
Unity 无法把"定义在别的 .cs 文件里的次要 MonoBehaviour 类"序列化进 prefab（`SaveAsPrefabAsset` 报 missing script）。所以 `ScrollPaneHost`/`InputSubmit`/`ListSelection` 这些 **Migrate 期 AddComponent 并存进 prefab** 的类，每个都单独一个同名文件。只在**运行时** AddComponent、从不入 prefab 的类（`ScrollBarGrip`/`ScrollBarTrack`/`WindowDragArea`/`PopupBlocker`）可与主类同文件。

**交互烘焙完整化（2026-07-09，让转换产物在无 demo 胶水下也具备完整交互）**
核心原则修正：FairyGUI 在**编辑器里就烘焙进资产**的交互，必须由 Migrate 烘出来，不能只写在 Example 胶水里，否则通用工程拿到的 prefab 是"能显示不能交互"。本轮补齐：
- **按钮关联控制器（tab/radio 组）**：`<Button controller=".." page="..">` 现解析进 `Schema.Extension` 并烘成 `ButtonBase.relatedOwner/relatedControllerField/relatedPage`。点击时 `ApplyRelatedController` 经 `ControllerBinding`（反射设 `Controller<T>` 结构的 page，与 InteractionDriver/Migrate 同一条反射路径）换页 + 按"共享同一关联控制器"同步整组 `selected`（组员不必是直接兄弟）。这才是 FairyGUI 标准 tab/单选机制；旧的"同父兄弟扫描"仅作**无关联控制器**时的回退。Example `SetupButtonDemo` 已删除（烘焙替代）。
- **`overflow=scroll` 自挂 ScrollPane**：Migrate 给滚动根挂 `ScrollPaneHost`，运行时 `Start` 自挂 `ScrollPane`（幂等）。通用工程的滚动组件无需业务代码即可拖动/滚轮/滚动条。`ScrollPane.ContentBounds` 改用 `Relation.TopLeft`，内容用非 (0,1) 轴心也测得准；grip 长度按 view/content 比例实时缩放；补轨道点按翻页。
- **Slider `min/reverse/wholeNumbers/changeOnClick`**：`Schema.Extension` 补这四个属性并烘进 `Slider`；runtime 补 reverse 填充/拖动反号 + Filled-Image bar。
- **列表选择**：`<list selectionMode>` 默认 single，烘 `ListSelection`（点项按 single/multiple 置选中 + 发 `onClickItem`）。ComboBox 下拉/PopupMenu 自管点击，`Build`/ctor 里移除列表自带的 `ListSelection`/`ScrollPaneHost`。
- **ComboBox onChanged 语义修正**：程序化赋值 `selectedIndex` **不发** onChanged（只刷标题，复刻 GComboBox）；点下拉项**总是**发（含重选当前项）。
- **输入框**：`text` 用 `SetTextWithoutNotify`（程序化赋值不自触发 onChanged）；`editable=false` 映射 `readOnly`（只读但可聚焦，非禁用）；`onSubmit` 经 `InputSubmit`（ISubmitHandler，仅回车、仅单行）不再等同 onEndEdit（避免失焦误提交）。
- **Window frame 拖动**：`OnInit` 找 `dragArea` 挂 `WindowDragArea`，拖动移动整窗 + 提到最前（`Root.BringToFront`）。
- **Popup 指针定位**：`Root.ShowPopupAt` + `PopupMenu.ShowAtPointer` 支持右键在指针处弹出。
- **GearLook 置灰传按钮**：`ApplyGrayed` 同时置 `ButtonBase.grayed`，灰显的按钮进 disabled 页且拦截点击。
- **Depth `SetSortingOrder` 前移越位修正**：目标位 = 应排它前面的其它子物体数（`order<=` 计入），修 Unity 移除再插入的 off-by-one。
- 新增测试：`BakedInteractionTests`（直接实例化 prefab、无胶水、真射线驱动，证明通用工程已具交互）+ `NewFeatureTests` 补充关联控制器/ListSelection/ScrollPaneHost 真拖/GearLook 置灰/Depth 前移/Slider reverse/动态 List.Fill 刷新等回归。

**本轮明确不做 / 记为边界（避免再纠结）**
- 输入框 `restrict`/`keyboardType`/focus-blur 事件/Tab 导航、`selectedIcon`、按钮点击音、ComboBox `selectionController`/项图标、PopupMenu 的 `AddItemAt`/`RemoveItem`/可勾选项/子菜单、List 虚拟化/分页吸附/方向键导航/`align`/`autoResizeItem`：均超出"复刻可见交互"的发布范围，用到再补。
- `Draggable.dragBounds` 用 **parent-local**（非 FairyGUI 的 GRoot-local）；draggable/dragBounds/sortingOrder 本就是 FairyGUI 的**运行时 API**（app 代码设，非编辑器烘焙），故由业务代码按本 SDK 语义设，不烘焙。
- `ScrollPane` 惯性/回弹常数取近似值（视觉接近，非逐帧复刻 FairyGUI）；`ScrollToView` 仅竖直。
- UBB 裸 `[url]body[/url]`（无 `=`）暂不取 body 作 href；用 `[url=..]` 或 `<a href=..>`。
- 运行时富文本 `[img]…[/img]`/`<img src=…/>` 的正文（含 `ui://` URL）被忽略，图片按标签出现**序号**绑定 `TextField.imageSprites`（Migrate 从授权文本烘的 Unity 引用；运行时要换图就直接改这个数组）。`ui://` 概念只存在于转换器读源工程，运行时零残留。
- `TextInput.onSubmit` 经 `InputSubmit`（ISubmitHandler，仅回车不失焦）——比旧的 onEndEdit 别名安全（不误触发失焦提交），但若输入模块不向聚焦的 InputField 派发 submit 动作则可能不触发，属**尽力而为**；宁可偶不触发，不选失焦误触发。

**本轮改动经独立对抗式复审（4 路 agent 读 diff+源）确认后逐条修复**：slider 实例级不再覆盖定义级 reverse/wholeNumbers/changeOnClick；Check+关联控制器取消勾选回对页（复刻 oppositePageId，修 Demo_Controller 复选框只能开不能关）；Common tab 不置 selected（激活态靠 gears，否则卡按下页）；`ListSelection` 给项置 `changeStateOnClick=false`（否则多选态 Check 项与选择逻辑双翻永远选不上）；`ScrollPane.Attach` 不再按名复用 "content"（元素恰名 content 会劫持容器）；透明射线面改保留名 `__scrollHit`；轨道点按方向比较换到 bar 本地坐标；`ControllerBinding` 同页早返回不重跑 gears；`PopupMenu.ShowAtPointer` 默认 `Auto`（贴底上翻）。

**发布前 final review 修复（2026-07-09）**：`ComboBox` 的 XML `<item value>` 改按字符串解析并烘进 `values`，runtime `text/value` 也支持程序化 setter（不发 onChanged）；`List.Fill` 在已挂 `ScrollPane` 后重填会保留/刷新 `__scrollHit`、自动重绑 `ListSelection` 且不重复 listener；Example Grid 改用通用 `List.Fill`，删除显式 `AttachScrollPanes` 胶水；codegen 对与 runtime 基类成员同名的 controller enum 输出 `new enum`，消除 `grayed` 隐藏 warning。`Migrate` 101 个组件成功，`Run PlayMode Tests` 当前 `99/99` 通过。

**发布前 final review 第二轮（2026-07-09，多 agent 审查+FairyGUI 源码逐条核实后修复）**：
- **SDK 自足性（通用工程无胶水可用）**：`ComboBox` 点下拉时 `Root.inst` 为空则自建覆盖层（`Root.Create` 支持传 canvas 根，anchoredPosition 归零贴左上）；滚动条轨道/grip 引用烘进 `ScrollPaneHost` 序列化字段（原按 demo 资源名前缀 `ScrollBar_VT/HZ` 运行时找名，通用工程滚动条静默失效），`ScrollPane.Attach` 改读宿主引用、无 host 视作无滚动条；运行时字体烘进 `TextField.fontNames`（原靠 Example 胶水设静态 `defaultFont`，无胶水回落 Arial）；`Grayed`/`ColorAdjust` 的 shader 改为 Migrate 期 `Shader.Find` 后序列化进组件（随 prefab 进构建，用户工程无需配 AlwaysIncludedShaders），两组件均烘焙期挂好（GearLook 目标烘 disabled `Grayed`、ColorFilter 目标烘 disabled `ColorAdjust`），运行时只切 enabled/Set、不再 AddComponent/Shader.Find。
- **FairyGUI 行为对齐**：交互全面仅左键（复刻 `inputEvent.button != 0` 早返回：Button/Slider/ScrollPane/ScrollBar/Draggable/WindowDragArea/TextField 链接）；`ButtonBase.Selected` setter 复刻 GButton（Common 忽略、同值早返回、选中驱动关联控制器/Check 取消回对页），点击流程镜像 `__click`（Radio 已选中再点不重复、补 `onChanged` 事件）；新增 `GearButton`（复刻 `HandleControllerChanged`）由 Migrate 烘进关联控制器 gears，程序化换页也同步组内选中态；`GearColor`/`GearLook` 起 tween 加 display lock（对齐 GearXY/GearSize）；ComboBox `popupDirection` 默认 Auto、`text` setter 标题直通不反查、`value` setter 找不到回退首项、`onChanged` 复用 ButtonBase 事件（FairyGUI 同一事件通道）；Slider 轨道点按只跳一次（当前值+相对 grip 中心偏移，无 grip 回退绝对映射），grip 拖动移入烘焙的 `SliderGrip`（grip 常是独立 button 组件会吞指针事件，中继必须在 grip 层——复刻 GSlider 挂 `_gripObject`）；滚动条 grip 拖动保按下偏移（复刻 `_dragOffset`，不吸附中心）；`ListSelection` 复刻 `SetSelectionOnEvent`（multiple 无修饰键点击=排它选中，multiple_singleclick=切换；非按钮项点击冒泡到列表根也发 `onClickItem`，索引=content 内次序）；Root 模态层复刻 `AdjustModalLayer`（层插到按兄弟序最上层激活模态窗之下、不强制窗口置顶；模态期间显示的非模态窗插到层下）。
- **静态化/错位清理**：Button 的 mode/selected/grayed/selectedTitle/titleText/iconLoader 上提 `ButtonBase`，Migrate 按钮配置全静态（仅 `Controller<T>` 泛型字段保留反射）；删 ComboBox 下拉按名 "n0" 缩放背景的 demo 假设（背景容器 relation 已烘成拉伸锚点自动跟随）；`BasicsExample.cs`（demo postprocess）移出 SDK 到 `Assets/Scripts/Editor/OpenFairy`；`OpenFairySettings.asset`（工程配置）移出 SDK 到 `Assets/`（2026-07-15 已整体删除：烘焙字体只取 Common.json，运行时字体替换走用户脚本 `DemoFont`）；删无消费者的 `buttonSound`/`buttonClickSound` 配置字段。
- **可选中列表项须是 Radio/Check 模式**：`GButton.selected` 对 Common 直接忽略（FairyGUI 同），列表选择高亮只对非 Common 项生效——FairyGUI 的列表项模板本就用单选模式。

**string→enum 全面整改（2026-07-10，用户点名"强制补全覆盖面"）**：有限选项一律枚举——`Relation.sidePairs`→`RelationSide`（FairyGUI RelationType 全词表 27 成员；Width/WidthWidth 等是同义 XML 拼写）、`ListSource.layout`→`ListLayoutType`、`ListSelection.selectionMode`→`ListSelectionMode`、`Loader.align/vAlign` 魔数 int→嵌套 `AlignType/VertAlignType`、Schema 的 `Resource.Scale`→`ImageScale`（空值=None）、`AutoSize`→`TextAutoSize`、`Extension.Mode`→`ButtonMode?`（实例级缺省不覆盖定义级）、`TitleType`→`ProgressTitleType`。`FairyXml.ParseEnum` 回退时把 XML 标点词表折叠成成员名（`left-left`→LeftLeft、`multiple_singleclick`→MultipleSingleClick）且忽略大小写（数据里有 `titleType="valueAndmax"` 这类杂写）；空属性值 = 枚举缺省。**枚举化当场逮到的漏洞**：`rightext-right` 关联漏计目标移动分量（只算了 grow）；缺 `rightext-left`/`topext-*`/`bottomext-*`/`size` 关联（已按 RelationItem 补全）；烘焙期 `pagination` 布局被当 column 排（运行时却当 flow_hz，两侧现统一为 flow_hz 排布）；容器关联忽略 `left-right`/`top-bottom`/`*-center` 变体（现映射到对应锚点）。

**测试必须"枚举式"而非"抽查代表"（2026-07-09 教训，用户点名）**：抽查一个 ComboBox(n1) 通过就断言"ComboBox 可用"是系统性盲区——同页的 Dropdown 变体(n4/n5) 静默退化成 Component、点了没反应却测不出来。修法是加**枚举式两层网**：① 结构层 `ConverterCompletenessTests` 扫每个组件源 XML，凡 `extention` 声明了交互面就断言烘焙 prefab 挂了对应可交互运行时类型（不退化成 Component）；② 行为层 `InteractionRuntimeTests.Every_combobox_on_the_page_opens_a_dropdown` + `PageInteractionReachabilityTests` 逐个真实点击页内**每个** active 交互元素、断言各自都命中自身并解析到 handler（下拉须解析到下拉自身，不被内部 button 面抢走）。**新增交互能力后，测试也要按"页内每个同类元素都扫一遍"写，不许只驱动一个代表。**

## 当前已知边界

- Popup/ComboBox 外点关闭使用透明 blocker，表现为模态；这是为了避开新 Input System 下旧 `UnityEngine.Input` 的异常。
- `ScrollPane` 拖动/滚轮/滚动条 grip 拖动/轨道点按翻页/惯性回弹已支持，且 `overflow=scroll` 组件由 `ScrollPaneHost` 自动挂载（无需胶水）；仍无虚拟化、无分页吸附、无下拉刷新。
- 按钮关联控制器（tab/单选组）、列表单/多选（`ListSelection` + `onClickItem`）、Slider `min/reverse/wholeNumbers/changeOnClick`、Window frame 拖动、右键指针处弹菜单均已烘焙，通用工程开箱即用。
- ComboBox 下拉已按 `visibleItemCount` 裁剪滚动。**无 button 控制器的 Dropdown 变体现也接下拉**（2026-07-09 修）：codegen 无 "button" 控制器时补占位 `enum button` 让其仍是 `ComboBox<button>`（占位 enum 与 "button" 子节点的 `m_button` 字段不同名不冲突），不再退化成 Component；且给每个 ComboBox 烘一张盖满全 rect 的透明 `comboHit` 射线面作最后子物体——否则内部独立 button 子组件（Dropdown 的面）会按 uGUI"点击只到最深 handler、不冒泡"抢走点击、下拉打不开。
- Grid demo 主要填可见文本，star/cb/mc 细节从简。
- 其余未做项见「发布范围与取舍」末尾"本轮明确不做 / 记为边界"。

Advanced 横纵组使用预烘焙原生 LayoutGroup 派生，保留交叉轴偏移和隐藏/零尺寸成员语义。列表 selectionController 双向同步使用直接 owner/索引和 typed gear，不运行时查名称。Graph.Kind.Polyline 复用既有 points/lineSize/lineColor，坐标从矩形左上开始，新增 roundEdge 仅控制开放路径端点。完整包内测试数以本文件顶部当前结果为准。

TMP 阴影使用导入期原生 SDF underlay 材质资产，不使用 Builtin 的 BaseMeshEffect。已发布字段可通过 Migrate.RefreshTextShadows 窄刷新，先烘材质再更新持久 prefab，保留层级/身份与嵌套继承；不得为修字体阴影重建文字对象系统。原生 SDF padding/字号缩放与 fallback preset 匹配设置仍是明确边界。

列表项点击中继在编辑期控制器初始化时也会预挂，必须使用独立同名 ListSelectionItem.cs 保持 MonoScript 序列化身份。导入检查包括强制重导、卸载/重载及嵌套再次保存；SaveAsPrefabAsset 的失败/null 结果直接抛异常，不缓存或报告迁移完成。已有丢失脚本的产物应按源重建。

阴影窄刷新保留生成字段的首个同名项规则；未公开的重复文本仅在Editor按原group/mask与同名兄弟顺序精确对应。公开引用或作者层级不符仍失败。新增跨组/同组同名、独立材质、保存重载、父继承与重复幂等回归；不为窄刷新增加运行时标记或重新生成整套界面。

导入器将预挂捕获 presentation 与源一同移入作者 group、mask 或 scroll 层级。已有发布定义可经同一依赖顺序窄修，只搬已验证的既有 companion，保留 GUID/local ID 与引用；直通图像颜色滤镜不变，Runtime 严格父层校验保留。新增三种真实导入/损坏保存/重载与幂等检查、两个失效绑定正控和一项三模式 GPU 回归。

生成式本地化与原生标题绑定共用首个作者 title 身份；后续同名文本走既有 private typed 引用，保留公开首项、语言 family 名称和翻译 key。新增五项 Editor 与一项 Play 回归，覆盖 Button/Label/ComboBox 生成、跨组同名文本、持久化重载和独立语言切换；不增加运行时名称查找。

发布时清空列表通过共享的编辑期 XML 投影处理：明确启用 `autoClearItems` 的预览项不进入 prefab、资源依赖和生成式本地化；源文件及 defaultItem 保留，运行时正常 Fill 和停启不会清空动态项。缺省/false 的作者项保持。已有产物需重生成受影响脚本并在编译后重烘 prefab/语言资产。

按钮实例的 `titleColor` 在导入期更新标题当前颜色页，再应用作者选中态；其它页和缺省颜色不变。保存重载、显式页与缺省页、无颜色 gear、选中还原及 Canvas 停启有包内回归。运行时不查作者 XML。
