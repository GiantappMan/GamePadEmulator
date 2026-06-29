<#
.SYNOPSIS
    GamePad Emulator 一键打包脚本：构建自包含 x64 发布 + Inno Setup 安装包。

.DESCRIPTION
    流程：
      1. 定位 ISCC.exe（PATH -> 注册表卸载键 -> 常见安装路径）
      2. 读取版本号（csproj <Version>，可用 -Version 覆盖）
      3. dotnet publish：自包含 win-x64（默认多文件；-SingleFile 切单 exe）
      4. 清理旧产物 -> ISCC 编译 .iss
      5. 产出 GamePadEmulator-<version>-x64-Setup.exe 并报告体积

.PARAMETER Version
    覆盖 csproj 中读到的版本号（需为 x.y[.z[.w]] 格式）。

.PARAMETER SingleFile
    切换为单文件发布（PublishSingleFile=true）。
    默认多文件 self-contained：零第三方库兼容性风险，最稳定。
    单 exe：安装目录更干净，但首启会解压到临时目录。

.PARAMETER SkipBuild
    跳过 dotnet publish，仅用现有 publish\ 重新打包（调试 .iss 用）。

.EXAMPLE
    pwsh tools/packaging/build-installer.ps1
    pwsh tools/packaging/build-installer.ps1 -Version 1.2.0 -SingleFile
    pwsh tools/packaging/build-installer.ps1 -SkipBuild
#>

[CmdletBinding()]
param(
    [string]$Version,
    [switch]$SingleFile,
    [switch]$SkipBuild
)

$ErrorActionPreference = 'Stop'

# ---------------------------------------------------------------------------
# 0. 路径定位（脚本可在任意 cwd 下正确运行）
# ---------------------------------------------------------------------------
$PackagingDir = Split-Path -Parent $MyInvocation.MyCommand.Definition
$RepoRoot     = Split-Path -Parent (Split-Path -Parent $PackagingDir)
$ProjectPath  = Join-Path $RepoRoot 'src/GamePadEmulator/GamePadEmulator.csproj'
$IssFile      = Join-Path $PackagingDir 'GamePadEmulator.iss'
$PublishDir   = Join-Path $PackagingDir 'publish'
$OutputDir    = Join-Path $PackagingDir 'output'

function Write-Step([string]$msg) { Write-Host ""
Write-Host "==> $msg" -ForegroundColor Cyan }
function Write-Done([string]$msg) { Write-Host "    OK  $msg" -ForegroundColor Green }

Write-Host "==========================================" -ForegroundColor DarkCyan
Write-Host "  GamePad Emulator 打包脚本" -ForegroundColor Cyan
Write-Host "==========================================" -ForegroundColor DarkCyan

# ---------------------------------------------------------------------------
# 1. 定位 ISCC
# ---------------------------------------------------------------------------
Write-Step "定位 Inno Setup 编译器 (ISCC.exe)"

function Find-Iscc {
    # a) PATH
    $cmd = Get-Command ISCC.exe -ErrorAction SilentlyContinue
    if ($cmd) { return $cmd.Source }

    # b) 注册表卸载键（标准安装会写这里）
    $regPaths = @(
        'HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\Inno Setup 6_is1',
        'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Inno Setup 6_is1'
    )
    foreach ($rp in $regPaths) {
        if (Test-Path $rp) {
            $loc = (Get-ItemProperty $rp -ErrorAction SilentlyContinue).InstallLocation
            if ($loc) {
                $cand = Join-Path $loc 'ISCC.exe'
                if (Test-Path $cand) { return $cand }
            }
        }
    }

    # c) 常见安装路径
    $common = @(
        "$env:ProgramFiles\Inno Setup 6\ISCC.exe",
        "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe"
    )
    foreach ($c in $common) {
        if (Test-Path $c) { return $c }
    }
    return $null
}

