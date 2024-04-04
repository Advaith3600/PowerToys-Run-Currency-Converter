using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Globalization;
using System.Text.RegularExpressions;

using Wox.Plugin;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Library;

using Clipboard = System.Windows.Clipboard;

namespace Community.PowerToys.Run.Plugin.CurrencyConverter
{
    public class Main : IPlugin, ISettingProvider
    {
        public static string PluginID => "EF1F634F20484459A3679B4DE7B07999";

        private string IconPath { get; set; }
        private PluginInitContext Context { get; set; }
        public string Name => "Currency Converter";

        public string Description => "Convert real and crypto currencies.";

        private Dictionary<string, (JsonElement, DateTime)> ConversionCache = [];
        private readonly HttpClient Client = new HttpClient();
        private readonly RegionInfo regionInfo = new RegionInfo(CultureInfo.CurrentCulture.Name);

        private int ConversionDirection;
        private string LocalCurrency, GlobalCurrency;
        private string[] ExtraCurrencies;

        public IEnumerable<PluginAdditionalOption> AdditionalOptions => new List<PluginAdditionalOption>()
        {
            new PluginAdditionalOption()
            {
                Key = "QuickConversionDirection",
                DisplayLabel = "Quick Convertion Direction",
                DisplayDescription = "Set in which direction you want to convert.",
                PluginOptionType = PluginAdditionalOption.AdditionalOptionType.Combobox,
                ComboBoxItems =
                [
                    new KeyValuePair<string, string>("From local to global", "0"),
                    new KeyValuePair<string, string>("From global to local", "1"),
                ],
                ComboBoxValue = 0,
            },
            new PluginAdditionalOption()
            {
                Key = "QuickConversionLocalCurrency",
                DisplayLabel = "Quick Convertion Local Currency",
                DisplayDescription = "Set your local currency.",
                PluginOptionType = PluginAdditionalOption.AdditionalOptionType.Textbox,
                TextValue = regionInfo.ISOCurrencySymbol,
            },
            new PluginAdditionalOption()
            {
                Key = "QuickConversionGlobalCurrency",
                DisplayLabel = "Quick Convertion Global Currency",
                DisplayDescription = "Set your global currency.",
                PluginOptionType = PluginAdditionalOption.AdditionalOptionType.Textbox,
                TextValue = "USD",
            },
            new PluginAdditionalOption()
            {
                Key = "QuickConversionExtraCurrencies",
                DisplayLabel = "Extra currencies for quick conversion",
                DisplayDescription = "Add currencies comma separated. eg: USD, EUR, BTC",
                PluginOptionType = PluginAdditionalOption.AdditionalOptionType.Textbox,
                TextValue = "",
            },
        };

        public void UpdateSettings(PowerLauncherPluginSettings settings)
        {
            if (settings != null && settings.AdditionalOptions != null)
            {
                ConversionDirection = settings.AdditionalOptions.FirstOrDefault(x => x.Key == "QuickConversionDirection")?.ComboBoxValue ?? 0;
                string _LocalCurrency = settings.AdditionalOptions.FirstOrDefault(x => x.Key == "QuickConversionLocalCurrency").TextValue;
                LocalCurrency = _LocalCurrency == "" ? regionInfo.ISOCurrencySymbol : _LocalCurrency;

                GlobalCurrency = settings.AdditionalOptions.FirstOrDefault(x => x.Key == "QuickConversionGlobalCurrency").TextValue;

                ExtraCurrencies = settings.AdditionalOptions
                    .FirstOrDefault(x => x.Key == "QuickConversionExtraCurrencies")
                    .TextValue.Split(',')
                    .Select(x => x.Trim())
                    .Where(x => !string.IsNullOrEmpty(x))
                    .ToArray();
            }
        }

        private double GetConversionRate(string fromCurrency, string toCurrency)
        {
            if (ConversionCache.ContainsKey(fromCurrency) && ConversionCache[fromCurrency].Item2 > DateTime.Now.AddHours(-1)) // cache for 1 hour
            {
                try
                {
                    return ConversionCache[fromCurrency].Item1.GetProperty(toCurrency).GetDouble();
                }
                catch (KeyNotFoundException)
                {
                    throw new Exception($"{toCurrency.ToUpper()} is not a valid currency");
                }
            }
            else
            {
                string url = $"https://cdn.jsdelivr.net/npm/@fawazahmed0/currency-api@latest/v1/currencies/{fromCurrency}.min.json";
                try
                {
                    var response = Client.GetAsync(url).Result;
                    if (!response.IsSuccessStatusCode)
                    {
                        if (response.StatusCode == HttpStatusCode.NotFound)
                        {
                            throw new Exception($"{fromCurrency.ToUpper()} is not a valid currency");
                        }
                        else
                        {
                            throw new Exception("Something went wrong while fetching the conversion rate");
                        }
                    }

                    var content = response.Content.ReadAsStringAsync().Result;
                    JsonElement element = JsonDocument.Parse(content).RootElement.GetProperty(fromCurrency);
                    double conversionRate = element.GetProperty(toCurrency).GetDouble();
                    ConversionCache[fromCurrency] = (element, DateTime.Now);
                    return conversionRate;
                }
                catch (KeyNotFoundException)
                {
                    throw new Exception($"{toCurrency.ToUpper()} is not a valid currency");
                }
            }
        }

