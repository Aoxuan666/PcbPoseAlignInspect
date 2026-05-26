# 上位机 SDK 接入说明

## 1. 引用文件

- `PcbPoseAlignInspect.exe` 或 `PcbPoseAlignInspect.dll`
- `halcondotnet.dll`

目标框架：`.NET Framework 4.7.2`，平台建议 `x64`。

## 2. SDK 类

```csharp
PcbPoseAlignInspect.Sdk.PcbPoseInspectSdk
```

### 2.1 打开配置页面

```csharp
PcbPoseInspectRecipe OpenSetupDialog(Bitmap image, PcbPoseInspectRecipe currentRecipe)
```

返回用户确认后的配方；若用户取消，返回传入的 `currentRecipe`。

### 2.2 直接运行检测

```csharp
PcbPoseInspectResult RunInspection(Bitmap image, PcbPoseInspectRecipe recipe, double tolerancePx)
```

检测输出核心字段：

- `DxPx`
- `DyPx`
- `AngleDeltaDeg`
- `ScorePx`
- `Success`
- `NgReasons`

## 3. 配置页面操作

页面需要先绘制 `板体ROI`，建议只覆盖绿色 PCB，避开左侧黄色反光金属。算法会在该 ROI 内完成 HSV + RGB 绿色优势分割，提取 PCB 板体中心。

如果启用特征模板定位，还需要绘制：

- `特征搜索ROI`：运行时寻找特征点的范围。
- `特征模板ROI`：示教时保存的局部特征模板，可选择矩形或圆形。

保存特征模板时，程序会记录“特征中心 -> 板体中心”的偏移关系。检测时先通过 Halcon NCC 模板匹配找到特征点，再用该偏移换算运行时板体中心，与示教位置比较。

画布操作：

- 鼠标滚轮：缩放图像
- 鼠标右键拖动：平移图像
- 左键拖动 ROI 内部：移动 ROI
- 左键拖动 ROI 边/角点：调整 ROI

框选 ROI 后，页面会显示当前绿色阈值分割出来的 PCB 边缘，便于确认外轮廓提取是否稳定。

## 4. 判定方式

单参数公差判定：

```text
score = max(|dx|, |dy|, angleEquivalentPx)
angleEquivalentPx = |dAngle(rad)| * AngleRadiusPx
```

当 `score <= tolerancePx` 判定 `OK`，否则 `NG`。
