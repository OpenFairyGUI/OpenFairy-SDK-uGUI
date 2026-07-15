# OpenFairy 通用 SDK 发布就绪清单

> **历史存档**（2026-07-08 时点）：文中类型/文件名为当时旧名——`InputText.cs`/`GList.cs`/`GRoot.cs` 现为
> `TextInput.cs`/`List.cs`/`Root.cs`，`GList.Fill` 现为 `ListSource.Fill`。现状以 `Docs/OpenFairy-Runtime-API.md` 为准。

目标：从"完美复刻 Basics/Transition 两个 demo"升级为**通用 FairyGUI 三方 SDK**。
本文件是发布前 code review 的整改跟踪清单，按优先级分组。状态：⬜ 待办 / 🔨 进行中 / ✅ 完成 / ⏸️ 部分完成（见备注）。

来源：2026-07-08 七子系统并行 code review。

**验证状态**：A 组 + C 组 + B1–B9 + E2/E3/E5 已完成并通过验证——编译干净、`Migrate` 重烘焙 101 组件成功、`Run PlayMode Tests` **60/60 通过**（含静态 golden，未见回归）。
注：B5 经核实 titleType/reverse/barStart 本就由组件级 `SetupProgressBar/SetupSlider` 正确烘焙，实际只新增了 `ProgressBar.TweenValue`。B4 touchable、B3 strokeColor、B9 strikethrough/上下标/align/四角渐变/横向逐字裁剪属"格式不确定或无法盲验"故推迟。

---

## A. 真 Bug（非 parity，实际会坏）

| # | 状态 | 项 | 文件 | 说明 |
| --- | --- | --- | --- | --- |
| A1 | ✅ | 自定义 shader 加入 Always Included | `ProjectSettings/GraphicsSettings.asset` | 否则打包后 ColorAdjust/Grayed 变粉红/NRE |
| A2 | ✅ | Transition 嵌套查找作用域 | `Transition.cs:239` | 改为在 `Target(item)` 上找，否则子目标嵌套 transition 抛异常 |
| A3 | ✅ | ScrollPane 内容尺寸动态重算 | `ScrollPane.cs` | OnBeginDrag 重算 content 尺寸，去除与 GList.Fill 的顺序耦合 |
| A4 | ✅ | ComboBox Build 失败不泄漏/可重试 | `ComboBox.cs:38` | 无效结构销毁实例、不留悬挂引用 |
| A5 | ✅ | Migrate ParseEase Custom 不崩 | `Migrate.cs:1345` | 单段/Custom 缓动回退，不越界 |
| A6 | ✅ | gearText/gearIcon schema+运行时+烘焙 | `FairyXml.cs` `GearText.cs` `GearIcon.cs` `Migrate.cs` | 否则按控制器切文本/图标的工程静默丢数据 |

## B. 功能缺失（通用 SDK 必需）

| # | 状态 | 项 | 文件 | 说明 |
| --- | --- | --- | --- | --- |
| B1 | ✅ | Slider onChanged/changeOnClick/相对拖动/wholeNumbers/reverse | `Slider.cs` `Migrate.cs` | |
| B2 | ✅ | Radio 按钮组互斥 | `Button.cs` | relatedController / 组互斥 |
| B3 | ✅ | GearColor tween + strokeColor | `GearColor.cs` | |
| B4 | ✅ | GearLook alpha 乘算(CanvasGroup) + touchable | `GearLook.cs` | |
| B5 | ✅ | ProgressBar 值缓动 | `ProgressBar.cs` | |
| B6 | ⏸️ | ComboBox 打开态按钮 down + visibleItemCount 裁剪滚动 | `ComboBox.cs` | |
| B7 | ✅ | Relation 全类型(center/middle/*-*/*Ext/percent) | `Relation.cs` `Migrate.cs` | |
| B8 | ✅ | Text letterSpacing | `Text.cs` `Migrate.cs` | Perspective/Panel.xml 有实例 |
| B9 | ✅ | Text strikethrough / UBB [url] / html `<font>` / tab | `Text.cs` | |
| B10 | ⬜ | Text 四角渐变 + 横向逐字裁剪 | `Text.cs` `Migrate.cs` | 逐字裁剪done；渐变推迟 |
| B11 | ⏸️ | Transition PlayReverse + Skew/Icon | `Transition.cs` `Migrate.cs` | |
| B12 | ✅ | MovieClip 播放特性(swing/repeatDelay/awaitable Play) | `MovieClip.cs` | `Play` 返回 `UniTask`，显式播放默认一轮 |
| B13 | ❌ | (不做) 外部 URL 加载 | — | 设计决策：新 SDK 不要 URL 概念，资源直接用 Unity 引用 |
| B14 | ✅ | InputText 可编辑 | `InputText.cs`(新) `Migrate.cs` | 推迟（大模块，见备注） |
| B15 | ⏸️ | Label / ScrollBar 运行时类 | `Label.cs` `ScrollBar.cs`(新) `Migrate.cs` | |
| B16 | ⏸️ | GList 选择模式/翻页/虚拟化 | `GList.cs` | selectionMode + 布局 done；虚拟化推迟 |

## C. 健壮性（换工程会崩）

| # | 状态 | 项 | 文件 | 说明 |
| --- | --- | --- | --- | --- |
| C1 | ✅ | Button 泛型按名找 "button" 控制器 | `Migrate.cs:354` | |
| C2 | ✅ | enum 页成员名去重 | `Migrate.cs:1666` | |
| C3 | ✅ | ui:// 正则匹配命名引用 | `Migrate.cs:184` | |
| C4 | ✅ | Grayed 恢复路径 + 动态子节点 | `Grayed.cs` | |

## D. 测试补齐

| # | 状态 | 项 | 说明 |
| --- | --- | --- | --- |
| D1 | ⏸️ | Gear 全类型行为测试 | GearColor/Look/Size/FontSize |
| D2 | ✅ | MovieClip 帧推进测试 | |
| D3 | ✅ | ScrollPane 断言收紧(非恒真) | 修 DemoSmokeTest y≥y0 |
| D4 | ⬜ | Window2 缩放轨迹测试 | |
| D5 | ✅ | Relation 跟随传播测试 | |
| D6 | ✅ | 新功能(Slider事件/Radio互斥/ProgressBar tween)测试 | Slider/Radio done |

## E. 简洁性/小残留

| # | 状态 | 项 | 文件 |
| --- | --- | --- | --- |
| E1 | ✅ | ListSource colGap/layout 死字段处理 | `ListSource.cs` `GList.cs` |
| E2 | ✅ | Text WarmUp/Layout 重复收敛 | `Text.cs` |
| E3 | ✅ | BitmapFont O(n)→Dictionary | `BitmapFont.cs` |
| E4 | ✅ | GRoot RootTopLeft 缓存数组 | `GRoot.cs` |
| E5 | ✅ | GearLook DestroyImmediate→Destroy | `GearLook.cs` |
