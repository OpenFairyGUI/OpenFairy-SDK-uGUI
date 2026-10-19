# Basics / Transition 实现状态

本文记录 FairyGUI `Basics` / `Transition` 示例在 OpenFairy 中的当前覆盖状态。历史开发过程和临时排查结论已整理掉；日常操作流程以 `AGENTS.md` 为准。

## 当前结论

### 2026-10-20 分层 prefab 发布与描边测量

- 按组件依赖层批量保存互不依赖的 prefab，停止资产编辑并取得持久化引用后才构建父层；新材质、字体与内联图片仍在暂停导入之前创建。提示框和默认滚动条的依赖闭包先完成，组件实例的 ComboBox dropdown 也计入依赖。
- 新增三项真实导入检查覆盖三层嵌套、生成字段、字体/内联图片/材质首次保存、稳定重导及下游重复导入。受控夹具的下游导入次数由3降为2；不据此声称完整工程的提速倍数。
- TMP 描边像素检查从无描边字形选取中间行，并复用于描边图；避免不同默认字体的基线使固定窗口计入字帽外扩。生产描边公式、预期宽度与容差均未改变。
- 检查点 `02ef1a9` 的交互包级结果为 Editor 220/220、PlayMode 223/223，无失败或跳过；最初的描边测量失败和批处理缺少渲染帧的失败保留。后续静态按钮生成合并另外通过三项发布检查及 TMP/Builtin 八程序集编译，不将旧完整套件结果当作新合并版完整复验。这里不包含传统完整 Demo/golden、Builtin 原生整套或 Player 验证。

### 2026-10-20 FairyGUI 一致性与精简复审

- 第二轮：组件按 opaque（缺省 true）在根节点烘焙透明命中面，Image/MovieClip 与 FairyGUI 一样可命中并挡住下层；列表 renderOrder（descent/arch）只改兄弟序，项索引、位置与选择不变，原生行布局用 reverseArrangement 保持位置。Builtin 省略号按 _ellipsisCharIndex 规则落在最后一条完整容纳行的倒数第二字（不换行时为首个越界字前两字），裁剪模式下越界后续行与越过右边的字形不绘制；换行符占零宽字符位。restrictSize 的最小尺寸经 LayoutElement 约束自适应，文字按最大宽度换行；按钮/标签实例 titleFontSize、Icon 动效已导入。反向遮罩 reversedMask 需要自定义模板着色器，仍是明确边界。按钮/下拉的视觉状态页改为生成代码按 "button" 控制器页名静态给出（`StatePage` switch），运行时不再按字符串解析枚举；旧生成脚本仍可编译，重新 Migrate 后恢复状态页。
- 动效目标改为导入期烘焙的组件引用，嵌套动效先建齐同组件全部动效再解析，失效嵌套项保留序号但不生效；运行时不再 GetComponent 或按名称查找。Builtin 投影改用 uGUI Shadow，描边改为 Shadow 派生的四轴复制。TMP 后端补齐描边（SDF outline 加同宽 face dilate，外扩宽度经真实渲染测得与作者宽度一致）、HTML font/strike、UBB align/font 与链接保留外层颜色。

- 按钮状态逐条对齐 GButton：缺页回落首页、无 over 页不跟踪悬浮、拖出后松开回到当前态；新增 `downEffect`（dark/scale）导入与运行时效果，scale 绕中心缩放且实例默认中心轴心。ComboBox 对齐 GComboBox：按下开关下拉、打开期间保持 down 页、下拉宽度跟随组件、高度按 visibleItemCount 与行距并保持作者高度差。
- Transition 倒放交换起止值、缓动正向推进；重播前先完成上一轮；autoPlay 随启用重播、停用时原地停止（Shake 归位）。Gear tween 终点不变时继续、改变时先完成再重起；GearLook 旋转按不取模角度线性插值。
- Builtin TextField：字距只加在有宽度字形之间并随缩放，行距随缩放；Shrink 改为 DoShrink 算法；裁剪按完整高度；空文本 0×0；UBB 标签不分大小写、支持 `\[` 转义、未实现样式的标签被消费而非原样输出；链接保留外层颜色。行内图片按运行时同一标签口径烘焙，陈旧图片标签在导入期移除。
- 导入：置灰按 disabled 页 → grayed 控制器 → 灰度滤镜的顺序，只取其一；解析 `fillOrigin`；TMP 输入框去掉底框与内边距，TMP 字距/行距换算为 em 单位。删除无用转发与死代码（`ControllerBinding`、`HidesRuntimeMember`、`Transition.SetHook(string)`、`TextRendering.SetBestFit` 等），六处编辑态销毁合并为一处。
- 验证：窗口模式 Editor 下 Builtin 套件 **191/191 EditMode**、**PlayMode 190 通过 / 0 失败 / 6 不确定**。原先 12 项像素断言的失败来自线性色彩空间：其中 6 项只是用 sRGB 渲染目标回读，改为线性渲染目标后通过；另 6 项的期望值按 Gamma 空间混合计算，工程临时切到 Gamma 后 12/12 通过，现以 Assume 声明该前提。FairyGUI 与 OpenFairy 的颜色矩阵着色器在线性工程中同样先把顶点色转到线性空间，两者一致。GearClock 位置断言改为与尺寸一致的容差。
- Demo 全流程（Migrate 199 个组件 → FairyGUI 对照截图 27 页 → 全部 PlayMode 测试）在窗口模式 Editor 中跑通：当前 **296 通过 / 48 未通过（含 6 项 Gamma 前提不确定）**，原始代码同流程 **283 / 61**；除 6 项改为不确定外无新增失败，13 项转为通过。静态对照 11 页的坏像素比例与原始代码逐位相同（Button 略降），差异集中在字形抗锯齿边缘；其余未通过的 Demo 交互/导航测试在原始代码上同样失败。
- 修复生成代码：名为 page/controller 的控制器字段会被 Get/SetControllerPage 的同名参数遮蔽导致编译失败（Bag/BagWin），生成的控制器访问统一经 this。Demo 工程 Publish.json 恢复为 Demo 胶水与对照测试所依赖的 memberNamePrefix="m_"、ignoreNoname=false。
- Windows 64 位 Player 构建成功（Build Finished, Result: Success）。
- TMP 后端 EditMode 整套（窗口模式，临时启用 FAIRYGUI_TMPRO 与 TMP Essential Resources）：当前 204/218，原始代码同环境 202/216，未通过集合逐项相同（14 项均为 Essential Resources 自带字体资产缺少 m_SourceFontFile 的环境问题），无新增失败。AutoClearItems 导入测试要求 Assets/UIProject/Assets 已存在（即已跑过 Migrate），空工程首测会因 CreateFolder 父目录缺失失败。临时导入 TMP Essential Resources 后 TMP PlayMode 与原始代码对照 **175/221 对 169/215**，失败集合相同（批处理渲染帧与缺源字体的 TMP 默认字体），新增描边与标记转换回归在该环境通过；TMP EditMode 在批处理下改动前后均运行出错。传统 Demo/golden、TMP 完整套件与 Player 未重跑；更新 SDK 后需重新 Migrate。

