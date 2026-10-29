<#
.SYNOPSIS
    Publishes a self-contained Release build of Snare and replaces the installed copy.

.DESCRIPTION
    Stops any running Snare, then runs `dotnet publish` for win-x64 into the destination
    (default: %LOCALAPPDATA%\Programs\Snare). There is no installer and no auto-update;
    run this again to update. The library and settings in %LOCALAPPDATA%\Snare are untouched.

.EXAMPLE
    ./tools/publish.ps1
#>
[CmdletBinding()]
param(
    [string] $Destination = (Join-Path $env:LOCALAPPDATA 'Programs\Snare')
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repoRoot 'src\Snare.App\Snare.App.csproj'

# Snare is a tray app with no main window, so it can't be asked to close politely.
$running = Get-Process -Name 'Snare' -ErrorAction SilentlyContinue |
    Where-Object { $_.Path -and $_.Path.StartsWith($Destination, [StringComparison]::OrdinalIgnoreCase) }
foreach ($process in $running) {
    Write-Host "Stopping Snare (pid $($process.Id))"
    $process | Stop-Process -Force
    $process.WaitForExit(5000) | Out-Null
}

dotnet publish $project `
    --configuration Release `
    --runtime win-x64 `
    --self-contained true `
    -p:Platform=x64 `
    --output $Destination
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE"
}

Write-Host "Published to $Destination"
Write-Host "Start it with: & '$(Join-Path $Destination 'Snare.exe')'"
