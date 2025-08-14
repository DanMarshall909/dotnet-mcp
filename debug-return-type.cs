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
            return 42;
        }
    }
}";

var refactorer = new ExtractMethodRefactorer();
var result = await refactorer.ExtractMethodAsync(code, "return 42;", "ExtractedMethod");

Console.WriteLine("=== Debug Return Type Inference ===");
Console.WriteLine($"Extracted Method: {result.ExtractedMethod}");
Console.WriteLine($"Return Type: {result.ReturnType}");
Console.WriteLine($"Modified Code: {result.ModifiedCode}");