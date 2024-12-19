using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Library;
using System.Collections.Concurrent;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows.Input;
using Wox.Plugin;
using Wox.Plugin.Logger;

namespace Community.PowerToys.Run.Plugin.CurrencyConverter
{
    public class Main : IPlugin, IContextMenu, ISettingProvider, IDisposable, IDelayedExecutionPlugin
    {
        public static string PluginID => "EF1F634F20484459A3679B4DE7B07999";
        public string Name => "Currency Converter";
        public string Description => "Convert real and crypto currencies.";

        private string _iconPath;
        private string _warningIconPath;
        private PluginInitContext _context;
        private bool _disposed;
        private readonly ConcurrentDictionary<string, (JsonElement Rates, DateTime Timestamp)> _conversionCache = new();
        private HttpClient _httpClient;

        // Settings
        private bool _showWarningsInGlobal;
        private int _conversionDirection;
        private int _outputStyle;
        private int _decimalSeparator;
        private string _localCurrency;
        private string[] _currencies;
        private string _aliasFileLocation;

        // Constants
        private const int CacheExpirationHours = 3;
        private const bool EnableLog = false;
        private const string AliasFileName = "alias.json";
        private const string DefaultAliasResourceName = "Community.PowerToys.Run.Plugin.CurrencyConverter.alias.default.json";
        private const string GithubReadmeURL = "https://github.com/Advaith3600/PowerToys-Run-Currency-Converter?tab=readme-ov-file#aliasing";

        public IEnumerable<PluginAdditionalOption> AdditionalOptions => new List<PluginAdditionalOption>
        {
            new PluginAdditionalOption
            {
                Key = "ShowWarningsInGlobal",
                DisplayLabel = "Show warnings in global results",
                DisplayDescription = "Warnings from the plugin are suppressed when the \"Include in global result\" is checked",
                PluginOptionType = PluginAdditionalOption.AdditionalOptionType.Checkbox,
                Value = false,
            },
            new PluginAdditionalOption()
            {
                Key = "DecimalSeparator",
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
                ComboBoxValue = 1,
            },
            new PluginAdditionalOption()
            {
                Key = "QuickConversionDirection",
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
            new PluginAdditionalOption()
            {
                Key = "QuickConversionLocalCurrency",
                DisplayLabel = "Quick Conversion Local Currency",
                DisplayDescription = "Set your local currency for quick conversion",
                PluginOptionType = PluginAdditionalOption.AdditionalOptionType.Textbox,
                TextValue = (new RegionInfo(CultureInfo.CurrentCulture.Name)).ISOCurrencySymbol,
            },
            new PluginAdditionalOption()
            {
                Key = "QuickConversionCurrencies",
                DisplayLabel = "Quick Conversion Currencies",
                DisplayDescription = "Add currencies comma separated. eg: USD, EUR, BTC",
                PluginOptionType = PluginAdditionalOption.AdditionalOptionType.Textbox,
                TextValue = "USD",
            },
        };

        public void UpdateSettings(PowerLauncherPluginSettings settings)
        {
            if (settings?.AdditionalOptions == null) return;

            _showWarningsInGlobal = settings.AdditionalOptions.FirstOrDefault(x => x.Key == "ShowWarningsInGlobal")?.Value ?? false;
            _decimalSeparator = settings.AdditionalOptions.FirstOrDefault(x => x.Key == "DecimalSeparator")?.ComboBoxValue ?? 0;
            _conversionDirection = settings.AdditionalOptions.FirstOrDefault(x => x.Key == "QuickConversionDirection")?.ComboBoxValue ?? 0;
            _outputStyle = settings.AdditionalOptions.FirstOrDefault(x => x.Key == "ConversionOutputStyle")?.ComboBoxValue ?? 0;

            RegionInfo regionInfo = new RegionInfo(CultureInfo.CurrentCulture.Name);
            string localCurrency = settings.AdditionalOptions.FirstOrDefault(x => x.Key == "QuickConversionLocalCurrency")?.TextValue ?? "";
            _localCurrency = string.IsNullOrEmpty(localCurrency) ? regionInfo.ISOCurrencySymbol : localCurrency;

            _currencies = (settings.AdditionalOptions.FirstOrDefault(x => x.Key == "QuickConversionCurrencies")?.TextValue ?? "")
                .Split(',')
                .Select(x => x.Trim())
                .Where(x => !string.IsNullOrEmpty(x))
                .ToArray();
        }

        private double GetConversionRateSync(string fromCurrency, string toCurrency)
        {
            if (_conversionCache.TryGetValue(fromCurrency, out var cachedData) &&
                cachedData.Timestamp > DateTime.Now.AddHours(-CacheExpirationHours))
            {
                if (EnableLog) Log.Info("Converting from: " + fromCurrency + " to: " + toCurrency + " | Found it from the cache", GetType());
                if (cachedData.Rates.TryGetProperty(toCurrency, out JsonElement rate))
                {
                    return rate.GetDouble();
                }
                throw new Exception($"{toCurrency.ToUpper()} is not a valid currency");
            }

            if (EnableLog) Log.Info("Converting from: " + fromCurrency + " to: " + toCurrency + " | Trying to fetch", GetType());

            string url = $"https://cdn.jsdelivr.net/npm/@fawazahmed0/currency-api@latest/v1/currencies/{fromCurrency}.min.json";
            HttpResponseMessage response = _httpClient.GetAsync(url).Result;

            if (!response.IsSuccessStatusCode)
            {
                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    throw new Exception($"{fromCurrency.ToUpper()} is not a valid currency");
                }
                else
                {
                    string fallbackUrl = $"https://latest.currency-api.pages.dev/v1/currencies/{fromCurrency}.min.json";
                    response = _httpClient.GetAsync(fallbackUrl).Result;

                    if (!response.IsSuccessStatusCode)
                    {
                        throw response.StatusCode == System.Net.HttpStatusCode.NotFound
                            ? new Exception($"{fromCurrency.ToUpper()} is not a valid currency")
                            : new Exception("Something went wrong while fetching the conversion rate");
                    }

                    if (EnableLog) Log.Info("Converting from: " + fromCurrency + " to: " + toCurrency + " | Fetched from fallback", GetType());
                }
            } 
            else
            {
                if (EnableLog) Log.Info("Converting from: " + fromCurrency + " to: " + toCurrency + " | Fetched from API", GetType());
            }

            string content = response.Content.ReadAsStringAsync().Result;
            JsonElement element = JsonDocument.Parse(content).RootElement.GetProperty(fromCurrency);

            if (!element.TryGetProperty(toCurrency, out JsonElement conversionRate))
            {
                throw new Exception($"{toCurrency.ToUpper()} is not a valid currency");
            }

            _conversionCache[fromCurrency] = (element, DateTime.Now);
            return conversionRate.GetDouble();
        }

