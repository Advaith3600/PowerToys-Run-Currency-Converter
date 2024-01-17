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

        public List<Result> Query(Query query)
        {
            return new List<Result>
            {
                new Result
                {
                    Title = "Copy COOL",
                    IcoPath = IconPath,
                    Action = e =>
                    {
                        return true;
                    },
                },
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
            IconPath = "images/icon.png";
        }

        private void OnThemeChanged(Theme currentTheme, Theme newTheme)
        {
            UpdateIconPath(newTheme);
        }
    }
}