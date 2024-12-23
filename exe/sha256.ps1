$ErrorActionPreference = "Stop"

$zipHash = Get-FileHash -Path $zipX64 -Algorithm SHA256 | Select-Object -ExpandProperty Hash
$zipArmHash = Get-FileHash -Path $zipARM64 -Algorithm SHA256 | Select-Object -ExpandProperty Hash
$exeHash = Get-FileHash -Path $x64 -Algorithm SHA256 | Select-Object -ExpandProperty Hash
$exeARMHash = Get-FileHash -Path $arm64 -Algorithm SHA256 | Select-Object -ExpandProperty Hash

Write-Output "| File | SHA256 |"
Write-Output "| - | - |"
Write-Output "| x64-ZIP | $zipHash |"
Write-Output "| x64-EXE | $exeHash |"
Write-Output "| ARM64-ZIP | $zipArmHash |"
Write-Output "| ARM64-EXE | $exeARMHash |"