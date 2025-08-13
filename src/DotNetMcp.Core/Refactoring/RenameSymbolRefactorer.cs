using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.MSBuild;

namespace DotNetMcp.Core.Refactoring;

public class RenameSymbolRefactorer
{
    public record RenameResult(
        string[] AffectedFiles,
        int TotalChanges,
        string SymbolType,
        string[] Conflicts);

    public async Task<RenameResult> RenameSymbolAsync(string solutionPath, string originalName, string newName, string symbolKind)
    {
        using var workspace = MSBuildWorkspace.Create();
        
        var solution = solutionPath.EndsWith(".sln") 
            ? await workspace.OpenSolutionAsync(solutionPath)
            : await workspace.OpenProjectAsync(solutionPath).ContinueWith(t => t.Result.Solution);

        var affectedFiles = new List<string>();
        var totalChanges = 0;
        var conflicts = new List<string>();
        var symbolType = "unknown";

        foreach (var project in solution.Projects)
        {
            foreach (var document in project.Documents)
            {
                if (!document.Name.EndsWith(".cs")) continue;

                var syntaxTree = await document.GetSyntaxTreeAsync();
                if (syntaxTree == null) continue;

                var root = await syntaxTree.GetRootAsync();
                var semanticModel = await document.GetSemanticModelAsync();
                if (semanticModel == null) continue;

                var (newRoot, changes, detectedSymbolType, documentConflicts) = await RenameInDocument(
                    root, semanticModel, originalName, newName, symbolKind);

                if (changes > 0)
                {
                    affectedFiles.Add(document.FilePath ?? document.Name);
                    totalChanges += changes;
                    
                    if (symbolType == "unknown")
                        symbolType = detectedSymbolType;

                    conflicts.AddRange(documentConflicts);

                    // Write the modified content back to the file
                    if (document.FilePath != null)
                    {
                        await File.WriteAllTextAsync(document.FilePath, newRoot.ToFullString());
                    }
                }
            }
        }

        return new RenameResult(
            affectedFiles.ToArray(),
            totalChanges,
            symbolType,
            conflicts.ToArray());
    }

    private static async Task<(SyntaxNode newRoot, int changes, string symbolType, string[] conflicts)> RenameInDocument(
        SyntaxNode root, 
        SemanticModel semanticModel, 
        string originalName, 
        string newName, 
        string symbolKind)
    {
        var changes = 0;
        var symbolType = "unknown";
        var conflicts = new List<string>();
        var newRoot = root;

        // Find all identifier nodes that match the original name
        var identifiers = root.DescendantNodes()
            .OfType<IdentifierNameSyntax>()
            .Where(id => id.Identifier.ValueText == originalName)
            .ToList();

        foreach (var identifier in identifiers)
        {
            var symbolInfo = semanticModel.GetSymbolInfo(identifier);
            var symbol = symbolInfo.Symbol;

            if (symbol == null) continue;

            // Determine if this is the type of symbol we want to rename
            if (ShouldRenameSymbol(symbol, symbolKind))
            {
                symbolType = GetSymbolTypeString(symbol);
                
                // Check for conflicts
                if (HasNamingConflict(semanticModel, identifier, newName))
                {
                    conflicts.Add($"Potential naming conflict at {identifier.GetLocation()}");
                    continue;
                }

                // Replace the identifier
                var newIdentifier = identifier.WithIdentifier(SyntaxFactory.Identifier(newName));
                newRoot = newRoot.ReplaceNode(identifier, newIdentifier);
                changes++;
            }
        }

        // Also handle declarations (class names, method names, variable declarations, etc.)
        var declarations = GetDeclarationNodes(root, originalName, symbolKind);
        foreach (var declaration in declarations)
        {
            var newDeclaration = RenameDeclaration(declaration, originalName, newName);
            if (newDeclaration != null)
            {
                newRoot = newRoot.ReplaceNode(declaration, newDeclaration);
                changes++;
            }
        }

        return (newRoot, changes, symbolType, conflicts.ToArray());
    }

    private static bool ShouldRenameSymbol(ISymbol symbol, string symbolKind)
    {
        return symbolKind.ToLower() switch
        {
            "auto" => true,
            "variable" => symbol is ILocalSymbol or IFieldSymbol,
            "method" => symbol is IMethodSymbol,
            "class" => symbol is INamedTypeSymbol { TypeKind: TypeKind.Class },
            "interface" => symbol is INamedTypeSymbol { TypeKind: TypeKind.Interface },
            "property" => symbol is IPropertySymbol,
            _ => false
        };
    }

    private static string GetSymbolTypeString(ISymbol symbol)
    {
        return symbol switch
        {
            ILocalSymbol => "local variable",
            IFieldSymbol => "field",
            IMethodSymbol => "method",
            INamedTypeSymbol { TypeKind: TypeKind.Class } => "class",
            INamedTypeSymbol { TypeKind: TypeKind.Interface } => "interface",
            IPropertySymbol => "property",
            _ => "symbol"
        };
    }

    private static bool HasNamingConflict(SemanticModel semanticModel, SyntaxNode context, string newName)
    {
        // Simplified conflict detection - check if newName is already used in the same scope
        var enclosingMember = context.FirstAncestorOrSelf<MemberDeclarationSyntax>();
        if (enclosingMember == null) return false;

        var existingIdentifiers = enclosingMember.DescendantNodes()
            .OfType<IdentifierNameSyntax>()
            .Where(id => id.Identifier.ValueText == newName);

        return existingIdentifiers.Any();
    }

    private static IEnumerable<SyntaxNode> GetDeclarationNodes(SyntaxNode root, string originalName, string symbolKind)
    {
        var declarations = new List<SyntaxNode>();

        if (symbolKind is "auto" or "class")
        {
            declarations.AddRange(root.DescendantNodes().OfType<ClassDeclarationSyntax>()
                .Where(c => c.Identifier.ValueText == originalName));
        }

        if (symbolKind is "auto" or "interface")
        {
            declarations.AddRange(root.DescendantNodes().OfType<InterfaceDeclarationSyntax>()
                .Where(i => i.Identifier.ValueText == originalName));
        }

        if (symbolKind is "auto" or "method")
        {
            declarations.AddRange(root.DescendantNodes().OfType<MethodDeclarationSyntax>()
                .Where(m => m.Identifier.ValueText == originalName));
        }

        if (symbolKind is "auto" or "property")
        {
            declarations.AddRange(root.DescendantNodes().OfType<PropertyDeclarationSyntax>()
                .Where(p => p.Identifier.ValueText == originalName));
        }

        if (symbolKind is "auto" or "variable")
        {
            declarations.AddRange(root.DescendantNodes().OfType<VariableDeclaratorSyntax>()
                .Where(v => v.Identifier.ValueText == originalName));
        }

        return declarations;
    }

    private static SyntaxNode? RenameDeclaration(SyntaxNode declaration, string originalName, string newName)
    {
        return declaration switch
        {
            ClassDeclarationSyntax classDecl => classDecl.WithIdentifier(SyntaxFactory.Identifier(newName)),
            InterfaceDeclarationSyntax interfaceDecl => interfaceDecl.WithIdentifier(SyntaxFactory.Identifier(newName)),
            MethodDeclarationSyntax methodDecl => methodDecl.WithIdentifier(SyntaxFactory.Identifier(newName)),
            PropertyDeclarationSyntax propertyDecl => propertyDecl.WithIdentifier(SyntaxFactory.Identifier(newName)),
            VariableDeclaratorSyntax variableDecl => variableDecl.WithIdentifier(SyntaxFactory.Identifier(newName)),
            _ => null
        };
    }
}