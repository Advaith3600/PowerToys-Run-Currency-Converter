using Community.PowerToys.Run.Plugin.Update;
using Microsoft.PowerToys.Settings.UI.Library;
using System.Globalization;

namespace Community.PowerToys.Run.Plugin.CurrencyConverter
{
    public class Settings
    {
        public PluginUpdateSettings Update { get; set; } = new PluginUpdateSettings { ResultScore = 100 };

        protected internal bool ShowWarningsInGlobal { get; set; }
        protected internal int OutputStyle { get; set; }
        protected internal int DecimalSeparator { get; set; }
        protected internal int ConversionDirection { get; set; }
        protected internal string LocalCurrency { get; set; }
        protected internal string[] Currencies { get; set; }

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
                    Value = false,
                },
                new()
                {
                    Key = nameof(DecimalSeparator),
                    DisplayLabel = "Decimal format separator",
                    DisplayDescription = "Change between dots and commas for decimal separation",
                    PluginOptionType = PluginAdditionalOption.AdditionalOptionType.Combobox,
                    ComboBoxItems =
                        [
                            new KeyValuePair<string, string>("Use system default", "0"),
                            new KeyValuePair<string, string>("Always use dots for decimals", "1"),
                            new KeyValuePair<string, string>("Always use commas for decimals", "2"),
                        ],
                    ComboBoxValue = 0,
                },
                new()
                {
                    Key = nameof(OutputStyle),
                    DisplayLabel = "Conversion Output Style",
                    DisplayDescription = "Full Text: 2 USD = 1.86 EUR, Short Text: 1.86 EUR",
                    PluginOptionType = PluginAdditionalOption.AdditionalOptionType.Combobox,
                    ComboBoxItems =
                        [
                            new KeyValuePair<string, string>("Short Text", "0"),
                            new KeyValuePair<string, string>("Full Text", "1"),
                        ],
                    ComboBoxValue = 1,
                },
                new()
                {
                    Key = nameof(ConversionDirection),
                    DisplayLabel = "Quick Conversion Direction",
                    DisplayDescription = "Set the sort order for the quick conversion output",
                    PluginOptionType = PluginAdditionalOption.AdditionalOptionType.Combobox,
                    ComboBoxItems =
                        [
                            new KeyValuePair<string, string>("Local currency to other currencies", "0"),
                            new KeyValuePair<string, string>("Other currencies to local currency", "1"),
                        ],
                    ComboBoxValue = 0,
                },
                new()
                {
                    Key = nameof(LocalCurrency),
                    DisplayLabel = "Quick Conversion Local Currency",
                    DisplayDescription = "Set your local currency for quick conversion",
                    PluginOptionType = PluginAdditionalOption.AdditionalOptionType.Textbox,
                    TextValue = (new RegionInfo(CultureInfo.CurrentCulture.Name)).ISOCurrencySymbol,
                },
                new()
                {
                    Key = nameof(Currencies),
                    DisplayLabel = "Quick Conversion Currencies",
                    DisplayDescription = "Add currencies comma separated. eg: USD, EUR, BTC",
                    PluginOptionType = PluginAdditionalOption.AdditionalOptionType.Textbox,
                    TextValue = "USD",
                }
            ]);

            return options;
        }

        protected internal void SetAdditionalOptions(IEnumerable<PluginAdditionalOption> additionalOptions)
        {
            if (additionalOptions == null) return;

            ShowWarningsInGlobal = additionalOptions.FirstOrDefault(x => x.Key == nameof(ShowWarningsInGlobal))?.Value ?? false;
            DecimalSeparator = additionalOptions.FirstOrDefault(x => x.Key == nameof(DecimalSeparator))?.ComboBoxValue ?? 0;
            ConversionDirection = additionalOptions.FirstOrDefault(x => x.Key == nameof(ConversionDirection))?.ComboBoxValue ?? 0;
            OutputStyle = additionalOptions.FirstOrDefault(x => x.Key == nameof(OutputStyle))?.ComboBoxValue ?? 0;

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
