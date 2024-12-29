using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Globalization;
using System.Collections.Concurrent;
using Wox.Plugin;
using Wox.Plugin.Logger;

namespace Community.PowerToys.Run.Plugin.CurrencyConverter
{
    internal class CaseInsensitiveTupleComparer : IEqualityComparer<(string From, string To)>
    {
        public bool Equals((string From, string To) x, (string From, string To) y)
        {
            return string.Equals(x.From, y.From, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(x.To, y.To, StringComparison.OrdinalIgnoreCase);
        }

        public int GetHashCode((string From, string To) obj)
        {
            return StringComparer.OrdinalIgnoreCase.GetHashCode(obj.From) ^
                StringComparer.OrdinalIgnoreCase.GetHashCode(obj.To);
        }
    }

    public class Converter
    {
        private Settings _settings { get; }
        private ConverterSettings _converterSettings { get; }

        internal string? IconPath { get; set; }
        internal string WarningIconPath { get; set; } = "";

        private readonly string _aliasFileLocation;
        private readonly ConcurrentDictionary<(string From, string To), (decimal Rate, DateTime Timestamp)> _conversionCache = new(new CaseInsensitiveTupleComparer());
        private readonly HttpClient _httpClient;

        private const string AliasFileName = "alias.json";
        private const string DefaultAliasResourceName = "Community.PowerToys.Run.Plugin.CurrencyConverter.alias.default.json";

        public Converter(Settings settings)
        {
            _settings = settings;
            _converterSettings = new(_settings);

            HttpClientHandler handler = new()
            {
                UseDefaultCredentials = true,
                PreAuthenticate = true
            };
            _httpClient = new HttpClient(handler);

            _aliasFileLocation = Path.Combine(
                Constant.DataDirectory,
                "Settings",
                "Plugins",
                Assembly.GetExecutingAssembly().GetName().Name,
                AliasFileName);

            EnsureAliasFileExists();
        }