        private string GetCurrencyFromAlias(string currency)
        {
            if (!File.Exists(_aliasFileLocation))
            {
                return currency;
            }

            try
            {
                string jsonData = File.ReadAllText(_aliasFileLocation);
                using JsonDocument doc = JsonDocument.Parse(jsonData);
                return doc.RootElement.TryGetProperty(currency, out JsonElement value) ? value.GetString() : currency;
            }
            catch
            {
                return currency;
            }
        }

        private Result? GetConversion(bool isGlobal, double amountToConvert, string fromCurrency, string toCurrency)
        {
            fromCurrency = GetCurrencyFromAlias(fromCurrency.ToLower());
            toCurrency = GetCurrencyFromAlias(toCurrency.ToLower());

            if (fromCurrency == toCurrency || string.IsNullOrEmpty(fromCurrency) || string.IsNullOrEmpty(toCurrency))
            {
                return null;
            }

            if (EnableLog) Log.Info("Converting from: " + fromCurrency + " to: " + toCurrency, GetType());

            try
            {
                double conversionRate = GetConversionRateSync(fromCurrency, toCurrency);
                (double convertedAmount, int precision) = CalculateConvertedAmount(amountToConvert, conversionRate);

                string fromFormatted = amountToConvert.ToString("N", CultureInfo.CurrentCulture);
                string toFormatted = (amountToConvert < 0 ? convertedAmount * -1 : convertedAmount).ToString($"N{precision}", CultureInfo.CurrentCulture);

                string compressedOutput = $"{toFormatted} {toCurrency.ToUpper()}";
                string expandedOutput = $"{fromFormatted} {fromCurrency.ToUpper()} = {toFormatted} {toCurrency.ToUpper()}";

                return new Result
                {
                    Title = _outputStyle == 0 ? compressedOutput : expandedOutput,
                    SubTitle = $"Currency conversion from {fromCurrency.ToUpper()} to {toCurrency.ToUpper()}",
                    QueryTextDisplay = compressedOutput,
                    IcoPath = _iconPath,
                    ContextData = new Dictionary<string, string> { { "copy", toFormatted } },
                    ToolTipData = new ToolTipData(expandedOutput, "Click to copy the converted amount"),
                    Action = _ => PerformAction("copy", toFormatted)
                };
            }
            catch (Exception e)
            {
                const string link = "https://cdn.jsdelivr.net/npm/@fawazahmed0/currency-api@latest/v1/currencies.json";
                return isGlobal && !_showWarningsInGlobal ? null : new Result
                {
                    Title = e.Message,
                    SubTitle = "Press enter or click to open the currencies list",
                    IcoPath = _warningIconPath,
                    ContextData = new Dictionary<string, string> { { "externalLink", link } },
                    Action = _ => PerformAction("externalLink", link)
                };
            }
        }

