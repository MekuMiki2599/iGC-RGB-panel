#Requires -Version 5.1
<#
.SYNOPSIS
    构建项目并生成发布用的 zip（不包含七彩虹闭源组件）。

.DESCRIPTION
    发布包只包含 iGCRGB 程序本体、文档与组件准备脚本。
    七彩虹的 iGameAPI 组件版权归厂商所有，不随发布包分发；
    用户解压后运行 setup-vendor.bat，从本机 iGC.Lite 复制即可。

.PARAMETER Version
    版本号，默认从 csproj 读取。

.PARAMETER SkipBuild
    跳过编译，直接使用已有的 dist\iGCRGB。

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File scripts\make-release.ps1
#>
param(
    [string]$Version = "",
    [switch]$SkipBuild
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repoRoot "src\iGCRGB.Gui\iGCRGB.Gui.csproj"

if ([string]::IsNullOrWhiteSpace($Version)) {
    $csprojText = Get-Content -LiteralPath $project -Raw -Encoding UTF8
    $match = [regex]::Match($csprojText, "<Version>([^<]+)</Version>")
    if ($match.Success) { $Version = $match.Groups[1].Value.Trim() } else { $Version = "0.0.0" }
}

if (-not $SkipBuild) {
    $buildScript = Join-Path $PSScriptRoot "build.ps1"
    & powershell -NoProfile -ExecutionPolicy Bypass -File $buildScript
    if ($LASTEXITCODE -ne 0) {
        Write-Host "构建失败，终止打包。" -ForegroundColor Red
        exit 1
    }
}

$distDir = Join-Path $repoRoot "dist\iGCRGB"
$exe = Join-Path $distDir "iGCRGB.exe"
if (-not (Test-Path -LiteralPath $exe)) {
    Write-Host "未找到编译产物：$exe" -ForegroundColor Red
    Write-Host "请先运行 scripts\build.ps1。" -ForegroundColor Yellow
    exit 1
}

$releaseRoot = Join-Path $repoRoot "release"
$stageDir = Join-Path $releaseRoot ("iGCRGB_v" + $Version + "_win-x64")
$zipPath = Join-Path $releaseRoot ("iGCRGB_v" + $Version + "_win-x64.zip")

Write-Host ""
Write-Host ("iGCRGB - 生成发布包 v" + $Version) -ForegroundColor Cyan
Write-Host "---------------------------------------------"

if (Test-Path -LiteralPath $stageDir) { Remove-Item -LiteralPath $stageDir -Recurse -Force }
New-Item -ItemType Directory -Force -Path $stageDir | Out-Null

# 程序本体与配置
Copy-Item -LiteralPath $exe -Destination $stageDir -Force
$exeConfig = Join-Path $distDir "iGCRGB.exe.config"
if (Test-Path -LiteralPath $exeConfig) {
    Copy-Item -LiteralPath $exeConfig -Destination $stageDir -Force
}

# 文档
Copy-Item -LiteralPath (Join-Path $repoRoot "docs\USAGE_zh.txt") -Destination (Join-Path $stageDir "使用说明.txt") -Force
Copy-Item -LiteralPath (Join-Path $repoRoot "LICENSE") -Destination $stageDir -Force
Copy-Item -LiteralPath (Join-Path $repoRoot "NOTICE") -Destination $stageDir -Force

# 组件准备脚本
Copy-Item -LiteralPath (Join-Path $repoRoot "packaging\setup-vendor.bat") -Destination $stageDir -Force
Copy-Item -LiteralPath (Join-Path $repoRoot "packaging\setup-vendor.ps1") -Destination $stageDir -Force
Copy-Item -LiteralPath (Join-Path $repoRoot "packaging\组件说明.txt") -Destination $stageDir -Force

# 注意：这里刻意不复制 iGameAPI 组件
$vendorCheck = Join-Path $stageDir "iGameAPI"
if (Test-Path -LiteralPath $vendorCheck) { Remove-Item -LiteralPath $vendorCheck -Recurse -Force }

if (Test-Path -LiteralPath $zipPath) { Remove-Item -LiteralPath $zipPath -Force }
Compress-Archive -Path (Join-Path $stageDir "*") -DestinationPath $zipPath -CompressionLevel Optimal

$sizeKb = [math]::Round((Get-Item -LiteralPath $zipPath).Length / 1KB, 1)
Write-Host ""
Write-Host ("发布包已生成：" + $zipPath) -ForegroundColor Green
Write-Host ("大小：" + $sizeKb + " KB") -ForegroundColor Gray
Write-Host "包含：iGCRGB.exe、使用说明、setup-vendor 脚本、LICENSE/NOTICE" -ForegroundColor Gray
Write-Host "不包含：七彩虹 iGameAPI 组件（由用户运行 setup-vendor.bat 自行准备）" -ForegroundColor Gray
