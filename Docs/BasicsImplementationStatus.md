# Basics 页面实现状态

本文件记录 `Assets/FairyGUI/Examples/Basics` 对应到 NanamiUI 示例的覆盖情况。截图对比脚本会把 FairyGUI、NanamiUI、diff 三张图输出到 `Docs/RenderDiff`。

运行方式：Unity 菜单 `Tools/NanamiUI/Capture Basics Render Diff`。

截图脚本会在 Play Mode 中启用场景里的 `Stage Camera` 和 `UIPanel`，再为每个页面单独创建 FairyGUI `GRoot` 内容，避免 FairyGUI 侧空白或页面串图。

差异像素占比为 2026-07-07 全量截图的实测值（diff 中任一通道 >1/255 即计入，包含反锯齿噪声；纯反锯齿差异的页面约 1%~3%）。

| 页面 | 差异占比 | 说明 |
| --- | --- | --- |
| Main | 2.85% | 完成。差异全部为动态字体反锯齿噪声。 |
| Button | 2.59% | 完成。含 grayed 控制器约定、OnOffButton gearXY 默认值。 |
| Image | 16.07% | 基本完成。主要残差：FairyGUI 图集 duplicate padding 使平铺单元有 ~1px 重叠（已知差异，见下），另有九宫格渐变 banding。 |
| Graph | 0.16% | 完成。矩形/圆角/椭圆/多边形/正多边形/skew 全部像素级一致。 |
| MovieClip | 0.51% | 完成。jta 多帧解析 + 播放。 |
| Depth | 2.36% | 完成（静态）。 |
| Loader | 1.18% | 完成。fill/align/movieclip。 |
| List | 3.61% | 完成。四种布局与 FairyGUI 一致；截图环境无滚动条（UIConfig 未配置）。 |
| ProgressBar | 0.58% | 完成。reverse、径向填充、ani 关联跟随。 |
| Slider | 0.12% | 完成。 |
| ComboBox | 0.16% | 完成（静态，下拉交互未实现）。 |
| Clip&Scroll | 6.19% | 基本完成。overflow/margin/自定义遮罩一致；残差为底部小字号文本个别断行差 1 字。 |
| Controller | 3.31% | 完成。gearAni/gearFontSize/gearDisplay2。 |
| Relation | 1.78% | 完成。Relation 增量跟随（构建时烘焙 + 运行时轮询）。 |
| Label | 1.58% | 完成。 |
| Popup | 0.19% | 完成（静态，弹出交互未实现）。 |
| Window | 0.40% | 完成（静态，弹窗交互未实现）。 |
| Drag&Drop | 1.29% | 完成（静态，拖拽交互未实现）。 |
| Component | 2.56% | 完成。 |
| Grid | 1.66% | 完成。GridItem 星级进度、勾选框、movieclip。 |
| Text | 5.54% | 基本完成。UBB/渐变/描边/阴影/位图字体（两种 fnt 格式）/行内图片/链接样式/输入 prompt；残差为 UBB 混合字号段落断行差 1 字与行内图片行基线 2~3px。 |

## 已知差异

- **平铺（tile）图片**：FairyGUI 发布图集带 duplicate padding，平铺单元间有 ~1px 内容重叠；NanamiUI 用原图平铺无重叠。视觉差异细微，暂不复刻。
- **动态字体反锯齿**：两侧各自创建动态字体图集，边缘像素有 1/255 级差异，diff 中呈空心轮廓，无视觉差异。
- **截图环境无滚动条**：截图脚本不跑 BasicsMain.Awake，FairyGUI 的 UIConfig 滚动条未配置，因此 Migrate 侧滚动条常量（`VerticalScrollBar`/`HorizontalScrollBar`）也置 null。给真实项目使用时把这两个常量配上即可生成滚动条（机制已实现）。

## 截图输出约定

每个页面会生成：

- `{Page}_fairygui.png`
- `{Page}_nanami.png`
- `{Page}_diff.png`

其中 `Clip&Scroll` 会写成 `Clip_Scroll`，`Drag&Drop` 会写成 `Drag_Drop`。
