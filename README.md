# PcbPoseAlignInspect

WinForms + HALCON 的 PCB 治具入穴姿态检测配置项目。

## 目标

- 检测 `X` 偏移、`Y` 偏移、`角度` 偏差。
- 以模板匹配找到的稳定特征点作为检测基准。
- 先用 HSV + RGB 绿色优势提取 PCB 板体中心，再记录特征点到板中心的偏移关系。
- 板体 ROI 建议只覆盖绿色 PCB，避免左侧黄色反光金属进入分割范围。
- 框选 ROI 后会叠加显示绿色分割边缘，便于确认阈值抓边效果。
- 画布支持滚轮缩放、右键拖动平移、ROI 拖动和拉伸。
- 页面用于示教与建档，SDK 用于上位机调用。

## SDK 入口

- `PcbPoseInspectSdk.OpenSetupDialog(Bitmap image, PcbPoseInspectRecipe currentRecipe)`
- `PcbPoseInspectSdk.RunInspection(Bitmap image, PcbPoseInspectRecipe recipe, double tolerancePx)`

## 构建

```powershell
dotnet build
```
