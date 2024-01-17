using System.Net.Http;
using System.Text.Json;
using ManagedCommon;
using Wox.Plugin;


namespace PowerToysRunCurrencyConverter
{
    public class Main : IPlugin
    {
        public static string PluginID => "EF1F634F20484459A3679B4DE7B07999";

        private string IconPath { get; set; }
        private PluginInitContext Context { get; set; }
        public string Name => "Currency Converter";

        public string Description => "This plugins converts currency";

        private Dictionary<string, (double, DateTime)> cache = new Dictionary<string, (double, DateTime)>();

        private List<Result> InvalidFormat()
        {
            return new List<Result>
            {
                new Result
                {
                    Title = "Invalid Format",
                    SubTitle = "$$ 100 inr to usd - please use this format",
                    IcoPath = IconPath,

                },
            };
        }

        private double? GetConversionRate(string fromCurrency, string toCurrency)
        {
            string key = $"{fromCurrency}-{toCurrency}";
            if (cache.ContainsKey(key) && cache[key].Item2 > DateTime.Now.AddHours(-1)) // cache for 1 hour
            {
                return cache[key].Item1;
            }
            else
            {
                string url = $"https://cdn.jsdelivr.net/gh/fawazahmed0/currency-api@1/latest/currencies/{fromCurrency}/{toCurrency}.json";
                try
                {
                    HttpClient client = new HttpClient();
                    var response = client.GetStringAsync(url).Result;
                    JsonDocument document = JsonDocument.Parse(response);
                    JsonElement root = document.RootElement;
                    double conversionRate = root.GetProperty(toCurrency).GetDouble();
                    cache[key] = (conversionRate, DateTime.Now);
                    return conversionRate;
                }
                catch (Exception ex)
                {
                    return null;
                }
            }
        }

        public List<Result> Query(Query query)
        {
            var strs = query.RawQuery.Split("$$");
            if (strs.Length != 2)
            {
                return InvalidFormat();
            }

            var parts = strs[1].Trim().Split(" ");
            if (parts.Length != 4)
            {
                return InvalidFormat();
            }

            double amountToConvert;
            if (!double.TryParse(parts[0], out amountToConvert))
            {
                return InvalidFormat();
            }

            string fromCurrency = parts[1];
            string toCurrency = parts[3];

            double? conversionRate = GetConversionRate(fromCurrency, toCurrency);

            if (conversionRate == null)
            {
                return new List<Result>
                {
                    new Result
                    {
                        Title = "Something went wrong.",
                        IcoPath = IconPath,
                    }
                };
            }

            double convertedAmount = Math.Round(amountToConvert * (double) conversionRate, 2);

            return new List<Result>
            {
                new Result
                {
                    Title = $"{convertedAmount} {toCurrency}",
                    SubTitle = $"Currency conversion from {fromCurrency} to {toCurrency}",
                    IcoPath = IconPath,
                }
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
    }
}