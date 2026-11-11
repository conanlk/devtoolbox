# Installs the latest DevToolBox release on Windows (per user, no admin rights needed).
#
#   irm https://raw.githubusercontent.com/conanlk/devtoolbox/master/install.ps1 | iex
#
# Downloads DevToolBox-<version>-windows-x64-Setup.exe, checks it against the release's SHA256SUMS.txt and runs it
# silently for the current user (%LOCALAPPDATA%\Programs\DevToolBox). Files downloaded by PowerShell don't get the
# "Mark of the Web", so SmartScreen usually doesn't stop the unsigned installer.
#
# Options (environment variables, set them before running the line above):
#   $env:DEVTOOLBOX_VERSION = '0.1.0'       install that version instead of the latest release
#   $env:DEVTOOLBOX_REPO = 'owner/name'     download from another GitHub repository (forks)
#   $env:DEVTOOLBOX_INTERACTIVE = '1'       show the setup wizard instead of installing silently
#
# Works in Windows PowerShell 5.1 and PowerShell 7. Errors are thrown rather than calling `exit`, which would
# close the window when the script runs through `iex`.

& {
    $ErrorActionPreference = 'Stop'
    $ProgressPreference = 'SilentlyContinue'   # Invoke-WebRequest is very slow in 5.1 with the progress bar on
    [Net.ServicePointManager]::SecurityProtocol = [Net.ServicePointManager]::SecurityProtocol -bor [Net.SecurityProtocolType]::Tls12

    $repo = if ($env:DEVTOOLBOX_REPO) { $env:DEVTOOLBOX_REPO } else { 'conanlk/devtoolbox' }
    $base = "https://github.com/$repo/releases"

    # x64 builds only; Windows 11 on Arm runs them through emulation.
    if (-not [Environment]::Is64BitOperatingSystem) { throw 'DevToolBox needs 64-bit Windows.' }

    $version = $env:DEVTOOLBOX_VERSION
    if (-not $version) {
        # /releases/latest redirects to /releases/tag/v<version>; no API call, so no rate limit.
        try {
            $response = Invoke-WebRequest -Uri "$base/latest" -Method Head -UseBasicParsing
        }
        catch {
            throw "Could not reach $base (is the repository public and does it have a published release?) $_"
        }
        $finalUri = if ($response.BaseResponse.ResponseUri) { $response.BaseResponse.ResponseUri }  # 5.1
                    else { $response.BaseResponse.RequestMessage.RequestUri }                      # 7+
        $version = ($finalUri.AbsolutePath -split '/')[-1]
    }
    $version = $version.TrimStart('v')
    if ($version -notmatch '^\d+\.\d+\.\d+$') { throw "No published release found at $base" }

    $asset = "DevToolBox-$version-windows-x64-Setup.exe"
    $tmp = Join-Path ([IO.Path]::GetTempPath()) ("devtoolbox-" + [Guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $tmp | Out-Null
    try {
        $setup = Join-Path $tmp $asset
        $sums = Join-Path $tmp 'SHA256SUMS.txt'

        Write-Host "==> Downloading DevToolBox $version ($asset)"
        Invoke-WebRequest -Uri "$base/download/v$version/$asset" -OutFile $setup -UseBasicParsing
        try {
            Invoke-WebRequest -Uri "$base/download/v$version/SHA256SUMS.txt" -OutFile $sums -UseBasicParsing
        }
        catch {
            throw "This release has no SHA256SUMS.txt, so the download can't be checked."
        }

        $line = Get-Content $sums | Where-Object { (($_ -split '\s+', 2)[1] -replace '^\*', '') -eq $asset } | Select-Object -First 1
        if (-not $line) { throw "$asset is not listed in SHA256SUMS.txt" }
        $expected = ($line -split '\s+')[0].ToLowerInvariant()
        $actual = (Get-FileHash -Algorithm SHA256 -Path $setup).Hash.ToLowerInvariant()
        if ($actual -ne $expected) { throw "Checksum mismatch for $asset (expected $expected, got $actual)" }
        Write-Host '==> Checksum OK'

        Unblock-File -Path $setup   # in case something added the Mark of the Web anyway

        # /CURRENTUSER installs per user (no UAC prompt); the Setup closes a running DevToolBox before updating.
        $arguments = if ($env:DEVTOOLBOX_INTERACTIVE -eq '1') { @('/CURRENTUSER') }
                     else { @('/CURRENTUSER', '/SILENT', '/SUPPRESSMSGBOXES', '/NORESTART', '/CLOSEAPPLICATIONS') }
        Write-Host '==> Installing'
        $process = Start-Process -FilePath $setup -ArgumentList $arguments -Wait -PassThru
        if ($process.ExitCode -ne 0) { throw "Setup failed with exit code $($process.ExitCode)" }

        Write-Host '==> Done. Start DevToolBox from the Start menu.'
    }
    finally {
        Remove-Item -Recurse -Force -Path $tmp -ErrorAction SilentlyContinue
    }
}
