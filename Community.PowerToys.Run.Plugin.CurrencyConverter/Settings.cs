using Community.PowerToys.Run.Plugin.Update;
using Microsoft.PowerToys.Settings.UI.Library;

namespace Community.PowerToys.Run.Plugin.CurrencyConverter
{
    public class Settings
    {
        public PluginUpdateSettings Update { get; set; } = new PluginUpdateSettings { ResultScore = 100 };

        internal IEnumerable<PluginAdditionalOption> GetAdditionalOptions() => Update.GetAdditionalOptions();

        internal void SetAdditionalOptions(IEnumerable<PluginAdditionalOption> additionalOptions) => Update.SetAdditionalOptions(additionalOptions);
    }
}
