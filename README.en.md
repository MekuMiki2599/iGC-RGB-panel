<p align="center">
  <a href="README.md"><img src="https://img.shields.io/badge/%E7%AE%80%E4%BD%93%E4%B8%AD%E6%96%87-6E7781?style=flat-square" alt="简体中文"></a>
  <a href="README.en.md"><img src="https://img.shields.io/badge/English-4D6BFE?style=flat-square" alt="English"></a>
</p>

# iGCRGB

> A standalone lighting control panel for Colorful (七彩虹) iGame motherboards — apply your ARGB presets without launching iGame Center.

[![Developed entirely by DeepSeek AI](https://img.shields.io/badge/developed%20entirely%20by-DeepSeek%20AI-4D6BFE?style=flat-square)](https://www.deepseek.com/)

> 🤖 **This project was developed entirely by DeepSeek AI.**
> All C# source code, the WPF UI, PowerShell build scripts and documentation
> were written by DeepSeek. Human involvement was limited to requirements,
> on-hardware testing and publishing.

![Main window](docs/screenshot-main.png)

## Download

Grab the latest `iGCRGB_v2.0.0_win-x64.zip` from [**Releases**](https://github.com/MekuMiki2599/iGC-RGB-panel/releases).

> 📦 The release package contains **the application only** (iGCRGB.exe, docs and the vendor-setup helper).
> Colorful's lighting components are proprietary and are not redistributed. After extracting,
> run **`setup-vendor.bat`**, select your local iGC.Lite installation, and the script copies the
> required DLLs automatically. Alternatively follow `组件说明.txt` and copy them manually.
> Then double-click `iGCRGB.exe`.

## Why this project

Controlling motherboard lighting with iGC.Lite means launching the whole suite at every boot: fan control, LCD, hardware monitoring, ads and other modules all get loaded and stay resident in the background.

**iGCRGB** extracts just the lighting pipeline:

- Double-click one exe to open the settings panel — no need to launch iGC;
- The panel covers every effect and parameter from iGC's lighting page (18 effects, colour, brightness, speed, direction, sensitivity, temperature display, master switch);
- Only the three vendor lighting components are loaded; background CPU usage is around **0.2%**, and the working set is trimmed to a few MB when minimised to the tray;
- Supports launching silently at logon in tray mode and applying lighting automatically.

## Features

- 🎛 **Complete effect panel**: Off / Static / Breath / ColorCycle / Rainbow / Tune / Flicker / Lightning / StarryNight / ImmersiveGameplay / Music / Music A / Music C / Music D / Razer Connect / Status Display / New Start / Pulse
- 🎨 **Visual colour editing**: preset swatches, R/G/B sliders, hex input; every effect remembers its own colour and parameters
- ⚡ **Apply on change**: parameters are pushed about 0.4 s after you stop dragging (can be disabled for manual apply)
- 🔁 **Import from iGC**: first launch reads the whole preset saved by iGC; after that the tool works standalone
- 🖥 **Tray resident**: closing the window minimises to the tray while lighting keeps running; launching the exe again brings the window back
- 🚀 **Autostart**: one toggle in the panel; starts silently in `--tray` mode with no administrator rights required
- 🌙 **Wake handling**: lighting is re-initialised automatically after sleep/hibernate
- 📦 **No extra dependencies**: only .NET Framework 4.8 that ships with Windows — no NuGet packages, no drivers, no services

## Important notice

- This is an **unofficial** third-party tool. It is not affiliated with or endorsed by Colorful (七彩虹).
- Neither the repository nor the release package contains any Colorful source code or binaries. The closed-source components required to run it (`iGameAPI.Contracts.dll`, `iGameAPI\*.dll`, …) must be copied from **your own iGC.Lite installation**; the release package includes `setup-vendor.bat` to do this in one step.
- The tool drives lighting through the vendor's own public managed wrappers / exported APIs. It does not re-implement or decompile the vendor's lighting protocol.
- Changing motherboard RGB is generally safe, but you accept all risk. The authors are not liable for any damage or loss.
- If you redistribute a build of this project, remove the Colorful components first and let users supply their own.

## Requirements

| Item | Requirement |
| --- | --- |
| OS | Windows 10 / 11 64-bit |
| Runtime | .NET Framework 4.8 (included in Windows 10 1903+ / 11) |
| Hardware | Colorful iGame / Battle-Ax motherboards with ARGB control |
| Build | .NET SDK 8 (or 6/7) |
| Vendor components | Official iGC.Lite (provides the closed-source lighting DLLs) |

## Build from source

### 1. Install iGC.Lite

Install iGame Center from Colorful. The default install directory is usually `E:\iGC.Lite`.

Make sure the following files exist:

```
iGC.Lite\
├─ iGameAPI.Contracts.dll
└─ iGameAPI\
   ├─ iGameMBoard.dll
   ├─ iGameEasyCalc.dll
   ├─ iGameSpaceCalc.dll
   ├─ msvcp140.dll
   ├─ vcruntime140.dll
   └─ vcruntime140_1.dll
```

### 2. Copy the vendor components into vendor/

```powershell
powershell -ExecutionPolicy Bypass -File scripts\prepare-vendor-libs.ps1 -IgcPath "E:\iGC.Lite"
```

If `-IgcPath` is omitted, the script looks in `E:\iGC.Lite`, `D:\iGC.Lite`, `C:\iGC.Lite` and `Program Files`.

### 3. Build

```powershell
powershell -ExecutionPolicy Bypass -File scripts\build.ps1
```

The script compiles the project and assembles a runnable folder:

```
dist\iGCRGB\
├─ iGCRGB.exe
├─ iGCRGB.exe.config
├─ iGameAPI.Contracts.dll
├─ iGameAPI\... (vendor components)
└─ 使用说明.txt   (Chinese user manual)
```

Double-click `dist\iGCRGB\iGCRGB.exe` to run it.

Extra options:

```powershell
# Debug build
powershell -ExecutionPolicy Bypass -File scripts\build.ps1 -Configuration Debug

# Also create dist\iGCRGB_v2.0.0.zip
powershell -ExecutionPolicy Bypass -File scripts\build.ps1 -Zip
```

You can also use MSBuild / dotnet directly:

```powershell
dotnet build src\iGCRGB.Gui\iGCRGB.Gui.csproj -c Release -p:VendorDir="D:\some\vendor\"
```

## Usage

The full user manual is currently available in Chinese only: [docs/USAGE_zh.txt](docs/USAGE_zh.txt). In short:

1. Double-click `iGCRGB.exe` to open the panel;
2. Pick an effect on the left, adjust colour and parameters on the right; with "Apply on change" enabled the change is pushed automatically;
3. The switch in the top-right corner is the master lighting switch (same as iGC's "off");
4. Closing the window minimises to the tray; right-click the tray icon to quit;
5. The "Application & startup" section contains the autostart toggle, close behaviour and the "Import from iGC" button.

> ⚠️ Do not run iGC and iGCRGB at the same time — both claim the motherboard RGB controller and will fight over it.

## How it works

```
iGCRGB (WPF)
   │  calls
   ▼
iGameAPI.Contracts.dll (Colorful managed wrapper, P/Invoke)
   │
   ├── iGameMBoard.dll      motherboard RGB controller (HID)
   ├── iGameEasyCalc.dll    per-frame effect engine
   └── iGameSpaceCalc.dll   spatial effect engine (initialised only)
```

Initialisation follows the exact sequence used by iGC:

1. Stop any leftover effect engines;
2. Initialise `iGameSpaceCalc` / `iGameEasyCalc`;
3. Enumerate the motherboard lighting device and read its channels;
4. Switch the device into the EasyCalc unified-drive mode;
5. Build an `iGameEasyCalc_LEDParameter` (effect type, colour, brightness, speed, direction, FPS) and push it;
6. Keep a resident process so the engine keeps rendering; Status Display additionally receives temperature data.

More details (in Chinese): [docs/architecture.md](docs/architecture.md).

## Configuration and logs

- `settings.json` — next to the exe (falls back to `%LOCALAPPDATA%\iGCRGB\` if the folder is read-only). Stores all effect parameters and UI options.
- `iGCRGB.log` — runtime log. Check this first when troubleshooting.

## Known limitations

- Controls motherboard lighting only (the MotherBoard device reported by iGC). GPU / DRAM / keyboard / controller lighting is out of scope.
- iGC's "Space Light Control" canvas editor is not ported; the panel supports the regular whole-device effects.
- Immersive Gameplay, Razer Connect and the music effects rely on extra modules or live data inside iGC. This tool only pushes the basic parameters, so those effects may be incomplete.
- Status Display needs temperature data; the tool cannot read hardware sensors, so it uses the temperature value you set in the panel.
- Cannot run at the same time as iGC.

## Project structure

```
iGCRGB/
├─ src/iGCRGB.Gui/            WPF application
│  ├─ Core/                   lighting engine, config model, JSON, autostart, logging
│  ├─ Themes/Theme.xaml       dark theme and control styles
│  ├─ Converters/             binding converters
│  ├─ App.xaml(.cs)           startup, single instance, tray
│  └─ MainWindow.xaml(.cs)    settings panel
├─ scripts/
│  ├─ prepare-vendor-libs.ps1 copy vendor components into vendor/
│  └─ build.ps1               build + assemble dist/
├─ docs/                      screenshots, manual, architecture notes
├─ vendor/                    vendor components go here (never committed)
└─ LICENSE                    MIT
```

## Contributing

Issues and pull requests are welcome. Before submitting:

- Never upload any Colorful binaries to the repository, issues or pull requests;
- Keep compatibility with C# 7.3 / .NET Framework 4.8 (the project intentionally avoids third-party dependencies);
- Hardware-related changes should describe the test environment and motherboard model.

## Development note

This project is an experiment in "AI-driven development":

- Requirements analysis, hardware pipeline investigation, all C# / WPF code, UI design, build scripts and documentation were **written by DeepSeek**;
- Humans only provided requirements, tested on real hardware, and handle publishing/maintenance;
- Every commit maps back to a concrete conversation request.

If you reference or introduce this project, please credit: **"Developed entirely by DeepSeek AI"**.

## License

The source code is released under the [MIT License](LICENSE).
See [NOTICE](NOTICE) for development and copyright statements.
Colorful iGC / iGame Center and its components remain the property of their respective owners and are not covered by this license.
