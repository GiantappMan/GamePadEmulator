<#
.SYNOPSIS
    GamePad Emulator 一键发布：提升版本尾号 -> 打包安装程序 -> 提交 push -> 创建 GitHub Release。

.DESCRIPTION
    串联整个发布流程，任意一步失败即中止并退出非零码：
      1. 读取 csproj 当前 <Version>，将末位（patch）+1，回写 csproj。
      2. 调用 build-installer.ps1 生成 GamePadEmulator-<newVersion>-x64-Setup.exe。
      3. git add -> commit -> push（默认推 origin/main）。
      4. gh release create 上传安装包，生成 Release 与 Tag v<newVersion>。

.PARAMETER Bump
    版本提升策略，默认 Patch。可选 Major / Minor / Patch。

.PARAMETER Notes
    Release 说明（追加到自动生成的 changelog 后）。省略则用默认模板。

.PARAMETER Draft
    创建为草稿 Release，便于人工核对后再发布。

.PARAMETER Prerelease
    标记为预发布。

.PARAMETER SkipRelease
    仅提升版本 + 打包 + 提交 push，不创建 GitHub Release（排错用）。

.PARAMETER WhatIf
    只打印将要做的事，不修改任何文件、不执行命令。

.EXAMPLE
    pwsh tools/packaging/release.ps1                 # patch +1 并发布
    pwsh tools/packaging/release.ps1 -Bump Minor
    pwsh tools/packaging/release.ps1 -Draft          # 草稿便于复核
    pwsh tools/packaging/release.ps1 -SkipRelease    # 只到 push 为止
#>

[CmdletBinding(SupportsShouldProcess)]
param(
    [ValidateSet('Major','Minor','Patch')]
    [string]$Bump = 'Patch',

    [string]$Notes,

    [switch]$Draft,
    [switch]$Prerelease,
    [switch]$SkipRelease
)

$ErrorActionPreference = 'Stop'

# ---------------------------------------------------------------------------
# 0. 路径定位（脚本可在任意 cwd 下正确运行）
# ---------------------------------------------------------------------------
$PackagingDir   = Split-Path -Parent $MyInvocation.MyCommand.Definition
$RepoRoot       = Split-Path -Parent (Split-Path -Parent $PackagingDir)
$ProjectPath    = Join-Path $RepoRoot 'src/GamePadEmulator/GamePadEmulator.csproj'
$BuildInstaller = Join-Path $PackagingDir 'build-installer.ps1'
$OutputDir      = Join-Path $PackagingDir 'output'

function Write-Step([string]$m) { Write-Host ""; Write-Host "==> $m" -ForegroundColor Cyan }
function Write-Done([string]$m) { Write-Host "    OK  $m" -ForegroundColor Green }

Write-Host "==========================================" -ForegroundColor DarkCyan
Write-Host "  GamePad Emulator 发布流程" -ForegroundColor Cyan
Write-Host "==========================================" -ForegroundColor DarkCyan

# ---------------------------------------------------------------------------
# 1. 提升版本尾号
# ---------------------------------------------------------------------------
Write-Step "提升版本尾号 ($Bump)"

# 直接正则替换 <Version>...</Version>，避免 XML 重新序列化打乱 csproj 格式。
$csprojText = Get-Content -Raw $ProjectPath
if ($csprojText -notmatch '(?s)<Version>(.*?)</Version>') {
    throw "csproj 中找不到 <Version> 节点：$ProjectPath"
}
$currentVersion = $Matches[1].Trim()
if ($currentVersion -notmatch '^\d+\.\d+\.\d+$') {
    throw "当前版本号格式非法（需 x.y.z）：$currentVersion"
}

$parts  = $currentVersion.Split('.') | ForEach-Object { [int]$_ }
switch ($Bump) {
    'Major' { $parts[0]++; $parts[1] = 0; $parts[2] = 0 }
    'Minor' { $parts[1]++; $parts[2] = 0 }
    'Patch' { $parts[2]++ }
}
$newVersion = ($parts -join '.')