        private (double ConvertedAmount, int Precision) CalculateConvertedAmount(double amountToConvert, double conversionRate)
        {
            int precision = CultureInfo.CurrentCulture.NumberFormat.CurrencyDecimalDigits;
            double rawConvertedAmount = Math.Abs(amountToConvert * conversionRate);
            double convertedAmount = Math.Round(rawConvertedAmount, precision);

            if (rawConvertedAmount < 1)
            {
                string rawStr = rawConvertedAmount.ToString("F10", CultureInfo.InvariantCulture);
                int decimalPointIndex = rawStr.IndexOf('.');
                if (decimalPointIndex != -1)
                {
                    int numberOfZeros = rawStr.Substring(decimalPointIndex + 1).TakeWhile(c => c == '0').Count();
                    precision += numberOfZeros;
                    convertedAmount = Math.Round(rawConvertedAmount, precision);
                }
            }

            return (convertedAmount, precision);
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

        public double ApplyOp(char op, double b, double a) => op switch
        {
            '+' => a + b,
            '-' => a - b,
            '*' => a * b,
            '/' when b != 0 => a / b,
            '/' => throw new DivideByZeroException("Cannot divide by zero"),
            _ => throw new ArgumentException("Invalid operator", nameof(op))
        };

        public NumberFormatInfo GetNumberFormatInfo()
        {
            NumberFormatInfo nfi = new NumberFormatInfo();

            switch (_decimalSeparator)
            {
                case 1:
                    nfi.CurrencyDecimalSeparator = ".";
                    nfi.CurrencyGroupSeparator = ",";
                    break;
                case 2:
                    nfi.CurrencyDecimalSeparator = ",";
                    nfi.CurrencyGroupSeparator = ".";
                    break;
                default:
                    nfi = CultureInfo.CurrentCulture.NumberFormat;
                    break;
            }

            return nfi;
        }

        public double Evaluate(string expression)
        {
            Stack<double> values = new Stack<double>();
            Stack<char> ops = new Stack<char>();

            NumberFormatInfo formatter = GetNumberFormatInfo();
            string separator = formatter.CurrencyDecimalSeparator;

            for (int i = 0; i < expression.Length; i++)
            {
                if (expression[i] == ' ')
                    continue;

                if (expression[i] >= '0' && expression[i] <= '9')
                {
                    StringBuilder sbuf = new StringBuilder();
                    while (i < expression.Length && ((expression[i] >= '0' && expression[i] <= '9') || expression.Substring(i, separator.Length) == separator || char.IsWhiteSpace(expression[i]))) 
                    {
                        if (!char.IsWhiteSpace(expression[i]))
                            sbuf.Append(expression[i]);
                        i += expression.Substring(i, separator.Length) == separator ? separator.Length : 1;
                    }

                    values.Push(double.Parse(sbuf.ToString(), NumberStyles.Currency, formatter));
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

        private List<Result> GetConversionResults(bool isGlobal, double amountToConvert, string fromCurrency, string toCurrency)
        {
            List<(int index, string fromCurrency, Task<Result?> task)> conversionTasks = [];
            int index = 0;

            if (string.IsNullOrEmpty(fromCurrency))
            {
                foreach (string currency in _currencies)
                {
                    if (_conversionDirection == 0)
                    {
                        conversionTasks.Add((index++, _localCurrency, Task.Run(() => GetConversion(isGlobal, amountToConvert, _localCurrency, currency))));
                    }
                    else
                    {
                        conversionTasks.Add((index++, currency, Task.Run(() => GetConversion(isGlobal, amountToConvert, currency, _localCurrency))));
                    }
                }

                foreach (string currency in _currencies)
                {
                    if (_conversionDirection == 0)
                    {
                        conversionTasks.Add((index++, currency, Task.Run(() => GetConversion(isGlobal, amountToConvert, currency, _localCurrency))));
                    }
                    else
                    {
                        conversionTasks.Add((index++, _localCurrency, Task.Run(() => GetConversion(isGlobal, amountToConvert, _localCurrency, currency))));
                    }
                }
            }
            else if (string.IsNullOrEmpty(toCurrency))
            {
                if (_conversionDirection == 0)
                {
                    conversionTasks.Add((index++, fromCurrency, Task.Run(() => GetConversion(isGlobal, amountToConvert, fromCurrency, _localCurrency))));
                }

                foreach (string currency in _currencies)
                {
                    conversionTasks.Add((index++, fromCurrency, Task.Run(() => GetConversion(isGlobal, amountToConvert, fromCurrency, currency))));
                }

                if (_conversionDirection == 1)
                {
                    conversionTasks.Add((index++, fromCurrency, Task.Run(() => GetConversion(isGlobal, amountToConvert, fromCurrency, _localCurrency))));
                }
            }
            else
            {
                conversionTasks.Add((index++, fromCurrency, Task.Run(() => GetConversion(isGlobal, amountToConvert, fromCurrency, toCurrency))));
            }

            if (EnableLog) Log.Info("Found " + conversionTasks.Count + " conversions", GetType());

            var groupedTasks = conversionTasks.GroupBy(t => t.fromCurrency);

            var results = new Result?[conversionTasks.Count];

            Parallel.ForEach(groupedTasks, group =>
            {
                foreach (var task in group)
                {
                    task.task.Wait();
                    results[task.index] = task.task.Result;
                }
            });

            return results.Where(r => r != null).ToList();
        }

        private List<Result> ParseQuery(string search, bool isGlobal)
        {
            NumberFormatInfo formatter = GetNumberFormatInfo();
            string decimalSeparator = Regex.Escape(formatter.CurrencyDecimalSeparator);
            string groupSeparator = Regex.Escape(formatter.CurrencyGroupSeparator);

            string amountPattern = $@"(?<amount>(?:\d+|\s+|{decimalSeparator}|{groupSeparator}|[+\-*/()])+)";
            string fromPattern = @"(?<from>[\p{L}\p{Sc}_]*)";
            string toPattern = @"(?<to>[\p{L}\p{Sc}_]*)";

            string pattern = $@"^\s*(?:(?:{amountPattern}\s*{fromPattern})|(?:{fromPattern}\s*{amountPattern}))\s*(?:to|in)?\s*{toPattern}\s*$";
            if (EnableLog) Log.Info("Using the regex expression: " + pattern, GetType());
            Match match = Regex.Match(search.Trim(), pattern);

            if (!match.Success)
            {
                return new List<Result>();
            }

            double amountToConvert;
            try
            {
                CultureInfo culture = CultureInfo.CurrentCulture;
                if (EnableLog) Log.Info("Converting the expression to number: " + match.Groups["amount"].Value.Replace(formatter.CurrencyGroupSeparator, ""), GetType());
                amountToConvert = Evaluate(match.Groups["amount"].Value.Replace(formatter.CurrencyGroupSeparator, ""));
                if (EnableLog) Log.Info("Converted number is: " + amountToConvert, GetType());
            }
            catch (Exception)
            {
                return isGlobal && !_showWarningsInGlobal ? new List<Result>() : new List<Result>
                {
                    new Result
                    {
                        Title = "Invalid expression provided",
                        SubTitle = "Please check your mathematical expression",
                        IcoPath = _warningIconPath,
                    }
                };
            }

            string fromCurrency = match.Groups["from"].Value.Trim().ToLower();
            string toCurrency = string.IsNullOrEmpty(match.Groups["to"].Value.Trim()) ? "" : match.Groups["to"].Value.Trim().ToLower();
            if (EnableLog) Log.Info("from: " + fromCurrency + " and to: " + toCurrency, GetType());

            return GetConversionResults(isGlobal, amountToConvert, fromCurrency, toCurrency);
        }

        public List<Result> Query(Query query)
        {
            return string.IsNullOrEmpty(query.ActionKeyword) || string.IsNullOrEmpty(query.Search.Trim())
                ? new List<Result>()
                : new List<Result>
                {
                    new Result
                    {
                        Title = "Loading...",
                        SubTitle = "Please open an issue if needed.",
                        IcoPath = _iconPath,
                        ContextData = new Dictionary<string, string> { { "externalLink", GithubReadmeURL } },
                        Action = _ => PerformAction("externalLink", GithubReadmeURL)
                    }
                };

        }

        public List<Result> Query(Query query, bool isDelayedExecution)
        {
            if (query.Search.Trim() is null || !isDelayedExecution)
            {
                return new List<Result>();
            }

            if (EnableLog) Log.Info("Parsing the input: " + query.Search, GetType());

            List<Result> results = ParseQuery(query.Search, string.IsNullOrEmpty(query.ActionKeyword)).Where(x => x != null).ToList();

            if (!string.IsNullOrEmpty(query.ActionKeyword))
            {
                try
                {
                    if (!File.Exists(_aliasFileLocation))
                    {
                        throw new FileNotFoundException("Alias file not found.");
                    }

                    string jsonContent = File.ReadAllText(_aliasFileLocation);
                    ValidateJsonFormat(jsonContent);
                }
                catch (Exception ex) when (ex is FileNotFoundException || ex is JsonException)
                {
                    results.Add(new Result
                    {
                        Title = ex.Message,
                        SubTitle = "Press enter or click to see how to fix this issue",
                        IcoPath = _warningIconPath,
                        ContextData = new Dictionary<string, string> { { "externalLink", GithubReadmeURL } },
                        Action = _ => PerformAction("externalLink", GithubReadmeURL)
                    });
                }
            }

            return results
                .GroupBy(r => new { r.Title, r.SubTitle })
                .Select(g => g.First())
                .ToList();
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
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _context.API.ThemeChanged += OnThemeChanged;
            UpdateIconPath(_context.API.GetCurrentTheme());

            _aliasFileLocation = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "PowerToys",
                "CurrencyConverter",
                AliasFileName);
            EnsureAliasFileExists();

            HttpClientHandler handler = new HttpClientHandler
            {
                UseDefaultCredentials = true,
                PreAuthenticate = true
            };
            _httpClient = new HttpClient(handler);
        }

        private void EnsureAliasFileExists()
        {
            if (File.Exists(_aliasFileLocation)) return;

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_aliasFileLocation));
                string defaultJsonContent = ReadEmbeddedResource(DefaultAliasResourceName);
                File.WriteAllText(_aliasFileLocation, defaultJsonContent);
            }
            catch (Exception ex)
            {
                if (EnableLog) Log.Error($"An error occurred while creating the alias file at {_aliasFileLocation}. Exception: {ex.Message}", GetType());
            }
        }

        private string ReadEmbeddedResource(string resourceName)
        {
            using Stream? stream = typeof(Main).Assembly.GetManifestResourceStream(resourceName);
            if (stream == null)
            {
                throw new FileNotFoundException("Resource not found: " + resourceName);
            }

            using StreamReader reader = new StreamReader(stream);
            return reader.ReadToEnd();
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
            _iconPath = theme == Theme.Light || theme == Theme.HighContrastWhite ? _context?.CurrentPluginMetadata.IcoPathLight : _context?.CurrentPluginMetadata.IcoPathDark;
            _warningIconPath = "images\\warning.png";
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
            if (_disposed || !disposing) return;

            if (_context?.API != null)
            {
                _context.API.ThemeChanged -= OnThemeChanged;
            }

            _httpClient.Dispose();
            _disposed = true;
        }
    }
}