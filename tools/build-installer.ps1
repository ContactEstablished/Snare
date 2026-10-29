<#
.SYNOPSIS
    Builds Snare-Setup-<version>.exe, a per-user installer, into artifacts\installer.

.DESCRIPTION
    Publishes a self-contained win-x64 build, then compiles tools\installer\Snare.iss with
    Inno Setup. If ISCC.exe isn't installed, the Tools.InnoSetup NuGet package is downloaded
    into artifacts\inno-setup (ignored by git), so nothing is installed on the machine.
    FFmpeg isn't bundled; see tools\ffmpeg\README.md.

.EXAMPLE
    ./tools/build-installer.ps1 -Version 1.0.0
#>
[CmdletBinding()]
param(
    [string] $Version = '1.0.0',
    [string] $InnoSetupVersion = '6.4.3'
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$artifacts = Join-Path $repoRoot 'artifacts'
$publishDir = Join-Path $artifacts 'publish\win-x64'
$outputDir = Join-Path $artifacts 'installer'

function Find-Iscc {
    $installed = @(
        (Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6\ISCC.exe'),
        (Join-Path $env:ProgramFiles 'Inno Setup 6\ISCC.exe'),
        (Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6\ISCC.exe')
    ) | Where-Object { Test-Path $_ } | Select-Object -First 1
    if ($installed) { return $installed }

    $local = Join-Path $artifacts 'inno-setup\tools\ISCC.exe'
    if (-not (Test-Path $local)) {
        $package = Join-Path $artifacts "inno-setup-$InnoSetupVersion.nupkg"
        New-Item -ItemType Directory -Force (Split-Path $package) | Out-Null
        $url = "https://api.nuget.org/v3-flatcontainer/tools.innosetup/$InnoSetupVersion/tools.innosetup.$InnoSetupVersion.nupkg"
        Write-Host "Downloading Inno Setup $InnoSetupVersion"
        Invoke-WebRequest $url -OutFile $package
        Expand-Archive $package -DestinationPath (Join-Path $artifacts 'inno-setup') -Force
    }
    return $local
}

if (Test-Path $publishDir) { Remove-Item $publishDir -Recurse -Force }
dotnet publish (Join-Path $repoRoot 'src\Snare.App\Snare.App.csproj') `
    --configuration Release --runtime win-x64 --self-contained true -p:Platform=x64 `
    -p:Version=$Version --output $publishDir
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed with exit code $LASTEXITCODE" }

$iscc = Find-Iscc
& $iscc "/DAppVersion=$Version" "/DSourceDir=$publishDir" "/DOutputDir=$outputDir" (Join-Path $PSScriptRoot 'installer\Snare.iss')
if ($LASTEXITCODE -ne 0) { throw "ISCC failed with exit code $LASTEXITCODE" }

Write-Host "Built $(Join-Path $outputDir "Snare-Setup-$Version.exe")"
