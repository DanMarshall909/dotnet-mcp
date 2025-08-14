using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using DotNetMcp.Core.Services;
using DotNetMcp.Core.Models;

namespace DotNetMcp.Core.Refactoring;

public class RenameSymbolRefactorer : RefactoringBase
{
    public record RenameResult(
        string ModifiedCode,
        int TotalChanges,
        string SymbolType,
        string[] Conflicts);

    public async Task<RenameResult> RenameSymbolAsync(string code, string originalName, string newName, string symbolKind = "auto")
    {
        var (syntaxTree, semanticModel) = ParseCode(code);
        var root = await syntaxTree.GetRootAsync();
        
        // Use string-based replacement for reliability
        var modifiedCode = code;
        var changes = 0;
        var symbolType = DetermineSymbolType(root, originalName, symbolKind);
        
        // Replace all occurrences using regex to handle word boundaries
        var pattern = $@"\b{System.Text.RegularExpressions.Regex.Escape(originalName)}\b";
        var regex = new System.Text.RegularExpressions.Regex(pattern);
        var matches = regex.Matches(modifiedCode);
        
        if (matches.Count > 0)
        {
            modifiedCode = regex.Replace(modifiedCode, newName);
            changes = matches.Count;
        }
        else
        {
            // If no changes were made, return "unknown" type
            symbolType = "unknown";
        }

        return new RenameResult(
            modifiedCode,
            changes,
            symbolType,
            Array.Empty<string>());
    }

    public async Task<CompactRefactoringResult> RenameSymbolMultiFileAsync(
        MultiFileRefactoringEngine engine, 
        string symbolName, 
        string newName, 
        string? targetFilePath = null)
    {
        try
        {
            var deltas = await engine.RenameSymbolAcrossFilesAsync(symbolName, newName, targetFilePath);
            
            if (!deltas.Any())
            {
                return new CompactRefactoringResult(
                    false,
                    new List<RefactoringDelta>(),
                    $"Symbol '{symbolName}' not found");
            }

            var totalChanges = deltas.Sum(d => d.Changes.Count);
            var affectedFiles = deltas.Select(d => d.FilePath).Distinct().Count();
            
            var summary = new RefactoringSummary(
                $"Rename '{symbolName}' to '{newName}'",
                "void", // Not applicable for rename
                new[] { symbolName, newName },
                EstimateTokenSavings(deltas));

            return new CompactRefactoringResult(
                true,
                deltas,
                Summary: summary);
        }
        catch (Exception ex)
        {
            return new CompactRefactoringResult(
                false,
                new List<RefactoringDelta>(),
                ex.Message);
        }
    }

    private static int EstimateTokenSavings(List<RefactoringDelta> deltas)
    {
        // For rename operations, we don't really save tokens, but we can report efficiency
        return deltas.Sum(d => d.Changes.Count); // Return number of changes made
    }

    private static string DetermineSymbolType(SyntaxNode root, string originalName, string symbolKind)
    {
        // Try to determine the actual symbol type by looking at the syntax
        var classDeclarations = root.DescendantNodes().OfType<ClassDeclarationSyntax>()
            .Where(c => c.Identifier.ValueText == originalName);
        if (classDeclarations.Any())
            return "class";

        var interfaceDeclarations = root.DescendantNodes().OfType<InterfaceDeclarationSyntax>()
            .Where(i => i.Identifier.ValueText == originalName);
        if (interfaceDeclarations.Any())
            return "interface";

        var methodDeclarations = root.DescendantNodes().OfType<MethodDeclarationSyntax>()
            .Where(m => m.Identifier.ValueText == originalName);
        if (methodDeclarations.Any())
            return "method";

        // Check for fields first (they are class-level variables)
        var fieldDeclarations = root.DescendantNodes().OfType<FieldDeclarationSyntax>()
            .SelectMany(f => f.Declaration.Variables)
            .Where(v => v.Identifier.ValueText == originalName);
        if (fieldDeclarations.Any())
            return "field";

        // Then check for local variables
        var variableDeclarators = root.DescendantNodes().OfType<VariableDeclaratorSyntax>()
            .Where(v => v.Identifier.ValueText == originalName);
        if (variableDeclarators.Any())
            return "local variable";

        // If we have a specific kind hint, use it
        if (symbolKind != "auto")
            return symbolKind;

        return "symbol";
    }
}