$Iscc = Find-Iscc
if (-not $Iscc) {
    throw @"
未找到 Inno Setup 6 (ISCC.exe)。
请安装 Inno Setup 6：https://jrsoftware.org/isdl.php
安装后重试，或将 ISCC.exe 所在目录加入 PATH。
"@
}
Write-Done "ISCC: $Iscc"

# ---------------------------------------------------------------------------
# 2. 读取版本号
# ---------------------------------------------------------------------------
Write-Step "读取版本号"
if (-not $Version) {
    $csprojXml = [xml](Get-Content -Raw $ProjectPath)
    $Version = $csprojXml.Project.PropertyGroup.Version
}
if (-not $Version) { throw "无法从 csproj 读取 <Version>，请用 -Version 显式指定。" }
if ($Version -notmatch '^\d+\.\d+(\.\d+){0,2}$') {
    throw "版本号格式非法（需为 x.y[.z[.w]]）：$Version"
}
Write-Done "版本: $Version"

# ---------------------------------------------------------------------------
# 3. dotnet publish（自包含 x64）
# ---------------------------------------------------------------------------
$publishSingleFile    = if ($SingleFile) { 'true' } else { 'false' }
$includeNativeForSelf = if ($SingleFile) { 'true' } else { 'false' }

if ($SkipBuild) {
    Write-Step "跳过构建（-SkipBuild），使用现有 publish\"
    if (-not (Test-Path (Join-Path $PublishDir '*'))) {
        throw "publish\ 为空且指定了 -SkipBuild。请先去掉 -SkipBuild 完整构建一次。"
    }
} else {
    Write-Step "dotnet publish（self-contained, win-x64, SingleFile=$publishSingleFile）"
    if (Test-Path $PublishDir) {
        Get-ChildItem $PublishDir -Force | Remove-Item -Recurse -Force
    }

    & dotnet publish $ProjectPath `
        -c Release `
        -r win-x64 `
        --self-contained true `
        -p:DebugType=none `
        -p:DebugSymbols=false `
        -p:PublishSingleFile=$publishSingleFile `
        -p:IncludeNativeLibrariesForSelfExtract=$includeNativeForSelf `
        -o $PublishDir
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish 失败 (ExitCode=$LASTEXITCODE)。" }

    $exe = Join-Path $PublishDir 'GamePadEmulator.exe'
    if (-not (Test-Path $exe)) { throw "发布目录缺少 GamePadEmulator.exe：$exe" }
    Write-Done "发布完成 -> $PublishDir"
}

# ---------------------------------------------------------------------------
# 4. 清理旧产物 + ISCC 编译
# ---------------------------------------------------------------------------
Write-Step "编译安装包 (.iss)"
if (-not (Test-Path $OutputDir)) { New-Item -ItemType Directory -Path $OutputDir | Out-Null }
$expectedInstaller = Join-Path $OutputDir "GamePadEmulator-$Version-x64-Setup.exe"
if (Test-Path $expectedInstaller) { Remove-Item $expectedInstaller -Force }

# 工作目录设为 packaging\，使 .iss 里相对路径（publish\、output\）对齐。
Push-Location $PackagingDir
try {
    & $Iscc /Qp "/DVersion=$Version" $IssFile
    if ($LASTEXITCODE -ne 0) { throw "ISCC 编译失败 (ExitCode=$LASTEXITCODE)。" }
} finally {
    Pop-Location
}

# ---------------------------------------------------------------------------
# 5. 结果报告
# ---------------------------------------------------------------------------
if (-not (Test-Path $expectedInstaller)) {
    throw "ISCC 报告成功但未找到产物：$expectedInstaller"
}
$sizeMB = [math]::Round((Get-Item $expectedInstaller).Length / 1MB, 2)

Write-Host ""
Write-Host "==========================================" -ForegroundColor DarkGreen
Write-Host "  打包成功！" -ForegroundColor Green
Write-Host "==========================================" -ForegroundColor DarkGreen
Write-Host "  产物: $expectedInstaller"
Write-Host "  体积: $sizeMB MB"
Write-Host ""
