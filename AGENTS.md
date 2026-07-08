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

- Runtime 基础组件尽量与 FairyGUI 的 `GObject` 子类 1:1 对应，例如 `Text`、`Button`、`Label`。不带 "G" 前缀
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
- 充分利用新特性，使用当前 Unity 版本能够支持的最现代的写法。

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

## 当前已知边界

- Popup/ComboBox 外点关闭使用透明 blocker，表现为模态；这是为了避开新 Input System 下旧 `UnityEngine.Input` 的异常。
- `ScrollPane` 只实现拖动滚动，无惯性、回弹、虚拟化。
- ComboBox 下拉当前撑开显示全部项，未按 `visibleItemCount` 裁剪滚动；无 button 控制器的 Dropdown 变体不接下拉。
- Grid demo 主要填可见文本，star/cb/mc 细节从简。
