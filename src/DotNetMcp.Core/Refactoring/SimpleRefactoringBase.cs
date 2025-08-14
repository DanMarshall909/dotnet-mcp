using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DotNetMcp.Core.Refactoring;

public abstract class SimpleRefactoringBase
{
    protected static (SyntaxTree syntaxTree, SemanticModel semanticModel) ParseCode(string code)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(code);
        
        // Create a compilation with basic references
        var compilation = CSharpCompilation.Create(
            "TempAssembly",
            syntaxTrees: new[] { syntaxTree },
            references: GetBasicReferences());
            
        var semanticModel = compilation.GetSemanticModel(syntaxTree);
        
        return (syntaxTree, semanticModel);
    }
    
    private static MetadataReference[] GetBasicReferences()
    {
        var references = new List<MetadataReference>();
        
        // Add basic .NET references
        var runtimeLocation = typeof(object).Assembly.Location;
        var runtimeDirectory = Path.GetDirectoryName(runtimeLocation)!;
        
        references.Add(MetadataReference.CreateFromFile(runtimeLocation)); // System.Runtime
        references.Add(MetadataReference.CreateFromFile(Path.Combine(runtimeDirectory, "System.Runtime.dll")));
        references.Add(MetadataReference.CreateFromFile(Path.Combine(runtimeDirectory, "System.Collections.dll")));
        references.Add(MetadataReference.CreateFromFile(Path.Combine(runtimeDirectory, "System.Linq.dll")));
        references.Add(MetadataReference.CreateFromFile(Path.Combine(runtimeDirectory, "System.Console.dll")));
        
        return references.ToArray();
    }
}