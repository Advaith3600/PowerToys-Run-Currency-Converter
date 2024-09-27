namespace Community.PowerToys.Run.Plugin.CurrencyConverter.UnitTesting
{
    public class ApplyOpTest
    {
        readonly Main _main = new();    
        
        [Theory]
        [InlineData('+', 2)]
        [InlineData('-', 0)]
        [InlineData('*', 1)]
        public void ApplyOp_Should_Return_Operation_Result(char _operator, double expectedResult)
        {

            double result = _main.ApplyOp(_operator, 1, 1);

            Assert.Equal(expectedResult, result);
        }

        [Fact]
        public void ApplyOp_Should_Return_Exception_When_Some_Value_Zero()
        {
            double value1 = 0;
            double value2 = 1;
            char operation = '/';

            Assert.Throws<NotSupportedException>(() => _main.ApplyOp(operation, value1, value2));
        }

    }
}