### 2026-10-19 生成式名称与引用

- 作者名字与 C# 标识符分离。保留原物体和动效名称，控制器操作继续按原名称/页ID解析；生成类、包命名空间、字段、嵌套 enum、页成员、动效 Hook 和本地化成员在各自作用域分配唯一标识符，保留直接序列化引用。
- 支持数字开头、关键字、标点空格及 Unicode；空物体/控制器名有生成占位符，空页名仍用页ID。不同作者名转义后相同不会丢项；完全同名子物体仍只公开首项，其余本地化引用保持独立。原生保留成员和生成式成员均参与冲突分配。
- 生成的类型引用使用 global::，避免作者类型或命名空间遮蔽系统/SDK类型。索引阶段失效编辑期名称表，按已索引的包/资源ID稳定分配；不新增运行时名称查询或缓存。旧独立枚举清理不删除当前组件拥有的脚本路径。
- 同组七份真实生成代码在旧实现下有32个编译错误，修复后独立编译零错误；编入测试程序集后验证 Prefab 保存/重载、跨组件与跨包字段、状态页、文字 gear、六个独立 Hook 和整根停启。最后专项 **8/8 Editor、1/1 PlayMode**；TMP/Builtin 各四程序集零错误，Builtin 仅此引用编译范围。
- 最终 TMP 完整套件在交互 Editor 中为 **215/215 Editor、215/215 PlayMode**，无失败、跳过或不确定项，包含生成夹具与真实渲染帧检查。交互首轮因外部命令请求超时的错误日志为214/215；同版重跑全部通过，未改断言或运行时。
- 最终后台完整套件为 **214/215 Editor、205/215 PlayMode**；未得到实际渲染帧及 batchmode 的 WaitForEndOfFrame 失败保留。交互通过不代表批处理行为也通过，TMP/Builtin 八程序集编译通过也不代表 Builtin 原生整套复验。传统完整 Demo/golden 和 Player 未重跑。


### 2026-10-19 Builtin 文字缩放与省略

- Builtin TextField 增加序列化 OverflowMode，由作者 autoSize 的 Shrink/Ellipsis 直接烘焙。Shrink 在原有排版中按最大可容纳字号比例重新测量动态/位图字形，保留原始文本、字号和矩形；动态字体富文本的绝对字号参与缩放，字体大小 gear 与区域变化会重新结算。
- Ellipsis 保持作者字号，在最后可见行保留可容纳的前缀和 U+2026，不输出隐藏文字、下划线、链接或图片布局。行内图片在烘焙时包含初始隐藏项并保存引用，后续仅更新位置和显隐；不在排版中创建或销毁对象。
- 新增 16 项运行回归和 7 项真实 Prefab 导入/重载回归，覆盖单/多行、动态/位图、富文本、尺寸恢复、字号 gear、九种对齐、空行和图片复用。同一运行夹具在恢复旧排版主体后 16 项全部失败，修复后通过。独立 Builtin 完整套件为 **181/181 Editor、189/189 PlayMode**，无失败或跳过；下方历史结果不与之累加。
- 新建语言表和语言集合在 CreateAsset 首次序列化前填充内容；先创建空资产再反射赋值，会在首次重导时读回空表。新增强制重导的首写回归在旧实现下失败，覆盖初始文件内容而非只检查尚未保存的托管对象。
- 同一源码另通过 TMP 后端 **207/207 Editor、214/214 PlayMode**；使用可渲染 Built-in 测试工程，TMP 默认测试字体保留有效源字体引用。语言资产首写修复也消除了列表发布夹具先运行时触发的空表失败。未重跑传统完整 Demo/golden 或 Player 构建。

