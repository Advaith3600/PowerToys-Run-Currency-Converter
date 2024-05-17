# Currency Converter

PowerToys Run plugin which will convert real and crypto currencies.

![Screenshot](screenshots/screenshot1.png)

## Usage

```
$$ 100 inr to usd
```

### Changing / Removing prefix

You can change the `$$` prefix from the settings page. To use this plugin without any prefix just check the "Include in global result" checkbox. With that option checked, you can use this plugin without any prefix like 

```
1 eur to usd
```

![Screenshot](screenshots/screenshot2.png)

### Crypto and other currencies

This plugin also converters real currencies to crypto currencies and vice versa. Refer [here](https://cdn.jsdelivr.net/gh/fawazahmed0/currency-api@1/latest/currencies.json) for the full list of available conversions. 

Example Usage:

```
$$ 1 btc to usd
```

![Screenshot](screenshots/screenshot3.png)

### Quick Conversions

You can swiftly convert from your local currency to another currency simply by typing the number. The settings allow you to change both the local and all other currencies.

```
$$ 102.2
```

![Screenshot](screenshots/screenshot4.png)

### Output formatting and precision

The plugin supports two modes of output:

Short Text: The output will only contain the target currency.
Full Text: The output will contain both the source and target currencies.

You can adjust the precision value in the settings. This will determine the number of decimal points to be displayed. The plugin outputs values using dynamic precision. This means that if a value is less than 0, the number of non-zero decimals displayed will be exactly as specified in the settings.

![Screenshot](screenshots/screenshot5.png)

### Mathematical Calculations

You can input mathematical expressions, and the plugin will evaluate them using the BODMAS rule. The permitted operations are `+` (addition), `-` (subtraction), `*` (multiplication), and `/` (division). The use of brackets is also supported.

![Screenshot](screenshots/screenshot6.png)

## Installation

1. Download the latest release of the Currency Converter from the [releases page](https://github.com/advaith3600/powertoys-run-currency-converter/releases).
2. Extract the zip file's contents to your PowerToys modules directory (usually `%LOCALAPPDATA%\Microsoft\PowerToys\PowerToys Run\Plugins`).
3. Restart PowerToys.

## Conversion API

This plugin internally uses [Currency API](https://github.com/fawazahmed0/exchange-api) for the latest conversion rates. 
