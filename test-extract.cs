using System;

namespace TestApp
{
    public class Calculator
    {
        public int Calculate(int a, int b)
        {
            // Some complex calculation logic
            int result = a + b;
            result = result * 2;
            if (result > 100)
            {
                result = result - 10;
            }
            Console.WriteLine($"Calculated result: {result}");
            return result;
        }
    }
}