        private Result? GetConversion(double amountToConvert, string fromCurrency, string toCurrency)
        {
            fromCurrency = fromCurrency.ToLower();
            toCurrency = toCurrency.ToLower();

            if (fromCurrency == toCurrency || fromCurrency == "" || toCurrency == "")
            {
                return null;
            }

            double conversionRate = 0;
            try
            {
                conversionRate = GetConversionRate(fromCurrency, toCurrency);
            }
            catch (Exception e)
            {
                return new Result
                {
                    Title = e.Message,
                    SubTitle = "Press enter to open the currencies list",
                    IcoPath = IconPath,
                    Action = e =>
                    {
                        string url = "https://cdn.jsdelivr.net/npm/@fawazahmed0/currency-api@latest/v1/currencies.json";
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true });
                        return true;
                    }
                };
            }

            double convertedAmount = Math.Round(amountToConvert * conversionRate, 2);
            string formatted = convertedAmount.ToString("N", CultureInfo.CurrentCulture);

            return new Result
            {
                Title = $"{formatted} {toCurrency.ToUpper()}",
                SubTitle = $"Currency conversion from {fromCurrency.ToUpper()} to {toCurrency.ToUpper()}",
                IcoPath = IconPath,
                Action = e =>
                {
                    Clipboard.SetText(convertedAmount.ToString());
                    return true;
                }
            };
        }

        private List<Result?> ParseQuery(string search)
        {
            double amountToConvert = 0;
            string fromCurrency = "";
            string toCurrency = "";

            var match = Regex.Match(search.Trim(), @"([0-9.,]+) ?(\w*) ?(to)? ?(\w*)");

            if (! match.Success)
            {
                return [];
            }

            if (double.TryParse(match.Groups[1].Value, out amountToConvert))
            {
                fromCurrency = match.Groups[2].Value;

                if (!string.IsNullOrEmpty(match.Groups[3].Value) && match.Groups[3].Value.ToLower() == "to")
                {
                    toCurrency = match.Groups[4].Value;
                }
            }

            if (String.IsNullOrEmpty(fromCurrency)) 
            {
                fromCurrency = ConversionDirection == 0 ? LocalCurrency : GlobalCurrency;
                toCurrency = ConversionDirection == 0 ? GlobalCurrency : LocalCurrency;

                List<Result?> results = [GetConversion(amountToConvert, fromCurrency, toCurrency)];
                foreach (string currency in ExtraCurrencies)
                {
                    results.Add(GetConversion(amountToConvert, fromCurrency, currency));
                }

                results.Add(GetConversion(amountToConvert, toCurrency, fromCurrency));
                foreach (string currency in ExtraCurrencies)
                {
                    results.Add(GetConversion(amountToConvert, toCurrency, currency));
                }

                return results;
            }
            else if (String.IsNullOrEmpty(toCurrency))
            {
                List<Result?> results =
                [
                    GetConversion(amountToConvert, fromCurrency, ConversionDirection == 0 ? GlobalCurrency : LocalCurrency),
                    GetConversion(amountToConvert, fromCurrency, ConversionDirection == 0 ? LocalCurrency : GlobalCurrency)
                ];

                foreach (string currency in ExtraCurrencies)
                {
                    results.Add(GetConversion(amountToConvert, fromCurrency, currency));
                }

                return results;
            }

            return
            [
                GetConversion(amountToConvert, fromCurrency, toCurrency)
            ];
        }

        public List<Result> Query(Query query)
        {
            return ParseQuery(query.Search).Where(x => x != null).ToList();
        }

        public void Init(PluginInitContext context)
        {
            Context = context;
            Context.API.ThemeChanged += OnThemeChanged;
            UpdateIconPath(Context.API.GetCurrentTheme());
        }

        private void UpdateIconPath(Theme theme)
        {
            if (theme == Theme.Light || theme == Theme.HighContrastWhite)
            {
                IconPath = "images/icon-black.png";
            }
            else
            {
                IconPath = "images/icon-white.png";
            }
        }

        private void OnThemeChanged(Theme currentTheme, Theme newTheme)
        {
            UpdateIconPath(newTheme);
        }

        public System.Windows.Controls.Control CreateSettingPanel()
        {
            throw new NotImplementedException();
        }
    }
}
