using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Community.PowerToys.Run.Plugin.CurrencyConverter.UnitTesting
{
    public class EvaluateTest
    {
        readonly Main _main = new();

        [Theory]
        [InlineData("1+2", 3)]
        [InlineData("1-2", -1)]
        [InlineData("1/2", 0.5)]
        [InlineData("1*2a", 2)]
        [InlineData("1*2.9", 2.9)]
        [InlineData("1-2*3/2.1+(20+3)", 21.14)]
        public void Evaluate_Should_Return_Operation_Result(string operation, double expectedResult)
        {
            double actualResult = _main.Evaluate(operation);

            Assert.Equal(expectedResult, Math.Round(actualResult, 2));
        }
    }
}
