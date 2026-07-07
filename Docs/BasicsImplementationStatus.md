# Basics / Transition 页面实现状态

本文件记录 `Assets/FairyGUI/Examples` 的 Basics 与 Transition 示例对应到 NanamiUI 的覆盖情况。截图对比脚本会把 FairyGUI、NanamiUI、diff 三张图输出到 `Docs/RenderDiff`。

运行方式：Unity 菜单 `Tools/NanamiUI/Capture Basics Render Diff`。

截图流程（播放对齐截屏）：每页 FairyGUI 与 NanamiUI **同帧创建、同时播放**（含 t0 动效、movieclip、Relation 等运行时行为），经过 1 秒后**同帧截屏**——两侧用同一 Time.deltaTime 序列推进，动画相位一致，对比的是真实播放中的最终画面。动效起播第一帧按 GTweener smoothStart 语义钳制 dt。

差异像素占比为 2026-07-07 播放式截图的实测值（diff 中任一通道 >1/255 即计入，含反锯齿噪声；纯反锯齿差异的页面约 1%~3%）。

| 页面 | 差异占比 | 说明 |
| --- | --- | --- |
| Main | 2.85% | 完成。差异全部为动态字体反锯齿噪声。 |
| Button | 2.58% | 完成。含 grayed 控制器约定、OnOffButton gearXY 默认值。 |
| Image | 15.98% | 基本完成。残差：FairyGUI 图集 duplicate padding 使平铺单元 ~1px 重叠（已知差异），九宫格渐变 banding。 |
| Graph | 0.15% | 完成。 |
| MovieClip | 0.93% | 完成。播放相位与 FairyGUI 对齐。 |
| Depth | 2.36% | 完成（静态）。 |
| Loader | 1.09% | 完成。 |
| List | 3.52% | 完成。滚动条（UIConfig 默认 ScrollBar_VT/HZ）已生成，grip 按显示比例。 |
| ProgressBar | 0.49% | 完成。 |
| Slider | 0.07% | 完成。 |
| ComboBox | 0.16% | 完成（静态）。 |
| Clip&Scroll | 7.83% | 基本完成。残差：滚动条细节（箭头/滑块态）与底部小字号断行个别字。 |
| Controller | 3.49% | 完成。 |
| Relation | 1.78% | 完成。 |
| Label | 1.63% | 完成。 |
| Popup | 0.19% | 完成（静态）。 |
| Window | 0.40% | 完成（静态）。 |
| Drag&Drop | 1.26% | 完成（静态）。 |
| Component | 2.55% | 完成。 |
| Grid | 2.51% | 完成。 |
| Text | 5.54% | 基本完成。残差：UBB 混合字号段落断行差 1 字、行内图片行基线 2~3px。 |
| Transition/BOSS | 0.00% | 完成。路径动画 + 嵌套动效（ColorFilter/Scale 循环）逐帧一致。 |
| Transition/BOSS_SKILL | 0.38% | 完成。 |
| Transition/TRAP | 0.00% | 完成。Back.Out 旋转链逐帧一致。 |
| Transition/GoodHit | 14.19% | 视觉一致（Bounce 缩放 + Shake + 逐字位图字体）；小画布上血迹图反锯齿与 Shake 随机性撑高百分比。 |
| Transition/PowerUp | 30.61% | 视觉一致（Expo 滑入、数字滚动、火焰特效、淡出位移）；火焰 movieclip 帧相位 ±1 帧 + 加色发光在小画布上撑高百分比。 |
| Transition/PathDemo | 0.20% | 完成。贝塞尔路径 + 无限循环动效逐帧一致。 |

## 动效（Transition）实现

纯数据 + 轻量运行时：`Migrate` 把 `<transition>` 解析为 `NanamiUI.Transition` 组件（item 数组直接序列化，目标为对象引用），运行时逐帧求值。缓动用 DOTween 的 `EaseManager.Evaluate`（与 FairyGUI 内置的 Penner 方程同源，数值一致）；路径为 GPath 的移植（`TransitionPath`）。已支持 item 类型：XY（含路径）/Size/Scale/Pivot/Alpha/Rotation/Color/Animation/Visible/Sound/嵌套 Transition/Shake/ColorFilter/Text，repeat(-1 无限)/yoyo、autoPlay、播放次数与延迟、`Play/Stop/Step`。ColorFilter 通过 `ColorAdjust` + `NanamiUI/UI ColorMatrix` shader（亮度/对比度/饱和度/色相矩阵）。

## 已知差异

- **平铺（tile）图片**：FairyGUI 发布图集带 duplicate padding，平铺单元间有 ~1px 内容重叠；NanamiUI 用原图平铺无重叠。
- **动态字体反锯齿**：两侧各自创建动态字体图集，边缘像素有 1~2/255 级差异，无视觉差异（同区域平均亮度实测相等）。
- **截图底色**：截图相机使用不透明底色 #363C44（FairyGUI 文字 shader 直通 alpha 与 uGUI 预乘 alpha 在透明底上的 alpha 通道不同，实底下两侧像素一致）。
- **Shake 与 movieclip 帧相位**：Shake 为随机偏移，两引擎随机序列不同；movieclip 激活时机可能差 ±1 帧。仅影响 GoodHit/PowerUp 的百分比，不影响观感。

## 截图输出约定

每个页面会生成 `{Page}_fairygui.png`、`{Page}_nanami.png`、`{Page}_diff.png`；`&`、`/` 会替换为 `_`。
