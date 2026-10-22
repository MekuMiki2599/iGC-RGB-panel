#Requires -Version 5.1
<#
.SYNOPSIS
    把 iGCRGB 需要的七彩虹 iGC 闭源组件复制到 vendor/ 目录。

.DESCRIPTION
    本仓库不包含七彩虹的闭源二进制文件。
    请先安装官方 iGC.Lite（七彩虹 iGame Center 精简版），
    再用本脚本从安装目录复制运行/编译所需的 DLL。

    需要的文件：
      <iGC>\iGameAPI.Contracts.dll
      <iGC>\iGameAPI\iGameMBoard.dll
      <iGC>\iGameAPI\iGameEasyCalc.dll
      <iGC>\iGameAPI\iGameSpaceCalc.dll
      <iGC>\iGameAPI\msvcp140.dll
      <iGC>\iGameAPI\vcruntime140.dll
      <iGC>\iGameAPI\vcruntime140_1.dll

.PARAMETER IgcPath
    iGC.Lite 的安装目录。不填时会尝试常见路径。

.PARAMETER OutDir
    输出目录，默认是仓库根目录下的 vendor/。

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File scripts\prepare-vendor-libs.ps1 -IgcPath "E:\iGC.Lite"

.EXAMPLE
    pwsh scripts/prepare-vendor-libs.ps1
#>
param(
    [string]$IgcPath = "",
    [string]$OutDir = ""
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($OutDir)) {
    $OutDir = Join-Path $repoRoot "vendor"
}

$requiredFiles = @(
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

function Find-IgcInstall {
    $candidates = New-Object System.Collections.Generic.List[string]
    $candidates.Add("E:\iGC.Lite")
    $candidates.Add("D:\iGC.Lite")
    $candidates.Add("C:\iGC.Lite")
    if ($env:ProgramFiles) {
        $candidates.Add((Join-Path $env:ProgramFiles "iGC.Lite"))
    }
    if (${env:ProgramFiles(x86)}) {
        $candidates.Add((Join-Path ${env:ProgramFiles(x86)} "iGC.Lite"))
    }
    foreach ($path in $candidates) {
        if (Test-IgcInstall $path) { return $path }
    }
    return $null
}

Write-Host ""
Write-Host "iGCRGB - 准备厂商组件" -ForegroundColor Cyan
Write-Host "---------------------------------------------"

$detected = $null
if (-not [string]::IsNullOrWhiteSpace($IgcPath)) {
    if (-not (Test-IgcInstall $IgcPath)) {
        Write-Host "指定的 iGC 路径无效：$IgcPath" -ForegroundColor Red
        Write-Host "该目录下应当包含 iGameAPI.Contracts.dll 和 iGameAPI\iGameMBoard.dll。" -ForegroundColor Yellow
        exit 1
    }
    $detected = (Resolve-Path -LiteralPath $IgcPath).Path
}
else {
    $detected = Find-IgcInstall
    if (-not $detected) {
        Write-Host "没有找到 iGC.Lite 安装目录。" -ForegroundColor Yellow
        Write-Host "请先安装官方 iGC.Lite，然后执行：" -ForegroundColor Yellow
        Write-Host "    powershell -ExecutionPolicy Bypass -File scripts\prepare-vendor-libs.ps1 -IgcPath \"你的 iGC 安装目录\"" -ForegroundColor White
        Write-Host ""
        Write-Host "也可以手动把上面列出的 7 个 DLL 复制到 vendor\ 目录（保持 iGameAPI\ 子目录结构）。" -ForegroundColor Gray
        exit 1
    }
}

Write-Host ("iGC 安装目录：" + $detected)
Write-Host ("输出目录：" + $OutDir)

$missing = @()
foreach ($relative in $requiredFiles) {
    $source = Join-Path $detected $relative
    if (-not (Test-Path -LiteralPath $source)) {
        $missing += $relative
    }
}

if ($missing.Count -gt 0) {
    Write-Host ""
    Write-Host "以下文件在 iGC 目录中不存在：" -ForegroundColor Red
    foreach ($item in $missing) { Write-Host ("  - " + $item) -ForegroundColor Red }
    Write-Host "请确认 iGC.Lite 已完整安装（不是只有快捷方式）。" -ForegroundColor Yellow
    exit 1
}

foreach ($relative in $requiredFiles) {
    $source = Join-Path $detected $relative
    $target = Join-Path $OutDir $relative
    $targetDir = Split-Path -Parent $target
    if (-not (Test-Path -LiteralPath $targetDir)) {
        New-Item -ItemType Directory -Force -Path $targetDir | Out-Null
    }
    Copy-Item -LiteralPath $source -Destination $target -Force
    Write-Host ("  已复制 " + $relative) -ForegroundColor Green
}

Write-Host ""
Write-Host "完成。现在可以运行 scripts\build.ps1 构建项目。" -ForegroundColor Cyan
Write-Host "注意：vendor/ 中的文件属于七彩虹，请勿提交到公开仓库（.gitignore 已忽略）。" -ForegroundColor Gray
