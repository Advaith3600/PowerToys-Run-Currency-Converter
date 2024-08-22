using System.IO;
using System.Net;
using System.Text;
using System.Net.Http;
using System.Text.Json;
using System.Reflection;
using System.Windows.Input;
using System.Globalization;
using System.Text.RegularExpressions;

using Wox.Plugin;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Library;

using Clipboard = System.Windows.Clipboard;

namespace Community.PowerToys.Run.Plugin.CurrencyConverter
{
    public class Main : IPlugin, IContextMenu, ISettingProvider, IDisposable
    {
        public static string PluginID => "EF1F634F20484459A3679B4DE7B07999";

        private string IconPath { get; set; }
        private string WarningIconPath { get; set; }
        private PluginInitContext Context { get; set; }
        public string Name => "Currency Converter";

        public string Description => "Convert real and crypto currencies.";

        private bool Disposed { get; set; }

        private Dictionary<string, (JsonElement, DateTime)> ConversionCache = [];

        private bool ShowWarningsInGlobal;
        private int ConversionDirection, OutputStyle, OutputPrecision;
        private string LocalCurrency;
        private string[] Currencies;

        private string AliasFileLocation = "";

        public IEnumerable<PluginAdditionalOption> AdditionalOptions => new List<PluginAdditionalOption>()
        {
            new PluginAdditionalOption()
            {
                Key = "ShowWarningsInGlobal",
                DisplayLabel = "Show warnings in global results",
                DisplayDescription = "Warnings from the plugin are suppressed when the \"Include in global result\" is checked",
                PluginOptionType = PluginAdditionalOption.AdditionalOptionType.Checkbox,
                Value = false,
            },
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
                TextValue = (new RegionInfo(CultureInfo.CurrentCulture.Name)).ISOCurrencySymbol,
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
                ShowWarningsInGlobal = settings.AdditionalOptions.FirstOrDefault(x => x.Key == "ShowWarningsInGlobal")?.Value ?? false;

                ConversionDirection = settings.AdditionalOptions.FirstOrDefault(x => x.Key == "QuickConversionDirection")?.ComboBoxValue ?? 0;

                OutputStyle = settings.AdditionalOptions.FirstOrDefault(x => x.Key == "ConversionOutputStyle")?.ComboBoxValue ?? 0;
                OutputPrecision = (int)(settings.AdditionalOptions.FirstOrDefault(x => x.Key == "ConversionOutputPrecision")?.NumberValue ?? 2);

                RegionInfo regionInfo = new RegionInfo(CultureInfo.CurrentCulture.Name);
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
                    HttpClient Client = new HttpClient();
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

        private string GetCurrencyFromAlias(string currency)
        {
            try
            {
                if (!File.Exists(AliasFileLocation))
                {
                    return currency;
                }

                var jsonData = File.ReadAllText(AliasFileLocation);
                using (JsonDocument doc = JsonDocument.Parse(jsonData))
                {
                    if (doc.RootElement.TryGetProperty(currency, out JsonElement value))
                    {
                        return value.GetString();
                    }
                    else
                    {
                        return currency;
                    }
                }
            }
            catch (Exception)
            {
                return currency;
            }
        }

        private Result? GetConversion(bool isGlobal, double amountToConvert, string fromCurrency, string toCurrency)
        {
            fromCurrency = GetCurrencyFromAlias(fromCurrency.ToLower());
            toCurrency = GetCurrencyFromAlias(toCurrency.ToLower());

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
                const string link = "https://cdn.jsdelivr.net/npm/@fawazahmed0/currency-api@latest/v1/currencies.json";
                return isGlobal && !ShowWarningsInGlobal ? null : new Result
                {
                    Title = e.Message,
                    SubTitle = "Press enter or click to open the currencies list",
                    IcoPath = WarningIconPath,
                    ContextData = new Dictionary<string, string> { { "externalLink", link } },
                    Action = _ => PerformAction("externalLink", link)
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
            string toFormatted = (amountToConvert < 0 ? convertedAmount * -1 : convertedAmount).ToString($"N{precision}", CultureInfo.CurrentCulture);

            string compressedOutput = $"{toFormatted} {toCurrency.ToUpper()}";
            string expandedOutput = $"{fromFormatted} {fromCurrency.ToUpper()} = {toFormatted} {toCurrency.ToUpper()}";

            return new Result
            {
                Title = OutputStyle == 0 ? compressedOutput : expandedOutput,
                SubTitle = $"Currency conversion from {fromCurrency.ToUpper()} to {toCurrency.ToUpper()}",
                QueryTextDisplay = compressedOutput,
                IcoPath = IconPath,
                ContextData = new Dictionary<string, string> { { "copy", toFormatted } },
                ToolTipData = new ToolTipData(expandedOutput, "Click to copy the converted amount"),
                Action = _ => PerformAction("copy", toFormatted)
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
                    while (i < expression.Length && ((expression[i] >= '0' && expression[i] <= '9') || expression[i] == '.' || expression[i] == ','))
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

        private List<Result?> ParseQuery(string search, bool isGlobal)
        {
            var match = Regex.Match(search.Trim(), @"^\s*(?:(?:(?<amount>[0-9.,+\-*/ \(\)]+)\s*(?<from>\w*))|(?:(?<from>[a-zA-Z]*)\s*(?<amount>[0-9.,+\-*/ \(\)]+)))\s*(?:to)?\s*(?<to>\w*)\s*$");

            if (!match.Success)
            {
                return [];
            }

            double amountToConvert;
            try
            {
                CultureInfo culture = CultureInfo.CurrentCulture;
                amountToConvert = Evaluate(match.Groups["amount"].Value.Replace(culture.NumberFormat.NumberDecimalSeparator == "," ? "." : ",", ""));
            }
            catch (Exception)
            {
                return isGlobal && !ShowWarningsInGlobal ? [] : [
                    new Result
                    {
                        Title = "Invalid expression provided",
                        SubTitle = "Please check your mathematical expression",
                        IcoPath = WarningIconPath,
                    }
                ];
            }

            string fromCurrency = match.Groups["from"].Value;
            string toCurrency = string.IsNullOrEmpty(match.Groups["to"].Value) ? "" : match.Groups["to"].Value;

            if (string.IsNullOrEmpty(fromCurrency))
            {
                List<Result?> results = [];

                foreach (string currency in Currencies)
                {
                    if (ConversionDirection == 0)
                    {
                        results.Add(GetConversion(isGlobal, amountToConvert, LocalCurrency, currency));
                    }
                    else
                    {
                        results.Add(GetConversion(isGlobal, amountToConvert, currency, LocalCurrency));
                    }
                }

                foreach (string currency in Currencies)
                {
                    if (ConversionDirection == 0)
                    {
                        results.Add(GetConversion(isGlobal, amountToConvert, currency, LocalCurrency));
                    }
                    else
                    {
                        results.Add(GetConversion(isGlobal, amountToConvert, LocalCurrency, currency));
                    }
                }

                return results;
            }
            else if (string.IsNullOrEmpty(toCurrency))
            {
                List<Result?> results = [];

                if (ConversionDirection == 0)
                {
                    results.Add(GetConversion(isGlobal, amountToConvert, fromCurrency, LocalCurrency));
                }

                foreach (string currency in Currencies)
                {
                    results.Add(GetConversion(isGlobal, amountToConvert, fromCurrency, currency));
                }

                if (ConversionDirection == 1)
                {
                    results.Add(GetConversion(isGlobal, amountToConvert, fromCurrency, LocalCurrency));
                }

                return results;
            }

            return
            [
                GetConversion(isGlobal, amountToConvert, fromCurrency, toCurrency)
            ];
        }

        public List<Result> Query(Query query)
        {
            List<Result> results = ParseQuery(query.Search, string.IsNullOrEmpty(query.ActionKeyword)).Where(x => x != null).ToList();

            if (!string.IsNullOrEmpty(query.ActionKeyword))
            {
                try
                {
                    if (!File.Exists(AliasFileLocation))
                    {
                        throw new FileNotFoundException("Alias file not found.");
                    }

                    string jsonContent = File.ReadAllText(AliasFileLocation);
                    ValidateJsonFormat(jsonContent);
                }
                catch (Exception ex) when (ex is FileNotFoundException || ex is JsonException)
                {
                    const string link = "https://github.com/Advaith3600/PowerToys-Run-Currency-Converter?tab=readme-ov-file#aliasing";
                    results.Add(new Result
                    {
                        Title = ex.Message,
                        SubTitle = "Press enter or click to see how to fix this issue",
                        IcoPath = WarningIconPath,
                        ContextData = new Dictionary<string, string> { { "externalLink", link } },
                        Action = _ => PerformAction("externalLink", link)
                    });
                }
            }

            return results;
        }

        private void ValidateJsonFormat(string jsonContent)
        {
            try
            {
                using (JsonDocument doc = JsonDocument.Parse(jsonContent))
                {
                    // If parsing succeeds, the JSON is valid
                }
            }
            catch (JsonException)
            {
                throw new JsonException("Invalid JSON format.");
            }
        }

        public void Init(PluginInitContext context)
        {
            Context = context ?? throw new ArgumentNullException(nameof(context));
            Context.API.ThemeChanged += OnThemeChanged;
            UpdateIconPath(Context.API.GetCurrentTheme());

            AliasFileLocation = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "PowerToys", "CurrencyConverter", "alias.json");
            EnsureAliasFileExists();
        }

        private void EnsureAliasFileExists()
        {
            try
            {
                if (!File.Exists(AliasFileLocation))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(AliasFileLocation));
                    string defaultJsonContent = ReadEmbeddedResource("Community.PowerToys.Run.Plugin.CurrencyConverter.alias.default.json");
                    File.WriteAllText(AliasFileLocation, defaultJsonContent);
                }
            }
            catch (Exception) { }
        }

        private string ReadEmbeddedResource(string resourceName)
        {
            var assembly = Assembly.GetExecutingAssembly();
            using (Stream stream = assembly.GetManifestResourceStream(resourceName))
            {
                if (stream == null)
                {
                    throw new FileNotFoundException("Resource not found: " + resourceName);
                }

                using (StreamReader reader = new StreamReader(stream))
                {
                    return reader.ReadToEnd();
                }
            }
        }

        public List<ContextMenuResult> LoadContextMenus(Result selectedResult)
        {
            List<ContextMenuResult> results = [];

            if (selectedResult?.ContextData is Dictionary<string, string> contextData)
            {
                if (contextData.ContainsKey("copy"))
                {
                    results.Add(
                        new ContextMenuResult
                        {
                            PluginName = Name,
                            Title = "Copy (Enter)",
                            FontFamily = "Segoe Fluent Icons,Segoe MDL2 Assets",
                            Glyph = "\xE8C8",
                            AcceleratorKey = Key.Enter,
                            Action = _ => PerformAction("copy", contextData["copy"].ToString())
                        }
                    );
                }

                if (contextData.ContainsKey("externalLink"))
                {
                    string shortcutPrefix = contextData.ContainsKey("copy") ? "Ctrl + " : "";
                    results.Add(
                        new ContextMenuResult
                        {
                            PluginName = Name,
                            Title = $"Open ({shortcutPrefix}Enter)",
                            FontFamily = "Segoe Fluent Icons,Segoe MDL2 Assets",
                            Glyph = "\xE8A7",
                            AcceleratorKey = Key.Enter,
                            AcceleratorModifiers = contextData.ContainsKey("copy") ? ModifierKeys.Control : ModifierKeys.None,
                            Action = _ => PerformAction("externalLink", contextData["externalLink"].ToString())
                        }
                    );
                }
            }

            return results;
        }

        private bool PerformAction(string action, string context)
        {
            switch (action)
            {
                case "copy":
                    Clipboard.SetText(context);
                    break;
                case "externalLink":
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(context) { UseShellExecute = true });
                    break;
            }

            return true;
        }

        private void UpdateIconPath(Theme theme)
        {
            IconPath = theme == Theme.Light || theme == Theme.HighContrastWhite ? Context?.CurrentPluginMetadata.IcoPathLight : Context?.CurrentPluginMetadata.IcoPathDark;
            WarningIconPath = "images\\warning.png";
        }

        private void OnThemeChanged(Theme currentTheme, Theme newTheme) => UpdateIconPath(newTheme);

        System.Windows.Controls.Control ISettingProvider.CreateSettingPanel() => throw new NotImplementedException();

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (Disposed || !disposing)
            {
                return;
            }

            if (Context?.API != null)
            {
                Context.API.ThemeChanged -= OnThemeChanged;
            }

            Disposed = true;
        }
    }
}
