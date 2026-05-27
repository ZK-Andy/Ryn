# Downloads prebuilt saucer-bindings native libraries for Windows.
# Usage:
#   .\build\download-native.ps1              # download for current platform
#   .\build\download-native.ps1 -Rid win-x64 # download for specific RID
#   .\build\download-native.ps1 -All         # download for all platforms

param(
    [string]$Rid = "",
    [switch]$All
)

$ErrorActionPreference = "Stop"
$RepoRoot = Split-Path -Parent $PSScriptRoot
$Repo = "Yupmoh/Ryn"
$InteropDir = Join-Path (Join-Path $RepoRoot "src") "Ryn.Interop"

$AllRids = @("osx-arm64", "linux-x64", "win-x64")

function Get-GitHubToken {
    if ($env:GITHUB_TOKEN) { return $env:GITHUB_TOKEN }
    if ($env:GH_TOKEN) { return $env:GH_TOKEN }

    try {
        $credInput = "protocol=https`nhost=github.com`n"
        $tempFile = [System.IO.Path]::GetTempFileName()
        [System.IO.File]::WriteAllText($tempFile, $credInput)
        $output = cmd /c "git credential fill < `"$tempFile`"" 2>$null
        Remove-Item $tempFile -ErrorAction SilentlyContinue
        foreach ($line in $output) {
            if ($line -match "^password=(.+)$") {
                return $Matches[1]
            }
        }
    }
    catch {}

    return $null
}

function Get-AuthHeaders {
    $token = Get-GitHubToken
    $headers = @{ "Accept" = "application/vnd.github+json" }
    if ($token) {
        $headers["Authorization"] = "Bearer $token"
    }
    return $headers
}

function Get-ArchiveExt($rid) {
    if ($rid -like "win-*") { return ".zip" } else { return ".tar.gz" }
}

function Download-Rid($rid) {
    $ext = Get-ArchiveExt $rid
    $archiveName = "saucer-bindings-${rid}${ext}"
    $dest = Join-Path (Join-Path (Join-Path $InteropDir "runtimes") $rid) "native"
    $tempFile = Join-Path $env:TEMP $archiveName

    New-Item -ItemType Directory -Force -Path $dest | Out-Null

    Write-Host "==> Downloading $archiveName..."

    $headers = Get-AuthHeaders
    $apiBase = "https://api.github.com/repos/$Repo"

    try {
        $releases = Invoke-RestMethod -Uri "$apiBase/releases" -Headers $headers -UseBasicParsing
        $release = $null

        # Try latest release first, then fall back to native-v* tagged releases
        foreach ($r in $releases) {
            $asset = $r.assets | Where-Object { $_.name -eq $archiveName } | Select-Object -First 1
            if ($asset) {
                $release = $r
                break
            }
        }

        if (-not $release) {
            foreach ($r in $releases) {
                if ($r.tag_name -like "native-v*") {
                    $asset = $r.assets | Where-Object { $_.name -eq $archiveName } | Select-Object -First 1
                    if ($asset) {
                        $release = $r
                        break
                    }
                }
            }
        }

        if (-not $release -or -not $asset) {
            Write-Host "    No release found containing $archiveName."
            return $false
        }

        Write-Host "    Found in release $($release.tag_name)"

        $downloadHeaders = @{ "Accept" = "application/octet-stream" }
        $token = Get-GitHubToken
        if ($token) { $downloadHeaders["Authorization"] = "Bearer $token" }

        Invoke-WebRequest -Uri $asset.url -Headers $downloadHeaders -OutFile $tempFile -UseBasicParsing
    }
    catch {
        Write-Host "    Download failed: $_"
        return $false
    }

    Write-Host "    Extracting to $dest..."
    if ($ext -eq ".zip") {
        Expand-Archive -Path $tempFile -DestinationPath $dest -Force
    }
    else {
        tar -xzf $tempFile -C $dest
    }

    Remove-Item -Path $tempFile -ErrorAction SilentlyContinue
    Write-Host "    Done: $(Get-ChildItem $dest | ForEach-Object { $_.Name })"
    return $true
}

if ($All) {
    Write-Host "==> Downloading native libraries for all platforms..."
    foreach ($r in $AllRids) {
        if (-not (Download-Rid $r)) {
            Write-Host "    WARNING: Failed for $r, skipping."
        }
    }
}
elseif ($Rid) {
    if (-not (Download-Rid $Rid)) {
        Write-Host "==> Download failed for $Rid."
        exit 1
    }
}
else {
    $currentRid = "win-x64"
    if (-not (Download-Rid $currentRid)) {
        Write-Host ""
        Write-Host "==> Download failed. Build from source with:"
        Write-Host "    bash build/build-native.sh"
        exit 1
    }
}

Write-Host ""
Write-Host "==> Native libraries ready."