### 2026-10-19 原生指针扩展验证

- `Button<T>` 的 Enter/Exit/Down/Up 原生指针方法可覆写；业务 partial 调用基类即可保留视觉状态，无需额外事件中继组件。包内回归通过 EventSystem 的 ExecuteEvents 枚举 Common/Check/Radio、选中/未选中、灰显/正常以及左/右键，检查派生方法分发和基类页面状态。
- 按钮停用时清除瞬时按下和悬浮状态，避免复用后停留在旧的悬浮页；选中与灰显保持原值。新增停启回归枚举全部模式、选中和灰显组合，未修复时负控失败，修复后通过。
- 本轮独立 Builtin 工程的完整包级 Test Runner 结果为 **157/157 Editor**、**169/169 PlayMode**，失败、跳过、不确定项均为零。没有重跑 TMP 后端的完整套件或传统 Basics/Transition golden；下方历史结果保留各自验证范围。

### 2026-10-19 按钮实例标题颜色

- 导入实例 `titleColor` 时，同时记录标题颜色 gear 的当前页；没有显式页时追加该页，不覆盖缺省或其它状态页。作者选中态在颜色覆盖之后应用，取消选中仍恢复实例的初始页颜色。
- 六项 Editor 回归覆盖显式与缺省页、未覆盖对照、无 gear、独立源 prefab 和保存重载；两项 PlayMode 回归覆盖状态切换及整个 Canvas 停启。完整 TMP 包级结果为 **199/199 Editor**、**199/199 PlayMode**，无失败或跳过。TMP/Builtin 各四程序集离线零错误；未据此扩大 Builtin 原生、传统 Demo/golden 或 Player 范围。

### 2026-10-18 当前通用包能力与验证

