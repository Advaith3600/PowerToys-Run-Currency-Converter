$jsonFilePath = "$PSScriptRoot\..\Community.PowerToys.Run.Plugin.CurrencyConverter\plugin.json"
$jsonContent = Get-Content -Path $jsonFilePath -Raw | ConvertFrom-Json
$version = $jsonContent.version

$zipX64 = "..\bin\CurrencyConverter-$version-x64.zip"
$zipARM64 = "..\bin\CurrencyConverter-$version-ARM64.zip"
$tempDirX64 = "..\bin\temp\CurrencyConverter-$version-x64"
$tempDirARM64 = "..\bin\temp\CurrencyConverter-$version-ARM64"
$outputDir = "..\bin"

# Function to extract zip files
function Extract-Zip {
    param (
        [string]$zipPath,
        [string]$extractPath
    )
    if (Test-Path $extractPath) {
        Remove-Item -Recurse -Force $extractPath
    }
    Expand-Archive -Path $zipPath -DestinationPath $extractPath
}

# Function to build the installer
function Build-Installer {
    param (
        [string]$platform,
        [string]$targetDir
    )

    & makensis /Dver=$version /Ddirect=$targetDir /Dplatform=$platform .\CurrencyConverter.nsi
}

# Extract x64 zip
Extract-Zip -zipPath $zipX64 -extractPath $tempDirX64

# Extract ARM64 zip
Extract-Zip -zipPath $zipARM64 -extractPath $tempDirARM64

# Build x64 installer
Build-Installer -platform "x64" -targetDir "$tempDirX64\CurrencyConverter"

# Build ARM64 installer
Build-Installer -platform "ARM64" -targetDir "$tempDirARM64\CurrencyConverter"

# Check if the installers were created successfully
$x64Exists = Test-Path "$outputDir\CurrencyConverter-$version-x64.exe"
$arm64Exists = Test-Path "$outputDir\CurrencyConverter-$version-ARM64.exe"

if ($x64Exists -and $arm64Exists) {
    Write-Output "Installers created successfully."
} else {
    Write-Output "Failed to create installers."
}

# Clean up temporary directories
Remove-Item -Recurse -Force $tempDirX64
Remove-Item -Recurse -Force $tempDirARM64
