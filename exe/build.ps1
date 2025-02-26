$ErrorActionPreference = "Stop"

$outputDir = "..\bin"
$zipX64 = "$outputDir\$name-$version-x64.zip"
$zipARM64 = "$outputDir\$name-$version-ARM64.zip"
$tempDirX64 = "$outputDir\temp\$name-$version-x64"
$tempDirARM64 = "$outputDir\temp\$name-$version-ARM64"

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

    & makensis /Dver=$version /Ddirect=$targetDir /Dplatform=$platform /Dname=$name .\CurrencyConverter.nsi
}

# Extract x64 zip
Extract-Zip -zipPath $zipX64 -extractPath $tempDirX64

# Extract ARM64 zip
Extract-Zip -zipPath $zipARM64 -extractPath $tempDirARM64

# Build x64 installer
Build-Installer -platform "x64" -targetDir "$tempDirX64\$name"

# Build ARM64 installer
Build-Installer -platform "ARM64" -targetDir "$tempDirARM64\$name"

# Check if the installers were created successfully
$x64 = "$outputDir\$name-$version-x64.exe"
$arm64 = "$outputDir\$name-$version-ARM64.exe"

$x64Exists = Test-Path $x64
$arm64Exists = Test-Path $arm64

if ($x64Exists -and $arm64Exists) {
    Write-Output "Installers created successfully."
} else {
    Write-Output "Failed to create installers."
}

# Clean up temporary directories
Remove-Item -Recurse -Force $tempDirX64
Remove-Item -Recurse -Force $tempDirARM64

./sha256.ps1