- 滚动交互使用原生 `ScrollRect`/`Scrollbar`，保留虚拟窗口、对象池、变高行、循环索引和选择；显式滚条、分页/吸附标志与页面控制器在导入期烘焙。
- 增加原生 UI 对象捕获、对象模糊和容器颜色滤镜，共用捕获、预乘透明度、遮罩及释放生命周期。管线通过同步渲染回调接入；刷新不挂在 `Canvas.willRenderCanvases` 上。捕获扫描使用对象内复用 List，网格仍按引擎借用语义读取。
- 提示框使用作者配置的 Label prefab 直接引用。普通文本自动尺寸交给原生 `ContentSizeFitter`；根关系与尺寸 gear 尊重内容拥有的自动轴。立即显示提示框时刷新实际文字布局节点，避免非布局容器阻断子树遍历。
- 输入框通过 Input System 支持焦点期五步撤销/重做、选区恢复和单行 Tab 导航，程序赋值清除历史。灰显保留视觉语义，命中与点击由独立触摸/交互通道决定。
- 生成器支持发布命名空间前缀，避开 Unity 原生序列化保留字段。未绘图的 Graph 节点生成 `RectTransform` 引用。Transition 使用各自动画的帧率，缺省仍为 24。
- 该历史检查点源码在 Unity Editor/Mono 的完整 Test Runner 结果为 **199/199 Editor**、**199/199 PlayMode**，失败、跳过及不确定项均为零。TMP 与 Builtin 两种文字后端各四个程序集离线编译零错误，Builtin 不据此视为原生行为通过。此前104/119及更早结果为历史检查点，不与当前总数相加；传统 Basics/Transition 历史 golden 套件没有重新运行。
- 灰显与已有颜色滤镜共用子树捕获，保留原生 TMP 字体材质以及独立的触摸/交互通道。十三种内置混合方式已有覆盖率和预乘透明度捕获回归，alpha 为零的发光 RGB 不被提前丢弃；离屏组对外部背景的 Multiply/Screen 等依赖不承诺等同直接画屏。
- 捕获保留原生 MaskableGraphic/Mask 的完整材质链，只在 owned 副本上还原 base 遮罩参数，再由代理重算内部遮罩。此前不同输入会使颜色或混合缓存释放源图仍在使用的材质，下一帧源图消失；两项跨帧 GPU 回归及增强的 TMP 图文混排现已验证源图、材质、原生 pop 和图集保持。
- 非虚拟 SingleRow 使用原生布局与实际子物体矩形，公开滚动、吸附和可见项查询不再假设固定宽度；虚拟列表保留既有窗口和固定行路径。Hidden 列表在导入期增加原生 RectMask2D，新增六项导入和一项 GPU 裁剪回归；不是消费端运行时补挂蒙版。
- Advanced 横纵组预挂原生 LayoutGroup 派生与 ContentSizeFitter：只推进布局轴，保留交叉轴偏移；零尺寸、隐藏成员与负间距按作者语义处理。真实 TMP 长短变化、成员零宽转非零、父尺寸传播及保存后实例化已 Native；撤去导入配置调用的负控确实失败。
- 列表 selectionController 在导入期烘焙直接 owner/索引与 typed GearListSelection，保留程序换页、点击/合法 selectedIndex 双向同步、清除/越界和多选重入语义；虚拟回收/refill与真实 prefab 保存重载已 Native。 独立点击中继脚本修复编辑期保存后缺失 MonoScript；新增损坏资产保存失败回归，失败不会再被当成完整导入。
- Graph 开放折线使用原生 MaskableGraphic 网格，保留左上坐标、宽度/颜色、百分比点及可选圆端点；不闭合或填充内部。Schema/实际配置与 GPU 裁剪、透明度正控均已通过。
- TMP shadowColor/shadowOffset 烘焙为直接引用的原生 SDF underlay 材质；白字白底、alpha、fallback 原生释放与捕获均已 GPU。已发布字段窄刷新经真实嵌套 prefab、GUID/local ID、重载与重复字节检查；不重建层级或运行完整发布。
- 整根 Overlay Canvas 停启保留已有 Relation 基线，避免 CanvasScaler 恢复期间的临时尺寸被重复累计；隐藏期间真实父尺寸和内容变化仍在恢复后同步。原生锚点只消化 anchor span 对应的尺寸差，fitter 自身改宽仍保持对齐。新增三项真实 prefab/Canvas 回归覆盖原生父缩放、同帧和隔帧内容变化及三种水平 pivot。
- 首次启用的 Relation 先恢复已烘焙的 ContentSizeFitter driven 尺寸，再记录关系基线，随后仍接受正常内容重排。新增真实 prefab 往返用例，不以未序列化的零尺寸重写作者位置。
- Transition 声音优先复用 Root 的音源/路由，无 Root 才使用预烘焙的独立音源；停用的对象或音源不发声，其余 timeline 动作仍照常推进。声音夹具在首次启用前配置音源、提供自己的 listener，八项专项及最终整套均通过；未把一次不确定构造时序认定为生产故障原因。
- 本轮夹具明确处理两种引擎时序：popup 关闭按实际结束条件等待并保留超时，不能靠短固定秒数；动态改变 maskable 的 GPU 正控调用 RecalculateClipping，确保未裁剪 lane 实际更新原生 clip parent。已有输入测试仍使用官方 InputTestFixture 隔离后台设备状态。
- 修正拉伸锚点改变pivot时的补偿：使用 `sizeDelta`，不能把父锚点跨度重复计入位移。组件实例及列表项的作者控制器设置在导入期按页ID、有序解析为既有静态控制器调用；缺省保留定义默认，失效名称/页ID按原语义跳过。布局结算放在控件配置/初始actions之后，避免将早期标题宽度保存进最终prefab。
- 捕获相机的 `useOcclusionCulling` 在新引擎有弃用警告，目前没有等价的逐相机新接口；保留隔离捕获的明确关闭语义。`CanvasRenderer.GetMesh()` 未弃用，不增加网格复制或改变借用所有权。
- 位图字体同域重导已补查找缓存失效。对象滤镜自动运行已接 `Application.onBeforeRender`，在关系 `PostLateUpdate` 同步之后由最外层滤镜驱动内层逆序捕获；保留显式刷新供 Edit Mode 和离屏工具使用。两次 Play 都禁用域与场景重载的原生会话测试已通过。先前失败来自测试持有的旧托管组件包装器；支持组件改按真实原生对象重新取当前包装器，没有为此改生产滤镜生命周期。
- 静态文本、本地化、selected icon、按钮声音和输入编辑已接普通导出流程。语言资产保留直接引用，生成方法使用 typed 行及变量槽；不在运行时解析资源 URL。
- Root 的 Canvas 分支采用原生 stretch anchors；非 Canvas 分支保留兄弟 overlay，复制 anchors/pivot/size/pose，原生尺寸事件和 `LateUpdate` 矩形/矩阵变化检测共同同步，位置/旋转/缩放变化也会跟随。公开设计坐标仍以实际矩形左上为原点。
- 完整 Demo 及重新生成的 golden 仍需单独验证。包内测试通过不能替代被稀疏 checkout 排除的 Demo 脚本、生成资产或传统文字后端的完整行为证明。

- Window/popup 使用原生组件、预烘焙关闭/拖动/Transition 引用；PopupInputModule 保留同一次下层点击，重开与关闭动画有明确取消语义。十项运行用例与一项prefab往返通过。Root继承设计层，两种UI相机GPU回归通过；否则同一红色popup会被layer mask剔除。
- Loader支持静态Component子树，保留原尺寸并整体缩放；readonly content与ShowContent不创建资源。十一项Runtime、两项真实导入往返通过，含native尺寸回调、movie切换、空mesh、遮罩/alpha GPU对照。原URL资源仍只在导入期解析。

### 2026-10-19 通用导入与 Builtin 后端验证

