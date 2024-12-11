namespace Community.PowerToys.Run.Plugin.CurrencyConverter.UnitTesting
{
    public class HasPrecedenceTest
    {
        readonly Main _main = new();

        [Theory]
        [InlineData('*', '*')]
        //wip
        public void HasPrecedence_Should_Return_True(char operation1, char operation2)
        {
            bool result = _main.HasPrecedence(operation1, operation2);

            Assert.True(result);
        }
    }
}