if ($PSCmdlet.ShouldProcess($ProjectPath, "版本 $currentVersion -> $newVersion")) {
    $newCsprojText = $csprojText -replace '(?s)<Version>.*?</Version>', "<Version>$newVersion</Version>"
    Set-Content -Path $ProjectPath -Value $newCsprojText -NoNewline -Encoding UTF8
}
Write-Done "$currentVersion -> $newVersion"

# ---------------------------------------------------------------------------
# 2. 打包安装程序
# ---------------------------------------------------------------------------
Write-Step "打包安装程序"
if ($PSCmdlet.ShouldProcess('build-installer.ps1', "调用 (Version=$newVersion)")) {
    & pwsh -NoProfile -File $BuildInstaller -Version $newVersion
    if ($LASTEXITCODE -ne 0) { throw "build-installer.ps1 失败 (ExitCode=$LASTEXITCODE)。" }
}

$installer = Join-Path $OutputDir "GamePadEmulator-$newVersion-x64-Setup.exe"
if (-not $WhatIfPreference -and -not (Test-Path $installer)) { throw "未找到安装包产物：$installer" }
$sizeMB = if (Test-Path $installer) { [math]::Round((Get-Item $installer).Length / 1MB, 2) } else { 0 }
Write-Done "$installer ($sizeMB MB)"

# ---------------------------------------------------------------------------
# 3. git 提交并 push
# ---------------------------------------------------------------------------
Write-Step "git 提交并 push"
if ($PSCmdlet.ShouldProcess($RepoRoot, "git add / commit / push v$newVersion")) {
    Push-Location $RepoRoot
    try {
        git add -A
        # commit 允许在无可暂存变更时静默继续（极少见，但脚本要稳健）。
        & git commit -m "chore(release): v$newVersion" 2>&1 | Out-Host
        if ($LASTEXITCODE -ne 0 -and $LASTEXITCODE -ne 1) {
            throw "git commit 失败 (ExitCode=$LASTEXITCODE)。"
        }

        $branch = (git rev-parse --abbrev-ref HEAD).Trim()
        git push origin $branch
        if ($LASTEXITCODE -ne 0) { throw "git push 失败 (ExitCode=$LASTEXITCODE)。" }
        Write-Done "已 push 到 origin/$branch"
    } finally {
        Pop-Location
    }
}

# ---------------------------------------------------------------------------
# 4. GitHub Release
# ---------------------------------------------------------------------------
if ($SkipRelease) {
    Write-Step "跳过 GitHub Release (-SkipRelease)"
    Write-Host ""
    Write-Host "==========================================" -ForegroundColor DarkGreen
    Write-Host "  发布流程结束（未创建 Release）" -ForegroundColor Green
    Write-Host "==========================================" -ForegroundColor DarkGreen
    return
}

Write-Step "创建 GitHub Release"
$tagName = "v$newVersion"
$defaultNotes = "GamePad Emulator $tagName" + $(if ($Notes) { "`n`n$Notes" })
if (-not $Notes) { $defaultNotes += "`n`n安装：下载下方 Setup.exe 运行即可（需先安装 [ViGEmBus](https://github.com/ViGEm/ViGEmBus/releases/latest) 驱动）。" }

if ($PSCmdlet.ShouldProcess($RepoRoot, "gh release create $tagName")) {
    Push-Location $RepoRoot
    try {
        $ghArgs = @('release','create',$tagName,$installer,'--title',"GamePad Emulator $newVersion",'--notes',$defaultNotes,'--generate-notes')
        if ($Draft)      { $ghArgs += '--draft' }
        if ($Prerelease) { $ghArgs += '--prerelease' }
        & gh @ghArgs
        if ($LASTEXITCODE -ne 0) { throw "gh release create 失败 (ExitCode=$LASTEXITCODE)。" }
    } finally {
        Pop-Location
    }
}

Write-Host ""
Write-Host "==========================================" -ForegroundColor DarkGreen
Write-Host "  发布成功！" -ForegroundColor Green
Write-Host "==========================================" -ForegroundColor DarkGreen
Write-Host "  版本:  $newVersion"
Write-Host "  安装包: $installer ($sizeMB MB)"
Write-Host "  Tag:   $tagName"
Write-Host ""
