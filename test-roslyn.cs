using System;
using DotNetMcp.Core.Refactoring;

var code = @"
using System;

namespace TestNamespace
{
    public class TestClass
    {
        public void TestMethod()
        {
            var result = a + b + c;
            Console.WriteLine(""After extraction"");
        }
    }
}";

var refactorer = new ExtractMethodRefactorer();
var result = await refactorer.ExtractMethodAsync(code, "var result = a + b + c;", "CalculateSum");

Console.WriteLine("Modified Code:");
Console.WriteLine(result.ModifiedCode);
Console.WriteLine("\nExtracted Method:");
Console.WriteLine(result.ExtractedMethod);
Console.WriteLine($"\nUsed Variables: [{string.Join(", ", result.UsedVariables)}]");
Console.WriteLine($"Return Type: {result.ReturnType}");