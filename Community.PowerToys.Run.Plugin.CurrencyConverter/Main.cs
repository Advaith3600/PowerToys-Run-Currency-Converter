using System.Globalization;
using System.Net.Http;
using System.Text.Json;
using ManagedCommon;
using Wox.Plugin;
using Microsoft.PowerToys.Settings.UI.Library;
using System.Text.RegularExpressions;

using Clipboard = System.Windows.Clipboard;

namespace Community.PowerToys.Run.Plugin.CurrencyConverter
{
    public class Main : IPlugin, ISettingProvider
    {
        public static string PluginID => "EF1F634F20484459A3679B4DE7B07999";

        private string IconPath { get; set; }
        private PluginInitContext Context { get; set; }
        public string Name => "Currency Converter";

        public string Description => "Currency Converter Plugin";

        private Dictionary<string, (double, DateTime)> ConversionCache = new Dictionary<string, (double, DateTime)>();
        private readonly HttpClient Client = new HttpClient();
        private readonly RegionInfo regionInfo = new RegionInfo(CultureInfo.CurrentCulture.Name);

        private int ConversionDirection;
        private string LocalCurrency, GlobalCurrency;

        public IEnumerable<PluginAdditionalOption> AdditionalOptions => new List<PluginAdditionalOption>()
        {
            new PluginAdditionalOption()
            {
                Key = "QuickConversionDirection",
                DisplayLabel = "Quick Convertion Direction",
                DisplayDescription = "Set in which direction you want to convert.",
                PluginOptionType = PluginAdditionalOption.AdditionalOptionType.Combobox,
                ComboBoxItems = new List<KeyValuePair<string, string>>
                {
                    new KeyValuePair<string, string>("From local to global", "0"),
                    new KeyValuePair<string, string>("From global to local", "1"),
                },
                ComboBoxValue = ConversionDirection,
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
        };

        public void UpdateSettings(PowerLauncherPluginSettings settings)
        {
            if (settings != null && settings.AdditionalOptions != null)
            {
                ConversionDirection = settings.AdditionalOptions.FirstOrDefault(x => x.Key == "QuickConversionDirection")?.ComboBoxValue ?? 0;
                string _LocalCurrency = settings.AdditionalOptions.FirstOrDefault(x => x.Key == "QuickConversionLocalCurrency").TextValue;
                LocalCurrency = _LocalCurrency == "" ? regionInfo.ISOCurrencySymbol : _LocalCurrency;

                string _GlobalCurrency = settings.AdditionalOptions.FirstOrDefault(x => x.Key == "QuickConversionGlobalCurrency").TextValue;
                GlobalCurrency = _GlobalCurrency == "" ? "USD" : _GlobalCurrency;
            }
        }

        private double? GetConversionRate(string fromCurrency, string toCurrency)
        {
            string key = $"{fromCurrency}-{toCurrency}";
            if (ConversionCache.ContainsKey(key) && ConversionCache[key].Item2 > DateTime.Now.AddHours(-1)) // cache for 1 hour
            {
                return ConversionCache[key].Item1;
            }
            else
            {
                string url = $"https://cdn.jsdelivr.net/gh/fawazahmed0/currency-api@1/latest/currencies/{fromCurrency}/{toCurrency}.json";
                try
                {
                    var response = Client.GetStringAsync(url).Result;
                    JsonDocument document = JsonDocument.Parse(response);
                    JsonElement root = document.RootElement;
                    double conversionRate = root.GetProperty(toCurrency).GetDouble();
                    ConversionCache[key] = (conversionRate, DateTime.Now);
                    return conversionRate;
                }
                catch (Exception ex)
                {
                    return null;
                }
            }
        }

        private Result GetConversion(double amountToConvert, string fromCurrency, string toCurrency)
        {
            double? conversionRate = GetConversionRate(fromCurrency.ToLower(), toCurrency.ToLower());
            fromCurrency = fromCurrency.ToUpper();
            toCurrency = toCurrency.ToUpper();

            if (conversionRate == null)
            {
                return new Result
                {
                    Title = $"Something went wrong while converting from {fromCurrency} to {toCurrency}",
                    SubTitle = "Please try again. Check your internet and the plugin settings if this persists.",
                    IcoPath = IconPath,
                };
            }

            double convertedAmount = Math.Round(amountToConvert * (double) conversionRate, 2);
            string formatted = convertedAmount.ToString("N", CultureInfo.CurrentCulture);

            return new Result
            {
                Title = $"{formatted} {toCurrency}",
                SubTitle = $"Currency conversion from {fromCurrency} to {toCurrency}",
                IcoPath = IconPath,
                Action = e =>
                {
                    Clipboard.SetText(convertedAmount.ToString());
                    return true;
                }
            };
        }

        public List<Result> Query(Query query)
        {
            double amountToConvert = 0;
            string fromCurrency = "";
            string toCurrency = "";

            var match = Regex.Match(query.Search.Trim(), @"([0-9.,]+) ?(\w*) ?(to)? ?(\w*)");

            if (! match.Success)
            {
                return new List<Result>();
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

                return new List<Result>
                {
                    GetConversion(amountToConvert, fromCurrency, toCurrency),
                    GetConversion(amountToConvert, toCurrency, fromCurrency)
                };
            } 
            else if (String.IsNullOrEmpty(toCurrency))
            {
                return new List<Result>
                {
                    GetConversion(amountToConvert, fromCurrency, ConversionDirection == 0 ? GlobalCurrency : LocalCurrency),
                    GetConversion(amountToConvert, fromCurrency, ConversionDirection == 0 ? LocalCurrency : GlobalCurrency)
                };
            }

            return new List<Result>
            {
                GetConversion(amountToConvert, fromCurrency, toCurrency)
            };
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
