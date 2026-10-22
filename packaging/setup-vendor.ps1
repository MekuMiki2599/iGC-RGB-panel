#Requires -Version 5.1
<#
.SYNOPSIS
    从本机已安装的 iGC.Lite 复制 iGCRGB 运行所需的七彩虹灯光组件。

.DESCRIPTION
    发布包（Release zip）出于版权原因不包含七彩虹的闭源组件。
    本脚本会：
      1. 自动在常见路径查找 iGC.Lite；
      2. 找不到时弹出文件夹选择框，让你手动选择安装目录；
      3. 把所需的 7 个 DLL 复制到当前程序目录。

    需要 iGC.Lite 已安装：https://www.colorful.cn （七彩虹官网，iGame Center）

.PARAMETER IgcPath
    可选：直接指定 iGC.Lite 安装目录；不填时会自动查找或弹出选择框。
#>
param(
    [string]$IgcPath = ""
)

$dest = Split-Path -Parent $MyInvocation.MyCommand.Path
$required = @(
    "iGameAPI.Contracts.dll",
    "iGameAPI\iGameMBoard.dll",
    "iGameAPI\iGameEasyCalc.dll",
    "iGameAPI\iGameSpaceCalc.dll",
    "iGameAPI\msvcp140.dll",
    "iGameAPI\vcruntime140.dll",
    "iGameAPI\vcruntime140_1.dll"
)

function Test-IgcInstall([string]$path) {
    if ([string]::IsNullOrWhiteSpace($path)) { return $false }
    if (-not (Test-Path -LiteralPath $path)) { return $false }
    if (-not (Test-Path -LiteralPath (Join-Path $path "iGameAPI.Contracts.dll"))) { return $false }
    if (-not (Test-Path -LiteralPath (Join-Path $path "iGameAPI\iGameMBoard.dll"))) { return $false }
    return $true
}

Write-Host ""
Write-Host "iGCRGB - 准备七彩虹灯光组件" -ForegroundColor Cyan
Write-Host "--------------------------------------------------"
Write-Host "目标目录：$dest"
Write-Host ""

# 1. 先在常见位置自动查找
$candidates = @()
$candidates += "E:\iGC.Lite"
$candidates += "D:\iGC.Lite"
$candidates += "C:\iGC.Lite"
if ($env:ProgramFiles) {
    $candidates += (Join-Path $env:ProgramFiles "iGC.Lite")
}
if (${env:ProgramFiles(x86)}) {
    $candidates += (Join-Path ${env:ProgramFiles(x86)} "iGC.Lite")
}

$igcRoot = $null
if (-not [string]::IsNullOrWhiteSpace($IgcPath)) {
    if (-not (Test-IgcInstall $IgcPath)) {
        Write-Host "指定的 iGC 目录无效：$IgcPath" -ForegroundColor Red
        exit 1
    }
    $igcRoot = (Resolve-Path -LiteralPath $IgcPath).Path
}
else {
    foreach ($candidate in $candidates) {
        if (Test-IgcInstall $candidate) { $igcRoot = $candidate; break }
    }
}

# 2. 没找到就让用户选
if (-not $igcRoot) {
    Write-Host "没有自动找到 iGC.Lite，请在弹出的窗口中选择安装目录……" -ForegroundColor Yellow
    Add-Type -AssemblyName System.Windows.Forms | Out-Null
    $dialog = New-Object System.Windows.Forms.FolderBrowserDialog
    $dialog.Description = "请选择 iGC.Lite 安装目录（目录中应包含 iGameAPI 文件夹）"
    $dialog.ShowNewFolderButton = $false
    if ($env:SystemDrive) { $dialog.SelectedPath = "$($env:SystemDrive)\" }
    $result = $dialog.ShowDialog()
    if ($result -ne [System.Windows.Forms.DialogResult]::OK) {
        Write-Host ""
        Write-Host "已取消，未复制任何文件。" -ForegroundColor Yellow
        exit 1
    }
    $igcRoot = $dialog.SelectedPath
}

if (-not (Test-IgcInstall $igcRoot)) {
    Write-Host ""
    Write-Host "选择的目录无效：$igcRoot" -ForegroundColor Red
    Write-Host "该目录中应包含 iGameAPI.Contracts.dll 和 iGameAPI\iGameMBoard.dll。" -ForegroundColor Yellow
    exit 1
}

Write-Host "找到 iGC.Lite：$igcRoot" -ForegroundColor Green
Write-Host ""

# 3. 检查缺失
$missing = @()
foreach ($relative in $required) {
    if (-not (Test-Path -LiteralPath (Join-Path $igcRoot $relative))) {
        $missing += $relative
    }
}
if ($missing.Count -gt 0) {
    Write-Host "以下文件在 iGC 目录中不存在：" -ForegroundColor Red
    foreach ($item in $missing) { Write-Host ("  - " + $item) -ForegroundColor Red }
    Write-Host "请确认 iGC.Lite 已完整安装（而不是只有快捷方式）。" -ForegroundColor Yellow
    exit 1
}

# 4. 复制
foreach ($relative in $required) {
    $source = Join-Path $igcRoot $relative
    $target = Join-Path $dest $relative
    $targetDir = Split-Path -Parent $target
    if (-not (Test-Path -LiteralPath $targetDir)) {
        New-Item -ItemType Directory -Force -Path $targetDir | Out-Null
    }
    Copy-Item -LiteralPath $source -Destination $target -Force
    Write-Host ("  已复制 " + $relative) -ForegroundColor Green
}

Write-Host ""
Write-Host "完成！现在可以双击 iGCRGB.exe 启动。" -ForegroundColor Cyan
Write-Host ""
exit 0
