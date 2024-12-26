using Moq;
using System.Reflection;

namespace Community.PowerToys.Run.Plugin.CurrencyConverter.UnitTesting
{
    public class GetConversionTest
    {
        [Fact]
        public void GetConversion_ReturnsExpectedResult()
        {
            Main main = new();
            var mainMock = new Mock<Main> { CallBase = true };
            //Main main = mainMock.Object;
            MethodInfo GetConversionRateReflection = main.GetType().GetMethod("GetConversionRate", BindingFlags.NonPublic | BindingFlags.Instance);
            MethodInfo GetConversionReflection = main.GetType().GetMethod("GetConversion",  BindingFlags.NonPublic | BindingFlags.Instance);

            object[] getConversionRateParameters = ["USD", "AED"];
            object[] getConversionParameters = [2.0, "USD", "AED"];

            mainMock.Setup(method => GetConversionRateReflection.Invoke(method, getConversionRateParameters)).Returns(3.67);
            
            var getConversionResult = GetConversionReflection.Invoke(mainMock, getConversionParameters);

        }
    }
}
