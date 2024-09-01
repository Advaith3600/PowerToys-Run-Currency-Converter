$ErrorActionPreference = "Stop"

$projectDirectory = "$PSScriptRoot\Community.PowerToys.Run.Plugin.CurrencyConverter"
$jsonContent = Get-Content -Path "$projectDirectory\plugin.json" -Raw | ConvertFrom-Json
$version = $jsonContent.version

foreach ($platform in "x64", "ARM64")
{
    if (Test-Path -Path "$PSScriptRoot\bin\CurrencyConverter-$version-$platform.zip")
    {
        Remove-Item -Path "$PSScriptRoot\bin\CurrencyConverter-$version-$platform.zip"
    }

    if (Test-Path -Path "$projectDirectory\bin")
    {
        Remove-Item -Path "$projectDirectory\bin\*" -Recurse
    }

    if (Test-Path -Path "$projectDirectory\obj")
    {
        Remove-Item -Path "$projectDirectory\obj\*" -Recurse
    }

    dotnet build $projectDirectory.sln -c Release /p:Platform=$platform 

    Remove-Item -Path "$projectDirectory\bin\*" -Recurse -Include *.xml, *.pdb, PowerToys.*, Wox.*
    Rename-Item -Path "$projectDirectory\bin\$platform\Release" -NewName "CurrencyConverter"

    Compress-Archive -Path "$projectDirectory\bin\$platform\CurrencyConverter" -DestinationPath "$PSScriptRoot\bin\CurrencyConverter-$version-$platform.zip"
}

Set-Location -Path "exe"
./build.ps1