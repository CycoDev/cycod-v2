using System;
using System.Collections.Generic;
using System.Linq;

namespace GlowSharp.Examples
{
    /// <summary>
    /// Example C# code to demonstrate code file rendering.
    /// When rendered with GlowSharp, this will be automatically
    /// wrapped in a code block with syntax highlighting.
    /// </summary>
    public class Calculator
    {
        private readonly List<double> _history = new();

        public double Add(double a, double b)
        {
            var result = a + b;
            _history.Add(result);
            return result;
        }

        public double Multiply(double a, double b)
        {
            var result = a * b;
            _history.Add(result);
            return result;
        }

        public IEnumerable<double> GetHistory()
        {
            return _history.AsEnumerable();
        }

        public static void Main(string[] args)
        {
            var calc = new Calculator();

            // Perform some calculations
            var sum = calc.Add(10, 20);
            var product = calc.Multiply(5, 6);

            Console.WriteLine($"Sum: {sum}");
            Console.WriteLine($"Product: {product}");

            // Show history
            Console.WriteLine("\nCalculation History:");
            foreach (var result in calc.GetHistory())
            {
                Console.WriteLine($"  - {result}");
            }
        }
    }
}
