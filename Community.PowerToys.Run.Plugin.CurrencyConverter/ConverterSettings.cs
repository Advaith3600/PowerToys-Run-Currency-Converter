using System.Text.Json;

namespace Community.PowerToys.Run.Plugin.CurrencyConverter
{
    public class ConverterSettings
    {
        public string ConversionDate { get; set; } = "latest";

        private Settings _settings { get; }

        public ConverterSettings(Settings settings)
        {
            _settings = settings;
        }

        private readonly Dictionary<string, Dictionary<string, string>> _options = new()
        {
            {
                "Default", new()
                {
                    {"ConversionLink", "https://cdn.jsdelivr.net/npm/@fawazahmed0/currency-api@{date}/v1/currencies/{from}.min.json"},
                    {"ConversionFallbackLink", "https://{date}.currency-api.pages.dev/v1/currencies/{from}.min.json"},
                    {"ConversionHelperLink", "https://cdn.jsdelivr.net/npm/@fawazahmed0/currency-api@latest/v1/currencies.json"},
                }
            },
            {
                "ExchangeRateAPI", new()
                {
                    {"ConversionLink", "https://v6.exchangerate-api.com/v6/{api_key}/{date}/{from}"},
                    {"ConversionFallbackLink", "https://v6.exchangerate-api.com/v6/{api_key}/{date}/{from}"},
                    {"ConversionHelperLink", "https://www.exchangerate-api.com/docs/supported-currencies"},
                }
            },
            {
                "CurrencyAPI", new()
                {
                    {"ConversionLink", "https://api.currencyapi.com/v3/{date}?apikey={api_key}&base_currency={from}"},
                    {"ConversionFallbackLink", "https://api.currencyapi.com/v3/{date}?apikey={api_key}&base_currency={from}"},
                    {"ConversionHelperLink", "https://currencyapi.com/docs/currency-list"},
                }
            }
        };

        private string ParseLink(string link, string from, string to) => link
            .Replace("{api_key}", _settings.ConversionAPIKey)
            .Replace("{date}", ConversionDate)
            .Replace("{from}", _settings.ConversionAPI == (int)ConverterSettingsEnum.CurrencyAPI ? from.ToUpper() : from)
            .Replace("{to}", to);
        private Dictionary<string, string> GetOption() => _options[((ConverterSettingsEnum)_settings.ConversionAPI).ToString()];

        internal string GetConversionLink(string from, string to) => ParseLink(GetOption()["ConversionLink"], from, to);
        internal string GetConversionFallbackLink(string from, string to) => ParseLink(GetOption()["ConversionFallbackLink"], from, to);
        internal string GetHelperLink() => GetOption()["ConversionHelperLink"];

        private void EnsureConversionAPIKey()
        {
            if (string.IsNullOrEmpty(_settings.ConversionAPIKey))
                throw new Exception("Conversion API Key is not provided");
        }

        internal void ValidateConversionAPI()
        {
            if (_settings.ConversionAPI != (int)ConverterSettingsEnum.Default)
                EnsureConversionAPIKey();
        }

        internal JsonElement GetRootJsonElementFor(string content, string fromCurrency)
        {
            JsonElement GetProperty(string property) => JsonDocument.Parse(content).RootElement.GetProperty(property);

            switch (_settings.ConversionAPI)
            {
                case (int)ConverterSettingsEnum.Default: return GetProperty(fromCurrency);
                case (int)ConverterSettingsEnum.ExchangeRateAPI: return GetProperty("conversion_rates");
                case (int)ConverterSettingsEnum.CurrencyAPI: return GetProperty("data");
            }

            throw new Exception("Invalid Conversion API selected.");
        }

        internal (string, decimal) GetRateFor(JsonProperty property)
        {
            if (_settings.ConversionAPI == (int)ConverterSettingsEnum.CurrencyAPI)
            {
                if (property.Value.TryGetProperty("code", out JsonElement codeElement))
                {
                    string code = codeElement.GetString();
                    if (property.Value.TryGetProperty("value", out JsonElement valueElement))
                    {
                        decimal value = valueElement.GetDecimal();
                        return (code, value);
                    }
                }
                throw new Exception("Invalid JSON structure: missing 'code' or 'value'.");
            }

            return (property.Name, property.Value.GetDecimal());
        }

    }

    enum ConverterSettingsEnum
    {
        Default,
        ExchangeRateAPI,
        CurrencyAPI,
    }
}