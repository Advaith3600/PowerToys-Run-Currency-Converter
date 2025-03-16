using ManagedCommon;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows.Input;
using Community.PowerToys.Run.Plugin.Update;
using Microsoft.PowerToys.Settings.UI.Library;
using Wox.Plugin;
using Wox.Plugin.Logger;
using Wox.Infrastructure.Storage;

namespace Community.PowerToys.Run.Plugin.CurrencyConverter
{
    public class Main : IPlugin, IContextMenu, ISettingProvider, IDisposable, IDelayedExecutionPlugin, ISavable
    {
        public static string PluginID => "EF1F634F20484459A3679B4DE7B07999";
        public string Name => "Currency Converter";
        public string Description => "Convert real and crypto currencies.";

        private string? _iconPath;
        private string? _warningIconPath;
        private PluginInitContext _context = new();
        private bool _disposed;

        private PluginJsonStorage<Settings> _storage { get; }

        private Settings _settings { get; }

        private IPluginUpdateHandler _updater { get; }
        private Converter _converter { get; }

        private const string GithubReadmeURL = "https://github.com/Advaith3600/PowerToys-Run-Currency-Converter?tab=readme-ov-file#aliasing";

        public Main()
        {
            _storage = new PluginJsonStorage<Settings>();
            _settings = _storage.Load();

            _converter = new Converter(_settings);

            _updater = new PluginUpdateHandler(_settings.Update);
            _updater.UpdateInstalling += OnUpdateInstalling;
            _updater.UpdateInstalled += OnUpdateInstalled;
            _updater.UpdateSkipped += OnUpdateSkipped;
        }

        public IEnumerable<PluginAdditionalOption> AdditionalOptions => _settings.GetAdditionalOptions();

        public void UpdateSettings(PowerLauncherPluginSettings settings) => _settings.SetAdditionalOptions(settings.AdditionalOptions);

        public void Save() => _storage.Save();

        private List<Result> ParseQuery(string search, bool isGlobal)
        {
            NumberFormatInfo formatter = GetNumberFormatInfo();
            string decimalSeparator = Regex.Escape(formatter.CurrencyDecimalSeparator);
            string groupSeparator = Regex.Escape(formatter.CurrencyGroupSeparator);

            string amountPattern = $@"(?<amount>(?:\d+|\s+|{decimalSeparator}|{groupSeparator}|[+\-*/()])+)";
            string fromPattern = @"(?<from>[\p{L}\p{Sc}_]*)";
            string toPattern = @"(?<to>[\p{L}\p{Sc}_]*)";

            string pattern = $@"^\s*(?:(?:{amountPattern}\s*{fromPattern})|(?:{fromPattern}\s*{amountPattern}))\s*(?:to|in)?\s*{toPattern}\s*$";
#if DEBUG
            Log.Info("Using the regex expression: " + pattern, GetType());
#endif
            Match match = Regex.Match(search.Trim(), pattern);

            if (!match.Success)
            {
                return [];
            }

            decimal amountToConvert;
            try
            {
#if DEBUG
                Log.Info("Converting the expression to number: " + match.Groups["amount"].Value.Replace(formatter.CurrencyGroupSeparator, ""), GetType());
#endif
                amountToConvert = CalculateEngine.Evaluate(match.Groups["amount"].Value.Replace(formatter.CurrencyGroupSeparator, ""), GetNumberFormatInfo());
#if DEBUG
                Log.Info("Converted number is: " + amountToConvert, GetType());
#endif
            }
            catch (Exception)
            {
                return isGlobal && !_settings.ShowWarningsInGlobal ? [] : [
                    new()
                    {
                        Title = "Invalid expression provided",
                        SubTitle = "Please check your mathematical expression",
                        IcoPath = _warningIconPath,
                    }
                ];
            }

            string fromCurrency = match.Groups["from"].Value.Trim().ToLower();
            string toCurrency = string.IsNullOrEmpty(match.Groups["to"].Value.Trim()) ? "" : match.Groups["to"].Value.Trim().ToLower();
#if DEBUG
            Log.Info("from: " + fromCurrency + " and to: " + toCurrency, GetType());
#endif

            return _converter.GetConversionResults(isGlobal, amountToConvert, fromCurrency, toCurrency);
        }

