$response = Invoke-RestMethod -Uri https://api.github.com/repos/Advaith3600/PowerToys-Run-Currency-Converter/releases/latest

$ver = $response.tag_name.TrimStart('v')

$exe = $response.assets | Where-Object { $_.name -like "*x64.exe" } | Select-Object -ExpandProperty browser_download_url
$exeARM = $response.assets | Where-Object { $_.name -like "*ARM64.exe" } | Select-Object -ExpandProperty browser_download_url
$zip = $response.assets | Where-Object { $_.name -like "*x64.zip" } | Select-Object -ExpandProperty browser_download_url
$zipARM = $response.assets | Where-Object { $_.name -like "*ARM64.zip" } | Select-Object -ExpandProperty browser_download_url

if (-not (Test-Path .\temp)) {
 New-Item -Path .\temp -ItemType Directory
}

Invoke-WebRequest -Uri $exe -OutFile .\temp\x64.exe
Invoke-WebRequest -Uri $exeARM -OutFile .\temp\arm.exe
Invoke-WebRequest -Uri $zip -OutFile .\temp\x64.zip
Invoke-WebRequest -Uri $zipARM -OutFile .\temp\arm.zip

$exehash = Get-FileHash -Path .\temp\x64.exe -Algorithm SHA256 | Select-Object -ExpandProperty Hash
$exeARMhash = Get-FileHash -Path .\temp\arm.exe -Algorithm SHA256 | Select-Object -ExpandProperty Hash
$ziphash = Get-FileHash -Path .\temp\x64.zip -Algorithm SHA256 | Select-Object -ExpandProperty Hash
$zipARMhash = Get-FileHash -Path .\temp\arm.zip -Algorithm SHA256 | Select-Object -ExpandProperty Hash

$table = @"
| File | SHA256 |
| --- | --- |
| x64-ZIP | $ziphash |
| x64-EXE | $exehash |
| ARM64-ZIP | $zipARMhash |
| ARM64-EXE | $exeARMhash |
"@
echo $table
