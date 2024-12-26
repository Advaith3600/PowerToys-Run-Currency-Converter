$ErrorActionPreference = "Stop"

$projectDirectory = "$PSScriptRoot\Community.PowerToys.Run.Plugin.CurrencyConverter"
$jsonContent = Get-Content -Path "$projectDirectory\plugin.json" -Raw | ConvertFrom-Json
$version = $jsonContent.version
$name = $jsonContent.name

foreach ($platform in "x64", "ARM64")
{
    if (Test-Path -Path "$PSScriptRoot\bin\$name-$version-$platform.zip")
    {
        Remove-Item -Path "$PSScriptRoot\bin\$name-$version-$platform.zip"
    }

    if (Test-Path -Path "$projectDirectory\bin")
    {
        Remove-Item -Path "$projectDirectory\bin\*" -Recurse
    }

    if (Test-Path -Path "$projectDirectory\obj")
    {
        Remove-Item -Path "$projectDirectory\obj\*" -Recurse
    }

    dotnet build $projectDirectory.sln -c Release /p:Platform=$platform --property WarningLevel=0

    Remove-Item -Path "$projectDirectory\bin\*" -Recurse -Include *.xml, *.pdb, PowerToys.*, Wox.*, Microsoft.*
    Rename-Item -Path "$projectDirectory\bin\$platform\Release" -NewName "$name"

    Compress-Archive -Path "$projectDirectory\bin\$platform\$name" -DestinationPath "$PSScriptRoot\bin\$name-$version-$platform.zip"
}

Set-Location -Path "exe"
./build.ps1
Set-Location -Path ".."