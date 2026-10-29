<#
.SYNOPSIS
    Installs (or updates to) the newest Snare MSIX from artifacts\msix.

.DESCRIPTION
    The first time, puts artifacts\msix\Snare.cer in Local Machine\Trusted People so Windows
    accepts the self-signed package. That step asks for admin once. Then installs the package
    for the current user, stopping a running Snare first. The library and settings in
    %LOCALAPPDATA%\Snare are kept across updates and uninstalls.

.EXAMPLE
    ./tools/install-msix.ps1
#>
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$outputDir = Join-Path (Split-Path -Parent $PSScriptRoot) 'artifacts\msix'
$package = Get-ChildItem $outputDir -Filter 'Snare-*.msix' -ErrorAction SilentlyContinue |
    Sort-Object LastWriteTime -Descending |
    Select-Object -First 1
if (-not $package) { throw "No package in $outputDir. Run ./tools/build-msix.ps1 first." }
$certFile = Join-Path $outputDir 'Snare.cer'

$cert = [System.Security.Cryptography.X509Certificates.X509Certificate2]::new($certFile)
if (-not (Get-ChildItem Cert:\LocalMachine\TrustedPeople | Where-Object Thumbprint -eq $cert.Thumbprint)) {
    Write-Host 'Trusting the Snare signing certificate (admin prompt)'
    $import = "Import-Certificate -FilePath '$certFile' -CertStoreLocation Cert:\LocalMachine\TrustedPeople | Out-Null"
    $process = Start-Process pwsh -Verb RunAs -Wait -PassThru -ArgumentList '-NoProfile', '-Command', $import
    if ($process.ExitCode -ne 0) { throw 'Could not trust the certificate.' }
}

# Snare is a tray app with no window, so it can't be asked to close politely.
Get-Process -Name 'Snare' -ErrorAction SilentlyContinue | Stop-Process -Force

Add-AppxPackage -Path $package.FullName -ForceUpdateFromAnyVersion
Write-Host "Installed $($package.Name). Start Snare from the Start menu."