- 源目录、输出目录和依赖源目录可保存在 ProjectSettings，通过既有 Migrate 入口读取，域重载后的续建沿用配置。依赖源只参与实际引用的收集，输出保持包目录结构。
- 修正发布包的 Input System 依赖版本。TTF/OTF 资源在导入期生成可序列化的原生 Font 引用；Common.json 默认字体支持资源 URL，输入文字与提示文字保留该引用。
- 列表支持无默认模板的空容器和逐项模板覆盖，按实际项尺寸排列并序列化原生引用；不同模板的作者项不会进入默认模板的重填池。控制器与子节点同名时生成独立字段，代码、Prefab、本地化和阴影刷新共用绑定规则；作者重复子节点仍保留首项公开引用。
- 非滚动条资源不进入滚动条组件收集或实例化；没有标题的组件忽略无效果的标题覆盖。Blit 专用颜色矩阵 Pass 单独标记，避免 Builtin UI 绘制额外执行该 Pass，保留直接图像的透明度和裁剪。
- 新增十项字体、八项导入边界与三项滚动条资源回归，包含真实 Prefab 保存、重导和实例化；缺省/空字体仍采用默认字体族，混合模板另验证默认重填池。折线网格测试显式选择 VertexHelper 重载。
- Root 可预烘焙在原生 Canvas Prefab 中；音源与设计根尺寸绑定保留序列化引用，实例化时注册当前 Root。两项真实 Prefab 保存、重导及 PlayMode 实例化回归覆盖 Canvas 子层和非 Canvas 兄弟层，四种列表布局的重填池行为放在实际 PlayMode 验证。
- 文本发布的 `autoClearText` 同时作用于原生初始文字和生成式本地化，避免设计预览文字重新出现；九种文字类型/标志组合已有回归。Button/Label 的 icon 可经嵌套 Button/Label 在导入期绑定到末端 Loader，运行时仍使用直接序列化引用，四种组合的 Prefab 保存、重导和赋值均已验证。
- 控制器补间显式使用引擎 Normal 更新，保留缩放时间语义，不继承宿主的 DOTween 手动更新默认值；四类补间分别覆盖运行、暂停和恢复。原生滚条在显示比例为零或一时隐藏 grip，四方向均验证恢复溢出后的显隐。`ListSource.GetItemAt` 按逻辑索引取得作者项或已绑定项，重填后的对象池顺序不影响访问；未绑定索引明确抛出异常。
- 首次启用的 Relation 保留序列化的作者关系基准，避免 CanvasScaler 的临时尺寸覆盖烘焙值；两项真实 Prefab 往返测试覆盖位置与扩边关系。非列表滚动内容在原生布局变化后更新范围和 grip，动画滚动显式使用不受缩放的引擎时钟；新增两项运行回归覆盖动态内容和手动补间默认值。
- 普通 Image/MovieClip 不阻挡底层按钮的原生射线，Loader 保留自己的命中面；三项 Prefab 往返及 PlayMode 射线回归分别覆盖这些类型。列表回收保留业务 Awake 注册的点击监听，Fill 回调自行替换每次绑定的业务监听；选择中继以绑定版本取消重填留下的在途回调，避免旧点击作用于回收后的项。普通与虚拟列表均覆盖四种布局及点击期间重填为空、一项和多项。
- Builtin 输入提交在导入期以持久 UnityEvent 绑定到原生 InputField.onSubmit。聚焦的 InputField 会消费 EventSystem 导航事件，不能用同节点的 ISubmitHandler 代替文字提交；失焦仍是独立的 onEndEdit。两项真实 Prefab 往返及 PlayMode 回归覆盖单行/多行、聚焦、失焦及原生提交文本。
- 作者预置的 SingleRow 项首次按逻辑索引读取后，原生尺寸采样会初始化缓存并应用交叉轴自动尺寸；不要求先 Fill。新增运行回归枚举自动尺寸与滚动容器的四种组合，并验证后续宽度变化。
- Unity 6000.4.2f1 中 Builtin 后端完整包内测试 **157/157 Editor、167/167 PlayMode**，失败、跳过和不确定项均为零；GPU 回归在可渲染 Game 视图的编辑器中运行。此处数量包含新增回归，不与上方 TMP 后端的历史 141/175 相加。TMP 后端和传统完整 Demo/golden 尚未在本轮重验，消费端仍须单独验证。

### 历史示例检查点（不代表当前版本重验）

- `Basics` demo 的静态渲染、主要交互、窗口/弹窗、拖拽、滚动、下拉、文本链接、列表填充、翻页动效已完成。
- `Transition` demo 的主要动效已完成，路径、嵌套 transition、滤镜、shake、循环/yoyo 等运行时行为已覆盖。
- 当前公开 Unity 菜单只保留：
  - `Tools/OpenFairy/Migrate`
  - `Tools/OpenFairy/Generate Golden References`
  - `Tools/OpenFairy/Run PlayMode Tests`
