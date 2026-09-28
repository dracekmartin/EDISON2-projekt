# Builds EDISONRUNTIME.zip - the private .NET runtime shipped with the EDISON installation.
# EDLOADER unpacks it to <EDISON>\dotnet\ on stations without a global .NET Desktop Runtime;
# the .NET exes (EDISONMAIN2, TemplateDesigner, EDUPDATE2) look there first
# (AppHostRelativeDotNet in SingleFilePublish.targets).
#
# The zip is the official dotnet-runtime + windowsdesktop-runtime win-x64 zips merged into one
# dotnet root (host\fxr, shared\Microsoft.NETCore.App, shared\Microsoft.WindowsDesktop.App),
# verified against the SHA512 from Microsoft release metadata, plus EDISONRUNTIME.version
# (EDLOADER compares it with the unpacked copy). dotnet.exe is left out, the apphost does not need it.
#
# Usage (Windows PowerShell 5.1):
#   powershell -File tools/build-runtime-zip.ps1                    # latest 10.0 patch
#   powershell -File tools/build-runtime-zip.ps1 -Version 10.0.10
#
# ASCII only -- Windows PowerShell 5.1 reads UTF-8-no-BOM as CP-1252.

[CmdletBinding()]
param(
    [string]$Version = "",
    [string]$Channel = "10.0",
    [string]$OutFile = "C:\Edison\EDISONRUNTIME.zip"
)

$ErrorActionPreference = "Stop"
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
Add-Type -AssemblyName System.IO.Compression.FileSystem

$metaUrl = "https://builds.dotnet.microsoft.com/dotnet/release-metadata/$Channel/releases.json"
$meta = Invoke-RestMethod -Uri $metaUrl
if ($Version -eq "") { $Version = $meta.'latest-runtime' }
$release = $meta.releases | Where-Object { $_.runtime.version -eq $Version } | Select-Object -First 1
if (-not $release) { throw "Runtime $Version not found in $metaUrl" }

function Get-ZipInfo($component, $fileName) {
    $file = $component.files | Where-Object { $_.name -eq $fileName } | Select-Object -First 1
    if (-not $file) { throw "$fileName missing in release metadata for $Version" }
    return $file
}

$parts = @(
    (Get-ZipInfo $release.runtime "dotnet-runtime-win-x64.zip"),
    (Get-ZipInfo $release.windowsdesktop "windowsdesktop-runtime-win-x64.zip")
)

$work = Join-Path ([IO.Path]::GetTempPath()) ("edisonruntime-" + [Guid]::NewGuid().ToString("N"))
$root = Join-Path $work "dotnet"
New-Item -ItemType Directory -Force $root | Out-Null
try {
    $i = 0
    foreach ($part in $parts) {
        $zip = Join-Path $work ("part$i.zip")
        Write-Host "Downloading $($part.url)"
        Invoke-WebRequest -Uri $part.url -OutFile $zip -UseBasicParsing
        $hash = (Get-FileHash -Algorithm SHA512 $zip).Hash
        if ($hash -ne $part.hash.ToUpperInvariant()) { throw "SHA512 mismatch for $($part.url)" }

        # Both zips carry LICENSE.txt / ThirdPartyNotices.txt; ExtractToDirectory cannot overwrite.
        $unpacked = Join-Path $work ("part$i")
        [IO.Compression.ZipFile]::ExtractToDirectory($zip, $unpacked)
        Copy-Item -Path (Join-Path $unpacked "*") -Destination $root -Recurse -Force
        $i++
    }

    $dotnetExe = Join-Path $root "dotnet.exe"
    if (Test-Path $dotnetExe) { Remove-Item $dotnetExe }

    $required = @(
        "host\fxr\$Version\hostfxr.dll",
        "shared\Microsoft.NETCore.App\$Version\Microsoft.NETCore.App.deps.json",
        "shared\Microsoft.WindowsDesktop.App\$Version\Microsoft.WindowsDesktop.App.deps.json"
    )
    foreach ($rel in $required) {
        if (-not (Test-Path (Join-Path $root $rel))) { throw "Unexpected runtime layout, missing $rel" }
    }

    [IO.File]::WriteAllText((Join-Path $root "EDISONRUNTIME.version"), $Version)

    $tmpOut = "$OutFile.tmp"
    if (Test-Path $tmpOut) { Remove-Item $tmpOut }
    [IO.Compression.ZipFile]::CreateFromDirectory($root, $tmpOut, [IO.Compression.CompressionLevel]::Optimal, $false)
    Move-Item -Force $tmpOut $OutFile

    $files = (Get-ChildItem -Recurse -File $root).Count
    $size = [Math]::Round((Get-Item $OutFile).Length / 1MB, 1)
    Write-Host "OK: $OutFile - runtime $Version, $files files, $size MB"
}
finally {
    Remove-Item -Recurse -Force $work -ErrorAction SilentlyContinue
}
