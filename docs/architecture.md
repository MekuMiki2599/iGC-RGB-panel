# 架构与实现说明

本文记录 iGCRGB 的关键实现细节，方便后续维护和二次开发。

## 1. 整体结构

```
┌─────────────────────────────────────────────────────────────┐
│  iGCRGB.exe (WPF, .NET Framework 4.8, x64)                  │
│                                                             │
│  App.xaml.cs        启动、单实例、托盘、电源事件、后台应用      │
│  MainWindow.xaml    设置面板（灯效列表 + 参数编辑 + 预览）     │
│                                                             │
│  Core/LightingEngine.cs   调用厂商组件，下发灯效               │
│  Core/LightingConfig.cs   配置模型（与 iGC 的灯光配置一一对应） │
│  Core/EffectCatalog.cs    灯效目录与参数可见性                 │
│  Core/StartupManager.cs   开机自启（注册表 → 快捷方式 → VBS）   │
│  Core/Json.cs             极简 JSON 读写（不引入第三方库）      │
│  Core/IconFactory.cs      运行期绘制窗口/托盘图标               │
│  Core/AppLog.cs           文件日志 + 自动轮转                  │
└──────────────────────────┬──────────────────────────────────┘
                           │ P/Invoke / 托管调用
┌──────────────────────────▼──────────────────────────────────┐
│  iGameAPI.Contracts.dll  （七彩虹托管封装）                    │
│    LEDFactory / iGameMBoard / iGameEasyCalcAPI / ...         │
└──────────────────────────┬──────────────────────────────────┘
                           │ 原生导出
┌──────────────────────────▼──────────────────────────────────┐
│  iGameMBoard.dll       主板 RGB（HID）                       │
│  iGameEasyCalc.dll     灯效逐帧计算引擎                       │
│  iGameSpaceCalc.dll    空间灯效引擎（只初始化，不使用）         │
└─────────────────────────────────────────────────────────────┘
```

## 2. 厂商组件与为什么必须由用户提供

iGC.Lite 的灯光功能由一组**闭源**的 C++ / 托管混合组件实现：

| 文件 | 作用 |
| --- | --- |
| `iGameAPI.Contracts.dll` | 托管封装层，声明 P/Invoke 与结构体（`iGameEasyCalc_LEDParameter`、`iGameLEDDevice_Buffer` 等） |
| `iGameAPI\iGameMBoard.dll` | 主板 RGB 控制器驱动层，通过 HID 与主板 MCU 通信 |
| `iGameAPI\iGameEasyCalc.dll` | 灯效引擎：按参数逐帧计算 100 颗灯珠颜色 |
| `iGameAPI\iGameSpaceCalc.dll` | 空间灯效引擎；本工具只调用其 Stop/Init 保持状态干净 |
| `iGameAPI\msvcp140.dll` 等 | VC++ 运行时依赖 |

这些文件版权属于七彩虹，不能随开源项目分发，因此：

- 仓库只包含本项目自己编写的 C# 代码；
- 构建前由 `scripts/prepare-vendor-libs.ps1` 从用户本机的 iGC 安装目录复制到 `vendor/`；
- `vendor/` 已在 `.gitignore` 中忽略。

## 3. 启动与初始化序列

`LightingEngine.Initialize()` 复刻了 iGC.Lite 服务启动时的顺序：

```csharp
iGameSpaceCalcAPI.iGameSpaceCalc_Stop();   // 清理可能残留的引擎
iGameEasyCalcAPI.iGameEasyCalc_Stop();
Thread.Sleep(100);

iGameSpaceCalcAPI.iGameSpaceCalc_Init();
iGameEasyCalcAPI.iGameEasyCalc_Init();
Thread.Sleep(100);

var board = LEDFactory.CreateLEDDevice(iGameLEDDeviceType.MBoard);
board.Init();                              // 枚举 HID 设备并读取通道信息
board.Set_EasyCalc_Effects();              // 让设备从灯效引擎拉取帧数据
```

之后每次应用灯效：

```csharp
var param = new iGameEasyCalc_LEDParameter(0);
param.LEDType     = effect;                // iGameEasyCalc_LEDType
param.Color       = new RGB { R, G, B };
param.Brightness  = bri / 255f;            // 0..1
param.Sensitivity = 10;                    // iGC 同步灯效固定 10（音乐类放开）
param.Speed       = speed;
param.Direction   = dir;
param.LEDCount    = 100;
param.FPS         = 30;

iGameEasyCalcAPI.iGameEasyCalc_Calc_Effects(param);
```

当灯效为 `StatusDisplay` 时，额外下发温度：