- 最近一次验证：2026-07-10 `Migrate` 成功迁移 `101` 个组件，`Run PlayMode Tests` 通过 `134/134`；静态 golden、真实射线交互、转换完整性、池化复用及 `RelationSide` 全枚举回归均通过。
- 2026-07-10 依赖替换：`TransitionPath` 的曲线求值改用官方 `com.unity.splines`（直线三等分控制点、二次贝塞尔升阶、CR 样条按标准 Catmull-Rom→三次贝塞尔换算，均精确等价，段长权重保持 FairyGUI 口径；1001 采样点新旧最大偏差 6.3e-5 px）；静态 golden 测试的像素对比改用官方 `com.unity.testframework.graphics` 的 `ImageAssert`（`IncorrectPixelsCount`+`DeltaGamma`、`PerPixelGammaThreshold=2/255`，口径与旧手写 `DiffRatio` 一致；截图/参照统一 ARGB32）。`Docs/RenderDiff` 的人工查看 diff 条仍由 `BasicsRenderDiff` 自产。测试失败时框架会写 `Assets/ActualImages/`（已 gitignore）。
- 本轮发布整改：修 `ComboBox.selectedIndex` 程序化赋值不刷新/不发事件的 bug（改属性 + 加 `values/text/value`，并支持从 XML `<item value>` 烘焙字符串值、程序化设置 `text`/`value`）+ 下拉按 `visibleItemCount` 裁剪滚动；`ScrollPane` 补滚轮/滚动条 grip 拖动/惯性回弹/`onScroll`/`ScrollToView`；`List.Fill` 在已挂 ScrollPane 后重填会保留并刷新 `__scrollHit`，同时自动重绑 `ListSelection` 且避免重复 listener；`ProgressBar.TweenValue` 加 ease、`Slider` 加 grip touch 事件；修 `MovieClip.SetFrame` 越界不钳导致自播 Update 越界的 bug；`Window.modal` + `Root` 模态层；`PopupMenu.AddItem` 返回项按钮 + `ClearItems`；`TextInput` 加 `password`/`maxLength`/`editable`/`onSubmit`。转换器健壮性整改（对非 demo 的真实工程）：包名/空名 sanitize 进命名空间、非 button list item、陈旧 gear 页 id、`Image`/`RectTransform` 字段全限定、内嵌 `ui://` 容错、生成控制器 enum 与 runtime 基类成员同名时显式 `new` 消除隐藏警告。
- 本轮性能优化：参照 FairyGUI `GObjectPool` 思路，`ListSource` 基于 `UnityEngine.Pool.ObjectPool<GameObject>` 复用动态列表项，`PopupMenu` 复用菜单项；`ComboBox` 下拉改为禁用 prefab 自带 `ListSelection`/`ScrollPaneHost`，并调用 `List.Fill(..., false)` 跳过普通列表选择重绑。进一步把 TextField 逐字符数据从整份 `Run` 复制压缩为 run 索引 + 缓存 advance，`Relation` 同帧合并 RectTransform 写入，列表项每轮只扫描/复位一次按钮；`Transition.Play`/`PlayReverse` 与 `MovieClip.Play` 统一返回 `UniTask`，删除平行完成回调；Popup 关闭改用池化 `AutoResetUniTaskCompletionSource`；PopupMenu 项点击统一走 `ButtonBase.onClick`；四种 tween Gear 的 tweener/display lock/完成委托生命周期上提基类复用。
- 2026-07-10 工程打磨：`FairyXml` 按 Schema 类型缓存反射字段/特性和 enum alias，避免每个 XML 节点重复扫描；生成脚本旧 `Text/Shape/InputText` 与裸 `Image/RectTransform` 死锁遗留已清理，并加全目录回归；全工程移除 `GetComponent() ?? AddComponent()`，规避 Unity 6.4 `UnityEngine.Object` 假 null 导致的 `MissingComponentException`；FairyGUI vendored SDK 的对象查找调用升级到 Unity 6.4 非弃用 API；PlayMode 结果文件会附失败堆栈。
- 真跑才暴露的三个 bug（已修 + 新增真射线交互测试 `InteractionRuntimeTests`）：① MovieClip 页空白（`<jta>` movieclip 标签被丢弃，现已识别）；② Popup/Window 按钮点不动（button 根加透明 raycast 面，整块可点）；③ Text 输入框不工作（真因是 `OpenFairy.UGUI.TextField` 作 InputField 面时 `OnEnable` 清 raycastTarget + 自绘不填 generator；改用原生 InputField 结构：透明 Image 作 targetGraphic + 普通 UI.Text 作 textComponent，placeholder 仍 OpenFairy.UGUI.TextField。`activeInputHandler` 保持 `1`）。详见 `AGENTS.md`。
- `InteractionRuntimeTests` 覆盖真射线交互：MovieClip 自播、Window 开关、Popup 开/点项收起、ComboBox 开下拉、Slider 可点跳值、TextInput 可编辑读写。
- 2026-07-09 交互烘焙完整化：让**转换产物在无 demo 胶水下也具备完整交互**（通用 SDK 发布前提）。补齐按钮关联控制器(tab/单选组)、`overflow=scroll` 自挂 ScrollPane、Slider `min/reverse/wholeNumbers/changeOnClick`、列表选择 + `onClickItem`、ComboBox onChanged 语义、输入框 `SetTextWithoutNotify`/只读/回车提交、Window frame 拖动、右键指针弹菜单、GearLook 置灰传按钮、Depth 前移越位修正。新增 `BakedInteractionTests`（直接实例化 prefab、无胶水、真射线驱动）证明通用工程已具交互。详见 `AGENTS.md`「交互烘焙完整化」。
- 传统文本分支历史 `Run PlayMode Tests` `134/134`。范围与取舍详见 `AGENTS.md`「发布范围与取舍」。

## 2026-09-08 全局 TMP 变更

- `FAIRYGUI_TMPRO` 现在全局选择原生 TMP 文本/输入组件，已移除逐元素 `tmp` 开关。字体从明确覆盖、原字体名或 TMP Settings 默认字体解析；生成字段和常用控件、gear、动效均按宏选择后端。
- Nuo 集成工程在 Unity 6000.5.6f1 发布后审计 16 个 Prefab：28 个 TMP 文本实例，0 个传统 UI.Text，生成字段引用无缺失。生产 UI 源无需 `tmp` 属性。
- 使用该 Editor 的实际编译引用和 Roslyn，开启/关闭宏分别编译 Runtime、Editor、Assembly-CSharp，六次编译均通过。原生 Play Mode 验证已发布的 TMP 输入框接受中文与拉丁字符、静默赋值事件语义、UBB 透明度顶点颜色、文字/字号 gear，以及 Nuo 对话与任务流程。输入框测试使用已有发布夹具，未声称额外的无标记输入夹具重新发布已执行。
- 当前集成工程不包含 SDK 的 Basics/Transition 测试运行器和 golden。尚未运行 TMP 分支完整 parity 套件，历史 134/134 仅对应此前传统后端。TMP 当前 UBB 适配范围及未验证的 HTML、图片、链接点击边界见 README。

