using System.Globalization;
using System.Text;

namespace Community.PowerToys.Run.Plugin.CurrencyConverter
{
	public static class CalculateEngine
	{
        private static bool HasPrecedence(char op1, char op2)
        {
            if (op2 == '(' || op2 == ')')
                return false;
            if ((op1 == '*' || op1 == '/') && (op2 == '+' || op2 == '-'))
                return false;
            else
                return true;
        }

        private static double ApplyOp(char op, double b, double a) => op switch
        {
            '+' => a + b,
            '-' => a - b,
            '*' => a * b,
            '/' when b != 0 => a / b,
            '/' => throw new DivideByZeroException("Cannot divide by zero"),
            _ => throw new ArgumentException("Invalid operator", nameof(op))
        };

        public static double Evaluate(string expression, NumberFormatInfo formatter)
        {
            Stack<double> values = new Stack<double>();
            Stack<char> ops = new Stack<char>();

            string separator = formatter.CurrencyDecimalSeparator;
            for (int i = 0; i < expression.Length; i++)
            {
                if (expression[i] == ' ')
                    continue;

                if (expression[i] >= '0' && expression[i] <= '9')
                {
                    StringBuilder sbuf = new StringBuilder();
                    while (i < expression.Length && ((expression[i] >= '0' && expression[i] <= '9') || expression.Substring(i, separator.Length) == separator || char.IsWhiteSpace(expression[i])))
                    {
                        if (!char.IsWhiteSpace(expression[i]))
                            sbuf.Append(expression[i]);
                        i += expression.Substring(i, separator.Length) == separator ? separator.Length : 1;
                    }

                    values.Push(double.Parse(sbuf.ToString(), NumberStyles.Currency, formatter));
                    i--;
                }

                else if (expression[i] == '(')
                    ops.Push(expression[i]);

                else if (expression[i] == ')')
                {
                    while (ops.Count > 0 && ops.Peek() != '(')
                        values.Push(ApplyOp(ops.Pop(), values.Pop(), values.Pop()));
                    ops.Pop();
                }

                else if (expression[i] == '+' || expression[i] == '-' || expression[i] == '*' || expression[i] == '/')
                {
                    while (ops.Count > 0 && HasPrecedence(expression[i], ops.Peek()))
                        values.Push(ApplyOp(ops.Pop(), values.Pop(), values.Pop()));
                    ops.Push(expression[i]);
                }
            }

            while (ops.Count > 0)
                values.Push(ApplyOp(ops.Pop(), values.Pop(), values.Pop()));

            return values.Pop();
        }
    }
}
