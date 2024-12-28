using System.IO;
using System.Reflection;
using System.Text.Json;
using Wox.Plugin;
using Wox.Plugin.Logger;

namespace Community.PowerToys.Run.Plugin.CurrencyConverter
{
    public class CustomConverterSettings
    {
        private string _location;
        private const string FileName = "CustomConverterSettings.json";

        public int CacheExpirationHours { get; set; } = 3;
        public string ConversionDate { get; set; } = "latest";
        public bool IsEnabled { get; set; } = false;
        public string ConversionLink { get; set; } = "https://cdn.jsdelivr.net/npm/@fawazahmed0/currency-api@{date}/v1/currencies/{from}.min.json";
        public string ConversionFallbackLink { get; set; } = "https://{date}.currency-api.pages.dev/v1/currencies/{from}.min.json";
        public bool LinkHasToCurrency { get; set; } = false;
        public string ConversionHelperLink { get; set; } = "https://cdn.jsdelivr.net/npm/@fawazahmed0/currency-api@latest/v1/currencies.json";

        public CustomConverterSettings()
        {
            _location = Path.Combine(
                Constant.DataDirectory,
                "Settings",
                "Plugins",
                Assembly.GetExecutingAssembly().GetName().Name,
                FileName);

            if (!File.Exists(_location))
            {
                EnsureFileExists();
#if DEBUG
                Log.Info("Creating a new custom converter settings file", GetType());
#endif
            }
            else
            {
                ValidateSettingsFile();
                LoadSettingsFile();
#if DEBUG
                Log.Info("Validated and loaded the custom converter settings file", GetType());
#endif
            }
        }

        private void EnsureFileExists()
        {
            var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_location, json);
        }

        private void ValidateSettingsFile()
        {
            var json = File.ReadAllText(_location);
            var settings = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json);

            if (settings == null)
            {
                throw new InvalidOperationException("Custom Converter Settings file is invalid or empty.");
            }

            var properties = this.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);

            foreach (var prop in properties)
            {
                if (!settings.ContainsKey(prop.Name))
                {
                    throw new InvalidOperationException($"Missing property: {prop.Name}");
                }

                var jsonElement = settings[prop.Name];
                var propertyType = prop.PropertyType;

                if (!IsValidType(jsonElement, propertyType))
                {
                    throw new InvalidOperationException($"Invalid type for property: {prop.Name}");
                }
            }
        }

        private bool IsValidType(JsonElement element, Type type)
        {
            if (type == typeof(bool))
            {
                return element.ValueKind == JsonValueKind.True || element.ValueKind == JsonValueKind.False;
            }
            else if (type == typeof(int))
            {
                return element.ValueKind == JsonValueKind.Number && element.TryGetInt32(out _);
            }
            else if (type == typeof(string))
            {
                return element.ValueKind == JsonValueKind.String;
            }
            return false;
        }

        private void LoadSettingsFile()
        {
            var json = File.ReadAllText(_location);
            var settings = JsonSerializer.Deserialize<CustomConverterSettings>(json);

            if (settings != null)
            {
                CacheExpirationHours = settings.CacheExpirationHours;

                if (settings.IsEnabled)
                {
                    foreach (var prop in this.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
                    {
                        var value = prop.GetValue(settings);
                        prop.SetValue(this, value);
                    }
                }
            }
        }

        private string ParseLink(string link, string from, string to) => link.Replace("{date}", ConversionDate).Replace("{from}", from).Replace("to", to);
        internal string GetConversionLink(string from, string to) => ParseLink(ConversionLink, from, to);
        internal string GetConversionFallbackLink(string from, string to) => ParseLink(ConversionFallbackLink, from, to);
    }
}