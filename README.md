# GamePad Emulator — 手柄模拟器

一个 Windows 桌面应用，可在屏幕上模拟 **PlayStation (DualShock 4)** 或 **Xbox 360** 手柄。
点击/拖拽画面上的手柄，会向系统注入真实手柄输入，**可在游戏里直接操作**。

---

## 功能特性

- **两种模式自由切换**：顶部一键切换 `Xbox 手柄` / `PS 手柄`
- **高保真矢量 UI**：纯 XAML 矢量绘制，无任何图片资源
  - Xbox 360：非对称摇杆布局、发光 Xbox 大圆键 (Guide)、彩色 A/B/X/Y、十字键
  - DualShock 4：对称摇杆、触控板、灯条、彩色 △○✕□、PS 键、Share/Options
- **真实输入注入**：基于 [ViGEmBus](https://github.com/ViGEm/ViGEmBus) 内核驱动创建虚拟 HID 设备，
  游戏把它当成真手柄读取（XInput / DInput 均可）
- **全交互控件**：
  - 摇杆：按住中心圆帽拖动，松开自动回中（带死区、圆形钳制）
  - 扳机 (LT/RT / L2/R2)：按住蓄力 0→1，松开归零；滚轮可微调
  - 十字键 / 功能键 / 面键：按住即触发，带按下视觉反馈
- **游戏模式**：开启后窗口缩成小浮窗置顶于游戏之上，且点击时**不会夺取前台焦点** ——
  游戏始终保持活动窗口（看得见、不暂停），你在浮窗上操作手柄，信号照常注入游戏。
  这就是实体手柄的体验：眼睛看游戏，手在别处操作
- **驱动状态面板**：实时检测 ViGEmBus 是否安装、虚拟手柄是否已连接，未装驱动时给出下载链接

---

## 运行前提

| 项目 | 要求 |
|------|------|
| 操作系统 | Windows 10 / 11 (x64) |
| .NET 运行时 | .NET 10 (构建时需要 SDK) |
| **ViGEmBus 驱动** | **必须安装**（否则只能看 UI，无法在游戏中生效） |

> **关于 ViGEmBus**：这是 Nefarius 开发的开源内核驱动，作用是在系统里创建虚拟 Xbox/PS 控制器设备。
> 所有同类软件（DS4Windows 等）都依赖它，属于系统级依赖，就像显卡驱动一样。
> 下载：https://github.com/ViGEm/ViGEmBus/releases/latest —— 安装 `ViGEmBus_*.exe` 后重启电脑。

应用内右下角也提供了「打开 ViGEmBus 下载页」按钮。装好驱动前，UI 仍可正常预览交互。

---

## 构建与运行

```bash
# 在 GamePadEmulator 目录下
dotnet build -c Release
# 运行（GUI 程序）
src/GamePadEmulator/bin/Release/net10.0-windows/GamePadEmulator.exe
```

开发调试：
```bash
dotnet run --project src/GamePadEmulator
```

### 单元测试（含端到端验证）
```bash
dotnet test
```
测试覆盖三个层面：
1. **轴值量化**（15 用例）：摇杆/扳机的归一化坐标 → HID 整数量化（中心、极值、单调性、钳制）
2. **端到端输入注入**：用真实 `XboxController` 后端创建虚拟手柄 → 注入 A 键/摇杆/扳机 →
   用游戏读取手柄所用的 **XInput API (`XInputGetState`)** 读回，断言数值完全一致；
   松开后断言键值清除。这条测试证明了完整链路：UI 状态 → ViGEm SubmitReport → ViGEmBus 驱动 → 操作系统 → XInput。
3. **设备可见性**：`ControllerService.Connect` 后，`Get-PnpDevice` 出现 `VID_045E&PID_028E`
   （Xbox 360 手柄），`XInputGetCapabilities` 返回已连接的 gamepad (SubType=1)。

> 这些测试需要 ViGEmBus 驱动已安装；未装时自动跳过（不报错），装好后必须通过。
> 已在本机验证：**18/18 通过**，设备在 Windows「设置→蓝牙和其他设备」中以
> 「支持 Windows 的 XBOX 360 手柄」出现。

---

## 使用方法

1. 顶部选择要模拟的手柄类型（Xbox / PS）
2. 点击 **「连接虚拟手柄」**（需已安装 ViGEmBus 驱动）
3. 打开游戏，让游戏成为活动窗口
4. 点击 **「游戏模式」** —— 模拟器缩成小浮窗置顶在角落，**点击它不会抢走游戏焦点**
5. 在浮窗上操作手柄（摇杆拖拽、按键按住、扳机蓄力），输入实时进游戏

### 为什么需要「游戏模式」
虚拟手柄信号和窗口焦点**无关**（手柄是全局设备，任何读手柄的程序都能收到）。
真正的问题是：切到模拟器时游戏窗口被遮挡/失去焦点，你看不到游戏画面。
游戏模式让模拟器浮在游戏之上但不抢焦点，解决了这个遮挡问题。

> 提示：全屏游戏请先改成**窗口/无边框窗口模式**，否则全屏会盖住浮窗。

---

## 项目结构

```
GamePadEmulator/
├── GamePadEmulator.sln
├── src/GamePadEmulator/
│   ├── Core/                       # 与 UI 解耦的核心层
│   │   ├── ControllerState.cs      # 控制器无关的输入状态结构
│   │   ├── VirtualController.cs    # 后端接口 + 轴值量化数学
│   │   ├── Ds4Controller.cs        # DualShock 4 ViGEm 后端
│   │   ├── XboxController.cs       # Xbox 360 ViGEm 后端
│   │   └── ControllerService.cs    # 门面：驱动检测、模式切换、连接
│   ├── Controls/                   # 可复用交互控件
│   │   ├── AnalogStick.cs          # 可拖拽摇杆（死区、圆形钳制、回中）
│   │   └── TriggerBar.cs           # 模拟扳机（蓄力、滚轮微调）
│   ├── Views/                      # 高保真手柄视图
│   │   ├── Ds4View.xaml(.cs)       # DualShock 4 矢量 UI
│   │   ├── XboxView.xaml(.cs)      # Xbox 360 矢量 UI
│   │   └── MainWindow.xaml(.cs)    # 主壳：模式切换、状态、连接
│   ├── Themes/Generic.xaml         # 控件模板 + 按键样式 + 配色
│   └── Utilities/WindowCapture.cs  # 自检用截图（环境变量触发）
└── tests/GamePadEmulator.Tests/    # xUnit 单元测试
```

## 技术栈

- **C# / WPF** (.NET 10) —— 原生 Windows UI，矢量绘图
- **Nefarius.ViGEm.Client** —— 官方 ViGEm 绑定库
- **xUnit** —— 单元测试
