using System;
using System.Reflection;

namespace Community.PowerToys.Run.Plugin.CurrencyConverter
{
    public class CustomConverter
    {
        private string _location;
        private const string FileName = "CustomConverterSettings.json";

        public bool IsEnabled { get; set; } = false;

        public CustomConverter() 
        {
            _location = Path.Combine(
                Constant.DataDirectory,
                "Settings",
                "Plugins",
                 Assembly.GetExecutingAssembly().GetName().Name,
                FileName);
        }
    }
}