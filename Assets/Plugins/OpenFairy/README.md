# OpenFairy

基于 uGUI 的 FairyGUI Runtime SDK。继续用 **FairyGUI Editor** 编辑 UI，用 OpenFairy 把 FairyGUI 工程转换成 **uGUI prefab + C# 代码**，并尽量还原 FairyGUI 的渲染、动效与交互表现。

## 设计取向

- **烘焙优先（bake-first）**：布局、皮肤、关联、动效结构在转换期烘进 prefab；运行时组件只做薄薄的行为层。
- **复用 uGUI 原生**：能用 `RectMask2D`、`CanvasGroup`、锚点拉伸、`InputField` 等原生能力就直接用，不为对齐 FairyGUI 签名造并列封装。
- **静态强类型**：controller 生成为组件类内嵌 enum + `Controller<enum>`；gear 按 controller enum 泛型化；有限选项用 enum 而非字符串。

## 目录

- `Runtime/`：运行时 SDK，命名空间 `OpenFairy.UGUI`（`TextField`/`TextInput`/`Button`/`ComboBox`/`Slider`/`ScrollPane`/`MovieClip`/`Transition`/`Gear*` …）。
- `Editor/`：通用转换器与工程 postprocess，命名空间 `OpenFairy.UGUI.Editor`。
- `Example/`：Basics demo 的运行时胶水。

## 使用（Unity 菜单 `Tools/OpenFairy`）

1. **Migrate** — 把 FairyGUI 工程（`UIProject/`）转成 `Assets/UIProject/` 下的 prefab、资源与生成代码。
2. **Generate Golden References** — 生成 FairyGUI 参照图与交互几何参照，用于像素/交互 parity 测试。
3. **Run PlayMode Tests** — 跑全部 PlayMode parity 测试，结果写入 `Temp/OpenFairyPlayModeResults.txt`。

改转换输出结构/序列化字段/生成代码 → 先 `Migrate`；改参照相关逻辑 → 先 `Generate Golden References`；改运行时渲染/动效/交互 → 至少 `Run PlayMode Tests`。

## 已实现

Basics / Transition 两套官方 demo 的静态渲染、主要交互（按钮/复选/单选、Slider、ComboBox 下拉、滚动、拖放、窗口/弹窗、翻页动效、文本 UBB 链接、列表填充）与 Transition 动效（路径、嵌套、滤镜、shake、循环/yoyo、倒放）均已覆盖并过 PlayMode parity 测试。

## 范围与边界

以下按设计**不实现**（用 Unity 原生替代或不需要）：resource URL / 动态异步资源加载、FairyGUI runtime API 签名一致性、GList 虚拟化/分页吸附/方向键导航、GTree、tooltips 浮层、blur 滤镜、跨组件 Controller page-action。详见仓库根 `AGENTS.md`「发布范围与取舍」，实现状态见 `Docs/BasicsImplementationStatus.md`。