        private NumberFormatInfo GetNumberFormatInfo()
        {
            NumberFormatInfo nfi = new();

            switch (_settings.DecimalSeparator)
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

        public List<Result> Query(Query query)
        {
            List<Result> results = [];

            if (_updater.IsUpdateAvailable())
            {
                results.AddRange(_updater.GetResults());
            }

            if (!(string.IsNullOrEmpty(query.ActionKeyword) || string.IsNullOrEmpty(query.Search.Trim())))
                results.Add(new Result
                {
                    Title = "Loading...",
                    SubTitle = "Please open an issue if needed.",
                    IcoPath = _iconPath,
                    ContextData = new Dictionary<string, string> { { "externalLink", GithubReadmeURL } },
                    Action = _ => Helper.PerformAction("externalLink", GithubReadmeURL)
                });

            return results;
        }

        public List<Result> Query(Query query, bool isDelayedExecution)
        {
            List<Result> results = [];

            if (_updater.IsUpdateAvailable())
            {
                results.AddRange(_updater.GetResults());
            }

            if (query.Search.Trim() is null || !isDelayedExecution)
            {
                return results;
            }

            if (!string.IsNullOrEmpty(query.ActionKeyword))
            {
                try
                {
                    _converter.ValidateAliasFile();
                    _converter.ValidateConversionAPI();
                }
                catch (Exception ex)
                {
                    results.Add(new Result
                    {
                        Title = ex.Message,
                        SubTitle = "Press enter or click to see how to fix this issue",
                        IcoPath = _warningIconPath,
                        ContextData = new Dictionary<string, string> { { "externalLink", GithubReadmeURL } },
                        Action = _ => Helper.PerformAction("externalLink", GithubReadmeURL)
                    });

                    return results;
                }
            }

#if DEBUG
            Log.Info("Parsing the input: " + query.Search, GetType());
#endif
            results.AddRange(ParseQuery(query.Search, string.IsNullOrEmpty(query.ActionKeyword)).Where(x => x != null).ToList());

            return results
                .GroupBy(r => new { r.Title, r.SubTitle })
                .Select(g => g.First())
                .ToList();
        }

        public void Init(PluginInitContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _context.API.ThemeChanged += OnThemeChanged;
            UpdateIconPath(_context.API.GetCurrentTheme());

            _updater.Init(_context);
        }

        public List<ContextMenuResult> LoadContextMenus(Result selectedResult)
        {
            List<ContextMenuResult> results = _updater.GetContextMenuResults(selectedResult);

            if (selectedResult?.ContextData is Dictionary<string, string> contextData)
            {
#if DEBUG
                string dictContents = string.Join(", ", contextData.Select(kv => $"{kv.Key}: {kv.Value}"));
                Log.Info($"Handling context action. Dictionary contents: {dictContents}", GetType());
#endif
                if (contextData.TryGetValue("copy", out string? value))
                {
                    string toCopy = value.ToString();
                    results.Add(
                        new()
                        {
                            PluginName = Name,
                            Title = "Copy (Enter)",
                            FontFamily = "Segoe Fluent Icons,Segoe MDL2 Assets",
                            Glyph = "\xE8C8",
                            AcceleratorKey = Key.Enter,
                            Action = _ => Helper.PerformAction("copy", toCopy)
                        }
                    );
                }

                if (contextData.TryGetValue("externalLink", out value))
                {
                    string shortcutPrefix = contextData.ContainsKey("copy") ? "Ctrl + " : "";
                    string link = value.ToString();
                    results.Add(
                        new()
                        {
                            PluginName = Name,
                            Title = $"Open ({shortcutPrefix}Enter)",
                            FontFamily = "Segoe Fluent Icons,Segoe MDL2 Assets",
                            Glyph = "\xE8A7",
                            AcceleratorKey = Key.Enter,
                            AcceleratorModifiers = contextData.ContainsKey("copy") ? ModifierKeys.Control : ModifierKeys.None,
                            Action = _ => Helper.PerformAction("externalLink", link)
                        }
                    );
                }
            }

            return results;
        }

        private void UpdateIconPath(Theme theme)
        {
            _iconPath = theme == Theme.Light || theme == Theme.HighContrastWhite ? _context?.CurrentPluginMetadata.IcoPathLight : _context?.CurrentPluginMetadata.IcoPathDark;
            _warningIconPath = "images\\warning.png";

            _converter.IconPath = _iconPath;
            _converter.WarningIconPath = _warningIconPath;
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

            _updater.Dispose();
            _converter.Dispose();

            _disposed = true;
        }
        private void OnUpdateInstalling(object? sender, PluginUpdateEventArgs e)
        {
            Log.Info("UpdateInstalling: " + e.Version, GetType());
        }

        private void OnUpdateInstalled(object? sender, PluginUpdateEventArgs e)
        {
            Log.Info("UpdateInstalled: " + e.Version, GetType());
            _context!.API.ShowNotification($"{Name} {e.Version}", "Update installed");
        }

        private void OnUpdateSkipped(object? sender, PluginUpdateEventArgs e)
        {
            Log.Info("UpdateSkipped: " + e.Version, GetType());
            Save();
            _context?.API.ChangeQuery(_context.CurrentPluginMetadata.ActionKeyword, true);
        }
    }
}