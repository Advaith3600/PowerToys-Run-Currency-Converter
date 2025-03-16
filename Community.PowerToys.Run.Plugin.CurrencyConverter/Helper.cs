using Wox.Plugin.Logger;

namespace Community.PowerToys.Run.Plugin.CurrencyConverter
{
	public class Helper
	{
        public static bool PerformAction(string action, string context)
        {
#if DEBUG
            Log.Info("Performing '" + action + "' on: " + context, typeof(Main));
#endif

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
    }
}
