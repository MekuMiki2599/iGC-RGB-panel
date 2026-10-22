# vendor —— 厂商组件放置目录（不随仓库分发）

本目录用于放置运行和编译 iGCRGB 所必需的**七彩虹闭源组件**。
这些文件版权属于七彩虹，**不会**提交到 Git 仓库（见根目录 `.gitignore`）。

## 需要的文件

从你的 iGC.Lite 安装目录复制以下文件（保持 `iGameAPI\` 子目录结构）：

```
vendor\
├─ iGameAPI.Contracts.dll
└─ iGameAPI\
   ├─ iGameMBoard.dll
   ├─ iGameEasyCalc.dll
   ├─ iGameSpaceCalc.dll
   ├─ msvcp140.dll
   ├─ vcruntime140.dll
   └─ vcruntime140_1.dll
```

## 自动复制

在仓库根目录运行：

```powershell
powershell -ExecutionPolicy Bypass -File scripts\prepare-vendor-libs.ps1 -IgcPath "E:\iGC.Lite"
```

## 手动复制

如果脚本无法完成（例如 iGC 安装在其他盘符），直接按上面的目录结构手动复制即可。

## 说明

- 这些 DLL 仅在本地编译、运行时使用，请勿提交到公开仓库或发布到 Release。
- 如果 iGC 升级后灯光功能异常，可以用新版 iGC 中的同名文件覆盖本目录。
- 本项目只用到上述组件，不需要 `iGameAPI.LED.dll`、`iGameGCard.dll`、`iGameAPI.NVAPI.dll` 等其他设备/显卡组件。
