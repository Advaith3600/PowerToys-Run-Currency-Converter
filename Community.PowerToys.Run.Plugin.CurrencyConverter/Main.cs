using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Globalization;
using System.Text.RegularExpressions;

using Wox.Plugin;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Library;

using Clipboard = System.Windows.Clipboard;
using System.Text;

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

        private int ConversionDirection, OutputStyle, OutputPrecision;
        private string LocalCurrency;
        private string[] Currencies;

        public IEnumerable<PluginAdditionalOption> AdditionalOptions => new List<PluginAdditionalOption>()
        {
            new PluginAdditionalOption()
            {
                Key = "ConversionOutputStyle",
                DisplayLabel = "Conversion Output Style",
                DisplayDescription = "Full Text: 2 USD = 1.86 EUR, Short Text: 1.86 EUR",
                PluginOptionType = PluginAdditionalOption.AdditionalOptionType.Combobox,
                ComboBoxItems =
                [
                    new KeyValuePair<string, string>("Short Text", "0"),
                    new KeyValuePair<string, string>("Full Text", "1"),
                ],
                ComboBoxValue = 0,
            },
            new PluginAdditionalOption()
            {
                Key = "ConversionOutputPrecision",
                DisplayLabel = "Conversion Output Precision",
                DisplayDescription = "Control the amount of decimal points shown on the output",
                PluginOptionType = PluginAdditionalOption.AdditionalOptionType.Numberbox,
                NumberValue = 2,
            },
            new PluginAdditionalOption()
            {
                Key = "QuickConversionDirection",
                DisplayLabel = "Quick Conversion Direction",
                DisplayDescription = "Set in which direction you want to convert first.",
                PluginOptionType = PluginAdditionalOption.AdditionalOptionType.Combobox,
                ComboBoxItems =
                [
                    new KeyValuePair<string, string>("Local currency to other currencies", "0"),
                    new KeyValuePair<string, string>("Other currencies to local currency", "1"),
                ],
                ComboBoxValue = 0,
            },
            new PluginAdditionalOption()
            {
                Key = "QuickConversionLocalCurrency",
                DisplayLabel = "Quick Conversion Local Currency",
                DisplayDescription = "Set your local currency.",
                PluginOptionType = PluginAdditionalOption.AdditionalOptionType.Textbox,
                TextValue = regionInfo.ISOCurrencySymbol,
            },
            new PluginAdditionalOption()
            {
                Key = "QuickConversionCurrencies",
                DisplayLabel = "Currencies for quick conversion",
                DisplayDescription = "Add currencies comma separated. eg: USD, EUR, BTC",
                PluginOptionType = PluginAdditionalOption.AdditionalOptionType.Textbox,
                TextValue = "USD",
            },
        };

        public void UpdateSettings(PowerLauncherPluginSettings settings)
        {
            if (settings != null && settings.AdditionalOptions != null)
            {
                ConversionDirection = settings.AdditionalOptions.FirstOrDefault(x => x.Key == "QuickConversionDirection")?.ComboBoxValue ?? 0;

                OutputStyle = settings.AdditionalOptions.FirstOrDefault(x => x.Key == "ConversionOutputStyle")?.ComboBoxValue ?? 0;
                OutputPrecision = (int) (settings.AdditionalOptions.FirstOrDefault(x => x.Key == "ConversionOutputPrecision")?.NumberValue ?? 2);

                string _LocalCurrency = settings.AdditionalOptions.FirstOrDefault(x => x.Key == "QuickConversionLocalCurrency")?.TextValue ?? "";
                LocalCurrency = _LocalCurrency == "" ? regionInfo.ISOCurrencySymbol : _LocalCurrency;

                Currencies = (settings.AdditionalOptions.FirstOrDefault(x => x.Key == "QuickConversionCurrencies")?.TextValue ?? "")
                    .Split(',')
                    .Select(x => x.Trim())
                    .Where(x => !string.IsNullOrEmpty(x))
                    .ToArray();
            }
        }

        private double GetConversionRate(string fromCurrency, string toCurrency)
        {
            if (ConversionCache.ContainsKey(fromCurrency) && ConversionCache[fromCurrency].Item2 > DateTime.Now.AddHours(-3)) // cache for 3 hour
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

            int precision = OutputPrecision;
            double rawConvertedAmount = Math.Abs(amountToConvert * conversionRate);
            double convertedAmount = Math.Round(rawConvertedAmount, precision);

            if (rawConvertedAmount < 1)
            {
                string rawStr = rawConvertedAmount.ToString("F10", CultureInfo.InvariantCulture);
                int decimalPointIndex = rawStr.IndexOf('.');
                if (decimalPointIndex != -1)
                {
                    int numberOfZeros = rawStr.Substring(decimalPointIndex + 1).TakeWhile(c => c == '0').Count();
                    precision = numberOfZeros + OutputPrecision;
                    convertedAmount = Math.Round(rawConvertedAmount, precision);
                }
            }

            string fromFormatted = amountToConvert.ToString("N", CultureInfo.CurrentCulture);
            string toFormatted = convertedAmount.ToString($"N{precision}", CultureInfo.CurrentCulture);

            return new Result
            {
                Title = OutputStyle == 0 ? $"{toFormatted} {toCurrency.ToUpper()}" : $"{fromFormatted} {fromCurrency.ToUpper()} = {toFormatted} {toCurrency.ToUpper()}",
                SubTitle = $"Currency conversion from {fromCurrency.ToUpper()} to {toCurrency.ToUpper()}",
                QueryTextDisplay = $"{toFormatted} {toCurrency.ToUpper()}",
                IcoPath = IconPath,
                Action = e =>
                {
                    Clipboard.SetText(toFormatted);
                    return true;
                }
            };
        }

        public bool HasPrecedence(char op1, char op2)
        {
            if (op2 == '(' || op2 == ')')
                return false;
            if ((op1 == '*' || op1 == '/') && (op2 == '+' || op2 == '-'))
                return false;
            else
                return true;
        }

        public double ApplyOp(char op, double b, double a)
        {
            switch (op)
            {
                case '+':
                    return a + b;
                case '-':
                    return a - b;
                case '*':
                    return a * b;
                case '/':
                    if (b == 0)
                        throw new NotSupportedException("Cannot divide by zero");
                    return a / b;
            }
            return 0;
        }

        public double Evaluate(string expression)
        {
            Stack<double> values = new Stack<double>();
            Stack<char> ops = new Stack<char>();

            for (int i = 0; i < expression.Length; i++)
            {
                if (expression[i] == ' ')
                    continue;

                if (expression[i] >= '0' && expression[i] <= '9')
                {
                    StringBuilder sbuf = new StringBuilder();
                    while (i < expression.Length && ((expression[i] >= '0' && expression[i] <= '9') || expression[i] == '.'))
                        sbuf.Append(expression[i++]);
                    values.Push(double.Parse(sbuf.ToString()));
                    i--;
                }

                else if (expression[i] == '(')
                    ops.Push(expression[i]);

                else if (expression[i] == ')')
                {
                    while (ops.Count > 0 && ops.Peek() != '(')
                        values.Push(ApplyOp(ops.Pop(), values.Pop(), values.Pop()));
                    ops.Pop();
                }

                else if (expression[i] == '+' || expression[i] == '-' || expression[i] == '*' || expression[i] == '/')
                {
                    while (ops.Count > 0 && HasPrecedence(expression[i], ops.Peek()))
                        values.Push(ApplyOp(ops.Pop(), values.Pop(), values.Pop()));
                    ops.Push(expression[i]);
                }
            }

            while (ops.Count > 0)
                values.Push(ApplyOp(ops.Pop(), values.Pop(), values.Pop()));

            return values.Pop();
        }

        private List<Result?> ParseQuery(string search)
        {
            var match = Regex.Match(search.Trim(), @"^\s*(?:(?:(?<amount>[0-9.,+\-*/ \(\)]+)\s*(?<from>\w*))|(?:(?<from>[a-zA-Z]*)\s*(?<amount>[0-9.,+\-*/ \(\)]+)))\s*(?:to)?\s*(?<to>\w*)\s*$");

            if (! match.Success)
            {
                return [];
            }

            double amountToConvert;
            try
            {
                amountToConvert = Evaluate(match.Groups["amount"].Value.Replace(",", ""));
            }
            catch (Exception)
            {
                return [
                    new Result
                    {
                        Title = "Invalid expression provided",
                        SubTitle = "Please check your mathematical expression",
                        IcoPath = IconPath,
                    }
                ];
            }

            string fromCurrency = match.Groups["from"].Value;
            string toCurrency = "";

            if (!string.IsNullOrEmpty(match.Groups["to"].Value))
            {
                toCurrency = match.Groups["to"].Value;
            }

            if (string.IsNullOrEmpty(fromCurrency)) 
            {
                List<Result?> results = [];
                
                foreach (string currency in Currencies)
                {
                    if (ConversionDirection == 0)
                    {
                        results.Add(GetConversion(amountToConvert, LocalCurrency, currency));
                    }
                    else
                    {
                        results.Add(GetConversion(amountToConvert, currency, LocalCurrency));
                    }
                }

                foreach (string currency in Currencies)
                {
                    if (ConversionDirection == 0)
                    {
                        results.Add(GetConversion(amountToConvert, currency, LocalCurrency));
                    }
                    else
                    {
                        results.Add(GetConversion(amountToConvert, LocalCurrency, currency));
                    }
                }

                return results;
            }
            else if (string.IsNullOrEmpty(toCurrency))
            {
                List<Result?> results = [];

                if (ConversionDirection == 0)
                {
                    results.Add(GetConversion(amountToConvert, fromCurrency, LocalCurrency));
                }

                foreach (string currency in Currencies)
                {
                    results.Add(GetConversion(amountToConvert, fromCurrency, currency));
                }

                if (ConversionDirection == 1)
                {
                    results.Add(GetConversion(amountToConvert, fromCurrency, LocalCurrency));
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
