<#
.SYNOPSIS
    Build and package Shadow Explorer.
.DESCRIPTION
    Publishes ShadowExplorer.exe and builds the Inno Setup installer.
    Version is defined in Installer/setup.iss (#define MyAppVersion).
.EXAMPLE
    .\build.ps1
#>

$ErrorActionPreference = "Stop"
$root = $PSScriptRoot

# Read version from setup.iss
$issContent = Get-Content "$root\Installer\setup.iss" -Raw
if ($issContent -match '#define MyAppVersion "([^"]+)"') {
    $version = $Matches[1]
} else {
    throw "Could not read version from setup.iss"
}

Write-Host "Building Shadow Explorer v$version" -ForegroundColor Cyan
Write-Host ""

# Publish main app
Write-Host "Publishing ShadowExplorer.exe..."
dotnet publish "$root\ShadowExplorer\ShadowExplorer.csproj" `
    -c Release -r win-x64 --self-contained `
    -p:PublishSingleFile=true `
    -p:IncludeAllContentForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -p:Version=$version `
    | Out-Null
Write-Host "  Done" -ForegroundColor Green

# Build installer
Write-Host "Building installer..."
$iscc = "C:\Program Files (x86)\Inno Setup 6\ISCC.exe"
if (-not (Test-Path $iscc)) { throw "Inno Setup not found at $iscc" }
& $iscc "$root\Installer\setup.iss" | Out-Null
Write-Host "  Done" -ForegroundColor Green

$installer = Get-Item "$root\dist\ShadowExplorer-Setup-$version.exe"
Write-Host ""
Write-Host "Output: $($installer.FullName) ($([math]::Round($installer.Length / 1MB)) MB)" -ForegroundColor Green
