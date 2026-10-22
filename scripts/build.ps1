#Requires -Version 5.1
<#
.SYNOPSIS
    构建 iGCRGB 并生成可直接运行的 dist/ 目录。

.DESCRIPTION
    需要 .NET SDK 8（或 6/7）。
    首次构建前需要厂商组件：如果 vendor/ 中缺少 iGameAPI.Contracts.dll，
    脚本会自动调用 prepare-vendor-libs.ps1，从本机的 iGC.Lite 安装目录复制。

.PARAMETER Configuration
    Debug 或 Release（默认 Release）。

.PARAMETER IgcPath
    传给 prepare-vendor-libs.ps1 的 iGC 安装目录。

.PARAMETER SkipVendorCheck
    跳过厂商组件检查（例如已经通过 -p:VendorDir=... 指定了其他位置）。

.PARAMETER Zip
    构建完成后额外生成 dist/iGCRGB_v<版本>.zip。

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File scripts\build.ps1

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File scripts\build.ps1 -Configuration Debug -Zip
#>
param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",
    [string]$IgcPath = "",
    [switch]$SkipVendorCheck,
    [switch]$Zip
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repoRoot "src\iGCRGB.Gui\iGCRGB.Gui.csproj"
$vendorDir = Join-Path $repoRoot "vendor"
$distRoot = Join-Path $repoRoot "dist"
$distDir = Join-Path $distRoot "iGCRGB"

Write-Host ""
Write-Host "iGCRGB - 构建" -ForegroundColor Cyan
Write-Host "---------------------------------------------"

function Require-Dotnet {
    $command = Get-Command dotnet -ErrorAction SilentlyContinue
    if (-not $command) {
        Write-Host "未找到 dotnet 命令。请安装 .NET SDK 8：" -ForegroundColor Red
        Write-Host "  https://dotnet.microsoft.com/download/dotnet/8.0" -ForegroundColor White
        exit 1
    }
    $sdks = & dotnet --list-sdks 2>$null
    if (-not $sdks) {
        Write-Host "找到了 dotnet，但没有安装任何 SDK。请安装 .NET SDK 8：" -ForegroundColor Red
        Write-Host "  https://dotnet.microsoft.com/download/dotnet/8.0" -ForegroundColor White
        exit 1
    }
    Write-Host ("dotnet SDK：" + (($sdks -split "`r?`n")[0]))
}

Require-Dotnet

if (-not $SkipVendorCheck) {
    $contracts = Join-Path $vendorDir "iGameAPI.Contracts.dll"
    if (-not (Test-Path -LiteralPath $contracts)) {
        Write-Host "vendor/ 中缺少厂商组件，尝试自动准备……" -ForegroundColor Yellow
        $prepare = Join-Path $PSScriptRoot "prepare-vendor-libs.ps1"
        if ([string]::IsNullOrWhiteSpace($IgcPath)) {
            & powershell -NoProfile -ExecutionPolicy Bypass -File $prepare
        }
        else {
            & powershell -NoProfile -ExecutionPolicy Bypass -File $prepare -IgcPath $IgcPath
        }
        if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $contracts)) {
            Write-Host "厂商组件准备失败，无法继续构建。" -ForegroundColor Red
            exit 1
        }
    }
}

Write-Host "编译项目……"
& dotnet build $project -c $Configuration --nologo
if ($LASTEXITCODE -ne 0) {
    Write-Host "编译失败。" -ForegroundColor Red
    exit 1
}

$outputDir = Join-Path $repoRoot ("src\iGCRGB.Gui\bin\" + $Configuration + "\net48")
$exe = Join-Path $outputDir "iGCRGB.exe"
if (-not (Test-Path -LiteralPath $exe)) {
    Write-Host "编译产物不存在：" + $exe -ForegroundColor Red
    exit 1
}

# 组装可运行目录
if (Test-Path -LiteralPath $distDir) {
    Remove-Item -LiteralPath $distDir -Recurse -Force
}
New-Item -ItemType Directory -Force -Path (Join-Path $distDir "iGameAPI") | Out-Null

Copy-Item -LiteralPath $exe -Destination $distDir -Force
$exeConfig = Join-Path $outputDir "iGCRGB.exe.config"
if (Test-Path -LiteralPath $exeConfig) {
    Copy-Item -LiteralPath $exeConfig -Destination $distDir -Force
}

# 厂商运行时组件
Copy-Item -LiteralPath (Join-Path $vendorDir "iGameAPI.Contracts.dll") -Destination $distDir -Force
$vendorApi = Join-Path $vendorDir "iGameAPI"
if (Test-Path -LiteralPath $vendorApi) {
    Copy-Item -Path (Join-Path $vendorApi "*") -Destination (Join-Path $distDir "iGameAPI") -Force
}

# 使用说明
$usage = Join-Path $repoRoot "docs\USAGE_zh.txt"
if (Test-Path -LiteralPath $usage) {
    Copy-Item -LiteralPath $usage -Destination (Join-Path $distDir "使用说明.txt") -Force
}

Write-Host ""
Write-Host ("构建完成：" + (Join-Path $distDir "iGCRGB.exe")) -ForegroundColor Green

if ($Zip) {
    $version = "2.0.0"
    $csprojText = Get-Content -LiteralPath $project -Raw -Encoding UTF8
    $match = [regex]::Match($csprojText, "<Version>([^<]+)</Version>")
    if ($match.Success) { $version = $match.Groups[1].Value.Trim() }
    $zipPath = Join-Path $distRoot ("iGCRGB_v" + $version + ".zip")
    if (Test-Path -LiteralPath $zipPath) { Remove-Item -LiteralPath $zipPath -Force }
    Compress-Archive -Path (Join-Path $distDir "*") -DestinationPath $zipPath -CompressionLevel Optimal
    Write-Host ("已打包：" + $zipPath) -ForegroundColor Green
}

Write-Host ""
Write-Host "提示：dist/iGCRGB 中的 iGameAPI 组件属于七彩虹，仅限本机使用，请勿上传到公开仓库/Release。" -ForegroundColor Gray