        public List<Result> GetConversionResults(bool isGlobal, decimal amountToConvert, string fromCurrency, string toCurrency)
        {
            List<(int index, string fromCurrency, Task<Result?> task)> conversionTasks = [];
            int index = 0;

            if (string.IsNullOrEmpty(fromCurrency))
            {
                foreach (string currency in _settings.Currencies)
                {
                    if (_settings.ConversionDirection == 0)
                    {
                        conversionTasks.Add((index++, _settings.LocalCurrency, Task.Run(() => GetConversion(isGlobal, amountToConvert, _settings.LocalCurrency, currency))));
                    }
                    else
                    {
                        conversionTasks.Add((index++, currency, Task.Run(() => GetConversion(isGlobal, amountToConvert, currency, _settings.LocalCurrency))));
                    }
                }

                foreach (string currency in _settings.Currencies)
                {
                    if (_settings.ConversionDirection == 0)
                    {
                        conversionTasks.Add((index++, currency, Task.Run(() => GetConversion(isGlobal, amountToConvert, currency, _settings.LocalCurrency))));
                    }
                    else
                    {
                        conversionTasks.Add((index++, _settings.LocalCurrency, Task.Run(() => GetConversion(isGlobal, amountToConvert, _settings.LocalCurrency, currency))));
                    }
                }
            }
            else if (string.IsNullOrEmpty(toCurrency))
            {
                if (_settings.ConversionDirection == 0)
                {
                    conversionTasks.Add((index++, fromCurrency, Task.Run(() => GetConversion(isGlobal, amountToConvert, fromCurrency, _settings.LocalCurrency))));
                }

                foreach (string currency in _settings.Currencies)
                {
                    conversionTasks.Add((index++, fromCurrency, Task.Run(() => GetConversion(isGlobal, amountToConvert, fromCurrency, currency))));
                }

                if (_settings.ConversionDirection == 1)
                {
                    conversionTasks.Add((index++, fromCurrency, Task.Run(() => GetConversion(isGlobal, amountToConvert, fromCurrency, _settings.LocalCurrency))));
                }
            }
            else
            {
                conversionTasks.Add((index++, fromCurrency, Task.Run(() => GetConversion(isGlobal, amountToConvert, fromCurrency, toCurrency))));
            }

#if DEBUG
            Log.Info("Found " + conversionTasks.Count + " conversions", GetType());
#endif

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

        private Result? GetConversion(bool isGlobal, decimal amountToConvert, string fromCurrency, string toCurrency)
        {
            fromCurrency = GetCurrencyFromAlias(fromCurrency.ToLower());
            toCurrency = GetCurrencyFromAlias(toCurrency.ToLower());

            if (fromCurrency == toCurrency || string.IsNullOrEmpty(fromCurrency) || string.IsNullOrEmpty(toCurrency))
            {
                return null;
            }

#if DEBUG
            Log.Info("Converting from: " + fromCurrency + " to: " + toCurrency, GetType());
#endif

            try
            {
                decimal conversionRate = GetConversionRateSync(fromCurrency, toCurrency);
                (decimal convertedAmount, int precision) = CalculateConvertedAmount(amountToConvert, conversionRate);

                string fromFormatted = amountToConvert.ToString("N", CultureInfo.CurrentCulture);
                string toFormatted = (amountToConvert < 0 ? convertedAmount * -1 : convertedAmount).ToString($"N{precision}", CultureInfo.CurrentCulture);

                string compressedOutput = $"{toFormatted} {toCurrency.ToUpper()}";
                string expandedOutput = $"{fromFormatted} {fromCurrency.ToUpper()} = {toFormatted} {toCurrency.ToUpper()}";

                return new Result
                {
                    Title = _settings.OutputStyle == 0 ? compressedOutput : expandedOutput,
                    SubTitle = $"Currency conversion from {fromCurrency.ToUpper()} to {toCurrency.ToUpper()}",
                    QueryTextDisplay = compressedOutput,
                    IcoPath = IconPath,
                    ContextData = new Dictionary<string, string> { { "copy", toFormatted } },
                    ToolTipData = new ToolTipData(expandedOutput, "Click to copy the converted amount"),
                    Action = _ => Helper.PerformAction("copy", toFormatted)
                };
            }
            catch (Exception e)
            {
                return isGlobal && !_settings.ShowWarningsInGlobal ? null : new Result
                {
                    Title = e.Message,
                    SubTitle = "Press enter or click to open the currencies list",
                    IcoPath = WarningIconPath,
                    ContextData = new Dictionary<string, string> { { "externalLink", _converterSettings.GetHelperLink() } },
                    Action = _ => Helper.PerformAction("externalLink", _converterSettings.GetHelperLink())
                };
            }
        }

        private decimal GetConversionRateSync(string fromCurrency, string toCurrency)
        {
            var cacheKey = (fromCurrency, toCurrency);

            if (_conversionCache.TryGetValue(cacheKey, out var directCacheData) &&
                directCacheData.Timestamp > DateTime.Now.AddHours(-_settings.ConversionCacheDuration))
            {
#if DEBUG
                Log.Info($"Converting from: {fromCurrency} to: {toCurrency} | Found direct rate in cache", GetType());
#endif
                return directCacheData.Rate;
            }

#if DEBUG
            Log.Info($"Converting from: {fromCurrency} to: {toCurrency} | Fetching from API", GetType());
#endif

            string url = _converterSettings.GetConversionLink(fromCurrency, toCurrency);
            HttpResponseMessage response = _httpClient.GetAsync(url).Result;

            if (!response.IsSuccessStatusCode)
            {
                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    throw new Exception($"{fromCurrency.ToUpper()} is not a valid currency");
                }
                else
                {
                    string fallbackUrl = _converterSettings.GetConversionFallbackLink(fromCurrency, toCurrency);
                    response = _httpClient.GetAsync(fallbackUrl).Result;

                    if (!response.IsSuccessStatusCode)
                    {
                        throw response.StatusCode == System.Net.HttpStatusCode.NotFound
                            ? new Exception($"{fromCurrency.ToUpper()} is not a valid currency")
                            : new Exception("Something went wrong while fetching the conversion rate");
                    }

#if DEBUG
                    Log.Info($"Converting from: {fromCurrency} to: {toCurrency} | Fetched from fallback", GetType());
#endif
                }
            }

            string content = response.Content.ReadAsStringAsync().Result;
            decimal conversionRate;

            JsonElement fromCurrencyElement = _converterSettings.GetRootJsonElementFor(content, fromCurrency);
            foreach (JsonProperty property in fromCurrencyElement.EnumerateObject())
            {
                (string targetCurrency, decimal rate) = _converterSettings.GetRateFor(property);
                _conversionCache[(fromCurrency, targetCurrency)] = (rate, DateTime.Now);
            }
            if (!_conversionCache.TryGetValue((fromCurrency, toCurrency), out var cacheOutput))
            {
                throw new Exception($"{toCurrency.ToUpper()} is not a valid currency");
            }
            conversionRate = cacheOutput.Rate;

            return conversionRate;
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

                JsonElement rootElement = doc.RootElement;
                string result = currency;

                foreach (JsonProperty property in rootElement.EnumerateObject())
                {
                    if (property.Name.Equals(currency, StringComparison.OrdinalIgnoreCase))
                    {
                        result = property.Value.GetString();
                        break;
                    }
                }

                return result;
            }
            catch
            {
                return currency;
            }
        }

        private (decimal ConvertedAmount, int Precision) CalculateConvertedAmount(decimal amountToConvert, decimal conversionRate)
        {
            int precision = CultureInfo.CurrentCulture.NumberFormat.CurrencyDecimalDigits;
            decimal rawConvertedAmount = Math.Abs(amountToConvert * conversionRate);
            decimal convertedAmount = Math.Round(rawConvertedAmount, precision);

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
#if DEBUG
                Log.Error($"An error occurred while creating the alias file at {_aliasFileLocation}. Exception: {ex.Message}", GetType());
#endif
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

        internal void ValidateAliasFile()
        {
            if (!File.Exists(_aliasFileLocation))
            {
                throw new FileNotFoundException("Alias file not found.");
            }

            string jsonContent = File.ReadAllText(_aliasFileLocation);
            try
            {
                using (JsonDocument doc = JsonDocument.Parse(jsonContent))
                {
                    // If parsing succeeds, the JSON is valid
                }
            }
            catch (JsonException)
            {
                throw new JsonException("Invalid JSON format in alias file.");
            }
        }

        internal void ValidateConversionAPI() => _converterSettings.ValidateConversionAPI();

        public void Dispose()
        {
            _httpClient.Dispose();
        }
    }
}
