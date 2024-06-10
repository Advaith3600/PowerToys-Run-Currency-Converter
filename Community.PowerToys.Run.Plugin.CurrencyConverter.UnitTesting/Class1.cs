using Moq;
using System.Reflection;

namespace Community.PowerToys.Run.Plugin.CurrencyConverter.UnitTesting
{
    public class GetConversionTest
    {
        [Fact]
        public void GetConversion_ReturnsExpectedResult()
        {
            Main main = new Main();
            MethodInfo method = main.GetType().GetMethod("GetConversion",  BindingFlags.NonPublic | BindingFlags.Instance);

            object[] _params = new object[] { 2.0, "USD", "AED" };

            var conversionResult = method.Invoke(main, _params);

            Assert()
        }
    }
}
