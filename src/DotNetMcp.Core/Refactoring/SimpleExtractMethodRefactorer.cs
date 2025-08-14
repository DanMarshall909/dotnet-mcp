using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DotNetMcp.Core.Refactoring;

public class SimpleExtractMethodRefactorer : SimpleRefactoringBase
{
    public record ExtractMethodResult(
        string ModifiedCode,
        string ExtractedMethod,
        string[] UsedVariables,
        string ReturnType);

    public async Task<ExtractMethodResult> ExtractMethodAsync(string code, string selectedCode, string methodName)
    {
        var (syntaxTree, semanticModel) = ParseCode(code);
        var root = await syntaxTree.GetRootAsync();

        // Find the class to add the method to
        var classDeclaration = root.DescendantNodes().OfType<ClassDeclarationSyntax>().FirstOrDefault();
        if (classDeclaration == null)
        {
            throw new InvalidOperationException("No class found to extract method into");
        }

        // Simple string-based approach for the selected code
        var normalizedSelected = selectedCode.Trim();
        var normalizedCode = code;

        // Check if the selected code actually exists in the source
        if (!normalizedCode.Contains(normalizedSelected))
        {
            throw new InvalidOperationException($"Selected code '{normalizedSelected}' not found in source");
        }

        // Create a simple extracted method
        var extractedMethod = $@"    private void {methodName}()
    {{
        {normalizedSelected}
    }}";

        // Replace the selected code with a method call
        var methodCall = $"{methodName}();";
        var modifiedCode = normalizedCode.Replace(normalizedSelected, methodCall);

        // Add the extracted method to the class (simple string insertion)
        var classEndIndex = modifiedCode.LastIndexOf('}');
        if (classEndIndex > 0)
        {
            modifiedCode = modifiedCode.Insert(classEndIndex, $"\n{extractedMethod}\n\n    ");
        }

        // Extract variable names from selected code (simplified)
        var usedVariables = ExtractVariableNames(normalizedSelected);

        return new ExtractMethodResult(
            modifiedCode,
            extractedMethod,
            usedVariables.ToArray(),
            "void");
    }

    private static List<string> ExtractVariableNames(string code)
    {
        var variables = new HashSet<string>();
        
        // Simple regex pattern to find identifiers that look like variables
        var words = System.Text.RegularExpressions.Regex.Matches(code, @"\b[a-zA-Z_][a-zA-Z0-9_]*\b")
            .Cast<System.Text.RegularExpressions.Match>()
            .Select(m => m.Value)
            .Where(w => !IsKeywordOrLiteral(w))
            .ToList();
            
        variables.UnionWith(words);
        
        return variables.ToList();
    }
    
    private static bool IsKeywordOrLiteral(string word)
    {
        var keywords = new HashSet<string>
        {
            "int", "string", "bool", "double", "float", "var", "new", "return", "if", "else", "for", "while",
            "class", "public", "private", "static", "void", "true", "false", "null", "this", "base",
            "Console", "WriteLine", "Length", "ToString"
        };
        
        return keywords.Contains(word) || char.IsDigit(word[0]);
    }
}