## 2026-09-09 发布代码设置

- Migrate 读取 `Publish.json/codeGeneration` 的 `classNamePrefix`、`memberNamePrefix`、`ignoreNoname`。类名前缀同步用于脚本文件名、同包/跨包类型引用及 Prefab 类型查找；成员前缀同步用于普通字段、控制器绑定和置灰控制器。空成员前缀时控制器枚举追加 `Pages`，避免与字段重名。
- 默认名称过滤仅针对完整的 `n[0-9]+` 子元件名，保留 `title`、`icon` 等约定名称和所有实际元件。已有生成脚本更换类名前缀时，经 AssetDatabase.MoveAsset 保留 GUID。
- Nuo 已按 `UI_` / `m_` / `ignoreNoname=true` 完整迁移 24 个生成类、18 份手写调用/partial 扩展及相关 Prefab/场景类型引用，24 个脚本的 `.meta` GUID 保持不变。重建后按源 Prefab 身份保留了 Scene_04 的嵌套对象 ID，场景只同步类型标识。
- Unity 6000.5.6f1 实际调用转换器完成五组集成测试：自定义前缀、空前缀、过滤开启、过滤关闭、缺省配置。各组生成脚本并通过 Unity 编译，再烘焙/实例化 Prefab，验证成员引用、跨包组件类型、保留 title/icon、过滤不删除对象及控制器切页；全部通过。另有 14 个过滤表达式边界用例通过。
- 正式产物检查覆盖 24 个 Prefab 的 162 个对象字段，以及 5 个场景的 4 个序列化 UI 绑定：无缺失脚本、托管类型或成员引用。原始 Publish.json 已恢复，临时测试资产已清理，编辑器恢复 Scene_05。测试报告与复现用验证器保留在 Nuo 的 `Temp/CodexPublishSettings/`。本次未运行 SDK 的完整 Basics/Transition parity 套件。

## 标准验证流程

1. 修改转换器、生成脚本结构或序列化字段后，先跑 `Tools/OpenFairy/Migrate`。
2. 修改渲染参照、测试目录、FairyGUI 侧 dump 或首次缺少参照时，跑 `Tools/OpenFairy/Generate Golden References`。
3. 最后跑 `Tools/OpenFairy/Run PlayMode Tests`，结果写入 `Temp/OpenFairyPlayModeResults.txt`。

`Generate Golden References` 会同时输出：

- `Assets/Tests/Golden/ReferenceImages/*.png`
- `Assets/Tests/Golden/ReferenceImages/*.geo.json`
- `Docs/RenderDiff/*_strip.png`
- `Docs/RenderDiff/*_fairygui.png`
- `Docs/RenderDiff/*_openfairy.png`
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
- FairyGUI XML 通过 `XmlAttribute` / `XmlElement` / `XmlArrayItem` 标注的 `OpenFairy.UGUI.Editor.Schema` 类型和轻量自定义 converter reader 反序列化为 `Schema.Package` / `Schema.Component` / `Schema.Display` 等结构化数据，`Vector2`、bool 默认值、派生字段和有限选项 enum 在 schema 层落型，转换器后续逻辑不再直接读取 XML attribute。
- 烘焙配置来自 `settings/Common.json`（允许不存在，缺省值硬编码自 FairyGUI 新建工程默认值）；运行时字体由用户脚本按 `fontNames` 遍历替换（`Assets/Scripts/DemoFont.cs`）。
- codegen 已支持 C# 关键字转义、controller enum 内嵌、`Button<T>`、`ComboBox<T>`、`ProgressBar`、`Slider` 等基类选择。
- `ListSource`、ComboBox items/values/dropdown、Window1 列表已通过全量 Migrate 重烘焙；Example Grid 已改用通用 `List.Fill`，不再自挂 SDK 级滚动能力。动态 `List.Fill` 现复用 `ListSource` item 池，`PopupMenu` 现复用菜单项池。
- 2026-07-18 codegen 生成命名空间由 `UI.{包名}` 简化为 `{包名}`；命名空间声明、跨包组件字段类型和 Migrate 的 `FindType` 使用同一套 `Identifier` 结果；同包组件字段使用短类型名，跨包字段使用 `global::{包名}.{组件名}` 避免目标包名被同名组件类遮蔽，并全限定被全局包命名空间遮蔽的 Unity/FairyGUI 同名类型；现生成脚本及 prefab 序列化类型名已同步迁移；Unity 6.4 自带 Roslyn 已验证 `Assembly-CSharp`、Editor、TestSupport、PlayMode tests 与 EditorTestRunner 全部编译通过。
- 2026-07-19 `ButtonBase.icon` 与 `Label.icon` 一样支持可选的 icon Loader；按钮实例 XML 即使设置了 `icon`，模板没有名为 `icon` 的 Loader，Migrate 也会忽略该图标而不会空引用崩溃，并由 `NewFeatureTests` 覆盖该结构。
- 2026-07-19 支持 FairyGUI `blend` 属性及完整 `BlendMode` 因子表：Normal、None、Add、Multiply、Screen、Erase、Mask、Below、Off、One_OneMinusSrcAlpha、Custom1~3；Migrate 为图片、movieclip、图形、Loader 和文本的各 Graphic 烘焙 `BlendModeEffect` 与 shader 引用，Multiply/Screen 按 FairyGUI 预乘 alpha，图片类 alpha 通道固定 `One/One`、文本类随当前混合因子，并可与现有灰度/ColorFilter 材质组合。
- 2026-07-19 BlendMode 改动已用 Unity 6.4 自带 Roslyn 响应文件编译 Runtime、Editor 与 PlayMode tests 三个程序集；当前会话未连接 Unity MCP，尚未执行 Migrate、Shader 导入和 PlayMode 实跑，计入下次完整验证。
- Basics demo 的工程胶水通过 `[MigratePostProcess]` 自动配置，不再作为菜单入口暴露。

