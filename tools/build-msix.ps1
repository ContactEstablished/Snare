<#
.SYNOPSIS
    Builds a signed Snare MSIX into artifacts\msix.

.DESCRIPTION
    Publishes a self-contained win-x64 build, lays it out with tools\msix\AppxManifest.xml and
    generated logos, packs it with makeappx and signs it with signtool (both from the Windows SDK).

    Signing uses a self-signed "CN=Snare Dev" certificate in Cert:\CurrentUser\My, created on the
    first run. Its public part is exported to artifacts\msix\Snare.cer. Windows trusts the package
    only once that certificate is in Local Machine\Trusted People; tools\install-msix.ps1 does that
    (one admin prompt) and installs the package.

    FFmpeg isn't bundled; see tools\ffmpeg\README.md.

.EXAMPLE
    ./tools/build-msix.ps1
#>
[CmdletBinding()]
param(
    # Must go up with every build so an installed package can be updated. Defaults to the date and time.
    [string] $Version = ('1.{0:yy}.{1}.{0:HHmm}' -f (Get-Date), (Get-Date).DayOfYear),
    [string] $Publisher = 'CN=Snare Dev'
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$outputDir = Join-Path $repoRoot 'artifacts\msix'
$layout = Join-Path $outputDir 'layout'
$package = Join-Path $outputDir "Snare-$Version.msix"

function Find-SdkTool([string] $name) {
    $tool = Get-ChildItem (Join-Path ${env:ProgramFiles(x86)} "Windows Kits\10\bin\*\x64\$name") -ErrorAction SilentlyContinue |
        Sort-Object { [version] $_.Directory.Parent.Name } -Descending |
        Select-Object -First 1
    if (-not $tool) { throw "$name wasn't found. Install the Windows SDK (winget install Microsoft.WindowsSDK.10.0.26100)." }
    return $tool.FullName
}

function Save-Logo([string] $path, [int] $size) {
    Add-Type -AssemblyName System.Drawing
    $bitmap = [System.Drawing.Bitmap]::new($size, $size)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    try {
        $graphics.SmoothingMode = 'AntiAlias'
        $graphics.TextRenderingHint = 'AntiAliasGridFit'
        $graphics.Clear([System.Drawing.Color]::FromArgb(255, 37, 99, 235))
        $font = [System.Drawing.Font]::new('Segoe UI', [float] ($size * 0.6), [System.Drawing.FontStyle]::Bold, [System.Drawing.GraphicsUnit]::Pixel)
        $format = [System.Drawing.StringFormat]::new()
        $format.Alignment = 'Center'
        $format.LineAlignment = 'Center'
        $graphics.DrawString('S', $font, [System.Drawing.Brushes]::White, [System.Drawing.RectangleF]::new(0, 0, $size, $size), $format)
        $bitmap.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
    }
    finally {
        $graphics.Dispose()
        $bitmap.Dispose()
    }
}

$makeappx = Find-SdkTool 'makeappx.exe'
$signtool = Find-SdkTool 'signtool.exe'

if (Test-Path $layout) { Remove-Item $layout -Recurse -Force }
dotnet publish (Join-Path $repoRoot 'src\Snare.App\Snare.App.csproj') `
    --configuration Release --runtime win-x64 --self-contained true -p:Platform=x64 `
    --output $layout
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed with exit code $LASTEXITCODE" }

$assets = New-Item -ItemType Directory -Force (Join-Path $layout 'Assets')
Save-Logo (Join-Path $assets 'StoreLogo.png') 50
Save-Logo (Join-Path $assets 'Square44x44Logo.png') 44
Save-Logo (Join-Path $assets 'Square150x150Logo.png') 150

(Get-Content (Join-Path $PSScriptRoot 'msix\AppxManifest.xml') -Raw).
    Replace('$VERSION$', $Version).
    Replace('$PUBLISHER$', $Publisher) |
    Set-Content (Join-Path $layout 'AppxManifest.xml') -Encoding utf8

Get-ChildItem $outputDir -Filter 'Snare-*.msix' -ErrorAction SilentlyContinue | Remove-Item -Force
& $makeappx pack /o /d $layout /p $package
if ($LASTEXITCODE -ne 0) { throw "makeappx failed with exit code $LASTEXITCODE" }

$cert = Get-ChildItem Cert:\CurrentUser\My |
    Where-Object { $_.Subject -eq $Publisher -and $_.HasPrivateKey -and $_.NotAfter -gt (Get-Date) } |
    Sort-Object NotAfter -Descending |
    Select-Object -First 1
if (-not $cert) {
    Write-Host "Creating the self-signed signing certificate $Publisher"
    $cert = New-SelfSignedCertificate -Type Custom -Subject $Publisher -KeyUsage DigitalSignature `
        -CertStoreLocation Cert:\CurrentUser\My -NotAfter (Get-Date).AddYears(5) `
        -TextExtension @('2.5.29.37={text}1.3.6.1.5.5.7.3.3', '2.5.29.19={text}')
}
Export-Certificate -Cert $cert -FilePath (Join-Path $outputDir 'Snare.cer') | Out-Null

& $signtool sign /fd SHA256 /sha1 $cert.Thumbprint /s My $package
if ($LASTEXITCODE -ne 0) { throw "signtool failed with exit code $LASTEXITCODE" }

Write-Host "Built $package"
Write-Host "Install it with: ./tools/install-msix.ps1"