```csharp
var state = new iGameEasyCalc_StateRealityData(true);
state.temperature_Reality = temp;
state.temperature_Min     = low;
state.temperature_Max     = high;
iGameEasyCalcAPI.iGameEasyCalc_Set_StateReality(state);
```

> 注意：厂商的托管封装里 `iGameMBoard.SetFPS()` 误写成了调用 `Get_FPS`（其自身的 bug）。
> 本项目不下发设备侧 FPS，效果帧率由 `param.FPS` 控制，与 iGC 的实际行为一致。

## 4. 灯效枚举

`iGameEasyCalc_LEDType` 的取值顺序（与 iGC 配置文件中的 `CheckedEffect` 对应）：

| 值 | 名称 | 值 | 名称 |
| --- | --- | --- | --- |
| 0 | Sleep（关灯） | 9 | ImmersiveGameplay |
| 1 | Static | 10 | Music |
| 2 | Breath | 11 | MusicA |
| 3 | ColorCycle | 12 | RazerConnect |
| 4 | Rainbow | 13 | StatusDisplay |
| 5 | Tune（流光） | 14 | MusicC |
| 6 | Flicker | 15 | MusicD |
| 7 | Lightning | 16 | NewStart |
| 8 | StarryNight | 17 | Pulse |

## 5. 配置模型

本项目的 `settings.json` 与 iGC 的 `LiteRGBEffectConfig.json` 一一对应：

```jsonc
{
  "version": 2,
  "systemOn": true,            // 灯光总开关（iGC: RGBSystemSwitch）
  "effect": "Tune",            // 当前灯效（iGC: CheckedEffect）
  "fps": 30,
  "stateLow": 50,              // iGC: Effect_State_Low
  "stateHigh": 70,             // iGC: Effect_State_High
  "stateBase": "CPU",          // iGC: Effect_State_Base
  "stateTemp": 40,             // 本工具额外提供：状态显示用的固定温度
  "autoApply": true,           // 界面：调整后立即生效
  "autoStart": false,          // 界面：开机自启
  "closeToTray": true,         // 界面：关闭窗口最小化到托盘
  "igcConfigPath": "E:\\iGC.Lite\\CCData\\Configs\\LiteRGBEffectConfig.json",
  "effects": {
    "Tune": { "bri": 76, "speed": 2, "dir": 0, "sensitivity": 30,
              "r": 86, "g": 13, "b": 169 }
    // ... 每种灯效独立保存一份参数
  }
}
```

首次启动（`settings.json` 不存在）时，`LightingConfig.LoadOrCreate()` 会读取
iGC 的灯光配置，把所有灯效参数、总开关、温度区间导入进来，并立即保存。

## 6. 进程生命周期

- **单实例**：`Local\iGCRGB_Gui_Singleton` 互斥体。再次启动时通过
  `Local\iGCRGB_Gui_Show` 事件通知已运行实例显示窗口，然后自身退出。
- **托盘**：`System.Windows.Forms.NotifyIcon`，菜单提供「打开设置 / 重新应用 / 立即关灯 / 退出」。
- **关闭行为**：默认隐藏到托盘；配置为「直接退出」时调用 `ExitApp()`。
- **开机自启**：启动参数 `--tray` 表示不显示窗口，直接应用灯光并常驻。
- **睡眠唤醒**：监听 `SystemEvents.PowerModeChanged`，恢复后延迟 3 秒重新初始化。
- **内存回收**：隐藏到托盘后调用 `SetProcessWorkingSetSize(-1, -1)` 回收工作集。

## 7. 开机自启的实现

按可靠性依次尝试三种方式（都需要当前用户权限，不需要管理员）：

1. `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` 写入 `"<exe>" --tray`；
2. 「启动」文件夹创建 `.lnk` 快捷方式（通过 `WScript.Shell` COM）；
3. 「启动」文件夹写入 VBS 脚本，以隐藏窗口的方式启动。

原因：部分安全软件会拦截注册表启动项或启动文件夹写入，多级回退可以让功能尽量可用。
卸载时会同时清理以上三种位置。

## 8. 兼容性与限制

- 目标框架为 .NET Framework 4.8，编译产物为 x64（厂商原生 DLL 是 x64）。
- 厂商 DLL 通过相对路径 `.\iGameAPI\` 加载，因此程序启动时会
  `Directory.SetCurrentDirectory(程序目录)`，保证从快捷方式/计划任务启动也能找到组件。
- 颜色下发为 8-bit RGB；亮度参数为 0–255（内部除以 255 转成 0–1）。
- 「空间灯控」使用另一套 `iGameSpaceCalc` 数据模型（坐标、灯珠映射），未在本项目中实现。
- 与 iGC 同时运行会争抢 HID 设备，可能表现为灯效闪烁或初始化失败。
