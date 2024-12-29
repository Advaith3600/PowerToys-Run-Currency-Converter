using Community.PowerToys.Run.Plugin.Update;
using Microsoft.PowerToys.Settings.UI.Library;
using System.Globalization;
using Wox.Plugin.Logger;

namespace Community.PowerToys.Run.Plugin.CurrencyConverter
{
    public class Settings
    {
        public PluginUpdateSettings Update { get; set; } = new PluginUpdateSettings { ResultScore = 100 };

        public bool ShowWarningsInGlobal { get; set; } = false;
        public int OutputStyle { get; set; } = 1;
        public int DecimalSeparator { get; set; } = 0;
        public int ConversionDirection { get; set; } = 0;
        public string LocalCurrency { get; set; } = "";
        public string[] Currencies { get; set; } = [];
        public double ConversionCacheDuration { get; set; } = 3;
        public int ConversionAPI { get; set; } = (int)ConverterSettingsEnum.Default;
        public string ConversionAPIKey { get; set; } = "";

        protected internal IEnumerable<PluginAdditionalOption> GetAdditionalOptions()
        {
            List<PluginAdditionalOption> options = Update.GetAdditionalOptions().ToList();

            options.AddRange([
                new()
                {
                    Key = nameof(ShowWarningsInGlobal),
                    DisplayLabel = "Show warnings in global results",
                    DisplayDescription = "Warnings from the plugin are suppressed when the \"Include in global result\" is checked",
                    PluginOptionType = PluginAdditionalOption.AdditionalOptionType.Checkbox,
                    Value = ShowWarningsInGlobal,
                },
                new()
                {
                    Key = nameof(DecimalSeparator),
                    DisplayLabel = "Decimal format separator",
                    DisplayDescription = "Change between dots and commas for decimal separation",
                    PluginOptionType = PluginAdditionalOption.AdditionalOptionType.Combobox,
                    ComboBoxItems =
                        [
                            new("Use system default", "0"),
                            new("Always use dots for decimals", "1"),
                            new("Always use commas for decimals", "2"),
                        ],
                    ComboBoxValue = DecimalSeparator,
                },
                new()
                {
                    Key = nameof(OutputStyle),
                    DisplayLabel = "Conversion Output Style",
                    DisplayDescription = "Full Text: 2 USD = 1.86 EUR, Short Text: 1.86 EUR",
                    PluginOptionType = PluginAdditionalOption.AdditionalOptionType.Combobox,
                    ComboBoxItems =
                        [
                            new("Short Text", "0"),
                            new("Full Text", "1"),
                        ],
                    ComboBoxValue = OutputStyle,
                },
                new()
                {
                    Key = nameof(ConversionDirection),
                    DisplayLabel = "Quick Conversion Direction",
                    DisplayDescription = "Set the sort order for the quick conversion output",
                    PluginOptionType = PluginAdditionalOption.AdditionalOptionType.Combobox,
                    ComboBoxItems =
                        [
                            new("Local currency to other currencies", "0"),
                            new("Other currencies to local currency", "1"),
                        ],
                    ComboBoxValue = ConversionDirection,
                },
                new()
                {
                    Key = nameof(LocalCurrency),
                    DisplayLabel = "Quick Conversion Local Currency",
                    DisplayDescription = "Set your local currency for quick conversion",
                    PluginOptionType = PluginAdditionalOption.AdditionalOptionType.Textbox,
                    TextValue = LocalCurrency,
                },
                new()
                {
                    Key = nameof(Currencies),
                    DisplayLabel = "Quick Conversion Currencies",
                    DisplayDescription = "Add currencies comma separated. eg: USD, EUR, BTC",
                    PluginOptionType = PluginAdditionalOption.AdditionalOptionType.Textbox,
                    TextValue = string.Join(", ", Currencies ?? [""]),
                },
                new()
                {
                    Key = nameof(ConversionCacheDuration),
                    DisplayLabel = "Conversion Cache duration",
                    DisplayDescription = "Duration should be mentioned in hours. Min: 0.5, Max: 24",
                    PluginOptionType = PluginAdditionalOption.AdditionalOptionType.Numberbox,
                    NumberValue = ConversionCacheDuration,
                    NumberBoxMin = 0.5,
                    NumberBoxMax = 24
                },
                new()
                {
                    Key = nameof(ConversionAPI),
                    DisplayLabel = "Conversion API",
                    DisplayDescription = "Use 'Default' if you are not sure which one to use. Be sure to read the plugin's readme file on GitHub if you plan to change this option to avoid any side effects.",
                    PluginOptionType = PluginAdditionalOption.AdditionalOptionType.Combobox,
                    ComboBoxItems =
                        [
                            new("Default", ((int)ConverterSettingsEnum.Default).ToString()),
                            new("ExchangeRateAPI", ((int)ConverterSettingsEnum.ExchangeRateAPI).ToString()),
                            new("CurrencyAPI", ((int)ConverterSettingsEnum.CurrencyAPI).ToString()),
                        ],
                    ComboBoxValue = ConversionAPI,
                },
                new()
                {
                    Key = nameof(ConversionAPIKey),
                    DisplayLabel = "Conversion API Key",
                    DisplayDescription = "(Optional) Provide the API key for the service if necessary",
                    PluginOptionType = PluginAdditionalOption.AdditionalOptionType.Textbox,
                    TextValue = ConversionAPIKey,
                }
            ]);

            return options;
        }

        protected internal void SetAdditionalOptions(IEnumerable<PluginAdditionalOption> additionalOptions)
        {
            if (additionalOptions == null) return;
            Log.Info("Default: " + ConverterSettingsEnum.Default.ToString(), GetType());

            ShowWarningsInGlobal = additionalOptions.FirstOrDefault(x => x.Key == nameof(ShowWarningsInGlobal))?.Value ?? false;
            DecimalSeparator = additionalOptions.FirstOrDefault(x => x.Key == nameof(DecimalSeparator))?.ComboBoxValue ?? 0;
            ConversionDirection = additionalOptions.FirstOrDefault(x => x.Key == nameof(ConversionDirection))?.ComboBoxValue ?? 0;
            OutputStyle = additionalOptions.FirstOrDefault(x => x.Key == nameof(OutputStyle))?.ComboBoxValue ?? 0;
            ConversionAPI = additionalOptions.FirstOrDefault(x => x.Key == nameof(ConversionAPI))?.ComboBoxValue ?? (int)ConverterSettingsEnum.Default;
            ConversionAPIKey = additionalOptions.FirstOrDefault(x => x.Key == nameof(ConversionAPIKey))?.TextValue ?? "";

            ConversionCacheDuration = Math.Max(Math.Min(additionalOptions
                .FirstOrDefault(x => x.Key == nameof(ConversionCacheDuration))
                ?.NumberValue ?? 3, 24), 0.5);

            RegionInfo regionInfo = new RegionInfo(CultureInfo.CurrentCulture.Name);
            string localCurrency = additionalOptions.FirstOrDefault(x => x.Key == nameof(LocalCurrency))?.TextValue ?? "";
            LocalCurrency = string.IsNullOrEmpty(localCurrency) ? regionInfo.ISOCurrencySymbol : localCurrency;

            Currencies = (additionalOptions.FirstOrDefault(x => x.Key == nameof(Currencies))?.TextValue ?? "")
                .Split(',')
                .Select(x => x.Trim())
                .Where(x => !string.IsNullOrEmpty(x))
                .ToArray();

            Update.SetAdditionalOptions(additionalOptions);
        }
    }
}
