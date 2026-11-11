<#
.SYNOPSIS
  Builds artifacts\installers\DevToolBox-<version>-windows-<arch>-Setup.exe on Windows.

.DESCRIPTION
  Publishes Native AOT for win-<arch>, runs --smoke-exit on it, then compiles DevToolBox.iss with Inno Setup 6.
  Needs: .NET 10 SDK, Visual Studio Build Tools with "Desktop development with C++", and Inno Setup 6
  (https://jrsoftware.org/isinfo.php, or: winget install JRSoftware.InnoSetup).

  The Setup.exe is unsigned: SmartScreen shows "Windows protected your PC" until the user clicks
  More info > Run anyway. Signing needs a code-signing certificate (see docs/installers.md).

.EXAMPLE
  powershell -ExecutionPolicy Bypass -File build\installers\windows\build-setup.ps1
  powershell -ExecutionPolicy Bypass -File build\installers\windows\build-setup.ps1 -Arch arm64
#>
param(
    [ValidateSet('x64', 'arm64')]
    [string] $Arch = 'x64'
)

$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path
Set-Location $repo

$rid = "win-$Arch"
$publishDir = Join-Path $repo "artifacts\publish\$rid"
$outputDir = Join-Path $repo 'artifacts\installers'

function Find-InnoSetupCompiler {
    $onPath = Get-Command ISCC.exe -ErrorAction SilentlyContinue
    if ($onPath) { return $onPath.Source }
    foreach ($dir in @(${env:ProgramFiles(x86)}, $env:ProgramFiles, (Join-Path $env:LOCALAPPDATA 'Programs'))) {
        if (-not $dir) { continue }
        $candidate = Join-Path $dir 'Inno Setup 6\ISCC.exe'
        if (Test-Path $candidate) { return $candidate }
    }
    throw 'Inno Setup 6 not found. Install it from https://jrsoftware.org/isinfo.php (or: winget install JRSoftware.InnoSetup).'
}

$iscc = Find-InnoSetupCompiler

Write-Host "==> Publishing $rid"
dotnet publish src\DevToolBox.App -c Release -r $rid -o $publishDir
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed ($LASTEXITCODE)" }

# Publish has just written the build number it used back to version.json.
$versionJson = Get-Content (Join-Path $repo 'version.json') -Raw | ConvertFrom-Json
$version = $versionJson.version
$build = $versionJson.build

Write-Host '==> Checking the published app'
$smoke = & (Join-Path $publishDir 'DevToolBox.exe') --smoke-exit | Out-String
Write-Host $smoke
if ($LASTEXITCODE -ne 0 -or $smoke -notmatch 'tools_ok=') { throw 'Smoke test failed' }

Write-Host '==> Building Setup.exe'
New-Item -ItemType Directory -Force -Path $outputDir | Out-Null
& $iscc /Qp `
    "/DAppVersion=$version" `
    "/DBuildNumber=$build" `
    "/DArch=$Arch" `
    "/DSourceDir=$publishDir" `
    "/DOutputDir=$outputDir" `
    (Join-Path $PSScriptRoot 'DevToolBox.iss')
if ($LASTEXITCODE -ne 0) { throw "Inno Setup failed ($LASTEXITCODE)" }

$setup = Join-Path $outputDir "DevToolBox-$version-windows-$Arch-Setup.exe"
Write-Host ("==> {0} ({1:N1} MB)" -f $setup, ((Get-Item $setup).Length / 1MB))