## 已知边界

- 组件级 BlendMode 在 FairyGUI 中依赖 PaintMode 先把整个组件渲染到 RenderTexture；OpenFairy 当前只实现图片、movieclip、图形、Loader、文本等 Graphic 级混合，不对组件子树做逐子图近似，避免与 FairyGUI 的整体合成语义不一致。
- Popup/ComboBox 外点关闭使用透明 blocker，表现为模态；这是为了避开新 Input System 下旧 `UnityEngine.Input` 的异常。
- `ScrollPane` 已支持拖动、滚轮、滚动条 grip 拖动、轨道点按翻页、惯性回弹；仍无虚拟化、分页吸附、下拉刷新。
- ComboBox 下拉已按 `visibleItemCount` 裁剪并滚动（项数超出时 `ScrollPane.Attach`）；无 button 控制器的 Dropdown 变体同样接下拉。
- Grid demo 主要填可见文本，部分装饰/交互细节从简。
- 文本仍有少量 UBB 混合字号断行、行内图片基线差异。
- GoodHit/PowerUp 中的 Shake 和部分 movieclip 相位差会放大像素 diff，但视觉表现已对齐。

## 渲染差异说明

以下差异属于当前可接受残差：

- 动态字体图集由两侧各自生成，边缘反锯齿会有 1px 左右差异。
- FairyGUI 图集带 duplicate padding，平铺图片边界可能出现约 1px 内容重叠；OpenFairy 使用独立原图平铺。
- 截图使用不透明背景色，避免 FairyGUI 直通 alpha 与 uGUI 预乘 alpha 在透明背景上的通道差异误判。
- Shake 使用随机偏移，两侧不追求同随机序列。

## 测试覆盖

- 静态 golden：`StaticGoldenTests` 比较 OpenFairy 末帧与 FairyGUI golden。
- 交互几何：`InteractionGeometryTests` 比较 slider、checkbox、按钮按下等交互终态几何。
- 轨迹行为：`PageTransitionTests` 覆盖翻页 display lock 动效轨迹。
- Main 导航：`MainNavigationTests` 覆盖 Main 初始页、20 个入口页真实 pointer 进入、子页面返回 Main，以及进出动效中态和终态。
- Demo smoke：覆盖 ComboBox、Depth/DragDrop、Grid、List、Popup、ProgressBar、Text、Window 等运行 demo 胶水。
- 专项测试：`DragDepthTests`、`TextLinkTests`、`WindowPopupTests`、`MainNavigationTests`、`NewFeatureTests`；其中 `NewFeatureTests` 覆盖 ComboBox 程序化 `text/value` 设置与 `List.Fill` 动态重填后的 ScrollPane/ListSelection 刷新。
- 通用工程交互（无 demo 胶水）：`BakedInteractionTests` 直接实例化转换 prefab，真射线驱动，覆盖 tab 换页、单选组互斥、`overflow=scroll` 自挂滚动、输入框结构、Slider 连续拖动。

上次完整 PlayMode 测试结果为 `134/134` 通过；2026-07-19 新增的无 icon Loader 按钮及 BlendMode 回归用例计入下一次完整测试。

阴影窄刷新保留生成字段的首个同名项规则；未公开的重复文本仅在Editor按原group/mask与同名兄弟顺序精确对应。公开引用或作者层级不符仍失败。新增跨组/同组同名、独立材质、保存重载、父继承与重复幂等回归；不为窄刷新增加运行时标记或重新生成整套界面。

导入器将预挂捕获 presentation 与源一同移入作者 group、mask 或 scroll 层级。已有发布定义可经同一依赖顺序窄修，只搬已验证的既有 companion，保留 GUID/local ID 与引用；直通图像颜色滤镜不变，Runtime 严格父层校验保留。新增三种真实导入/损坏保存/重载与幂等检查、两个失效绑定正控和一项三模式 GPU 回归。

生成式本地化与原生标题绑定共用首个作者 title 身份；后续同名文本走既有 private typed 引用，保留公开首项、语言 family 名称和翻译 key。新增五项 Editor 与一项 Play 回归，覆盖 Button/Label/ComboBox 生成、跨组同名文本、持久化重载和独立语言切换；不增加运行时名称查找。

发布时清空列表通过共享的编辑期 XML 投影处理：明确启用 `autoClearItems` 的预览项不进入 prefab、资源依赖和生成式本地化；源文件及 defaultItem 保留，运行时正常 Fill 和停启不会清空动态项。缺省/false 的作者项保持。已有产物需重生成受影响脚本并在编译后重烘 prefab/语言资产。
