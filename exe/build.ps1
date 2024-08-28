$iconPath = "icon.ico"
$description = "PowerToys Run plugin which will convert real and crypto currencies."

Write-Host "Building x64 executable..."
npx pkg . --targets node16-win-x64 --output CurrencyConverter-x64
npx rc CurrencyConverter-x64.exe --set-icon $iconPath --set-version-string "FileDescription" $description

Write-Host "Building ARM64 executable..."
npx pkg . --targets node16-win-arm64 --output CurrencyConverter-arm64
npx rc CurrencyConverter-arm64.exe --set-icon $iconPath --set-version-string "FileDescription" $description

Write-Host "Build completed!"
