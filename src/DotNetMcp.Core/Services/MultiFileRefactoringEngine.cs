using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.MSBuild;
using Microsoft.CodeAnalysis.Text;
using DotNetMcp.Core.Models;

namespace DotNetMcp.Core.Services;

public class MultiFileRefactoringEngine : IDisposable
{
    private Workspace? _workspace;
    private Solution? _solution;
    private readonly Dictionary<string, SyntaxTree> _syntaxTrees = new();
    private readonly Dictionary<string, SemanticModel> _semanticModels = new();

    public async Task<bool> LoadSolutionAsync(string solutionPath)
    {
        try
        {
            var msbuildWorkspace = MSBuildWorkspace.Create();
            _workspace = msbuildWorkspace;
            
            if (File.Exists(solutionPath) && solutionPath.EndsWith(".sln"))
            {
                _solution = await msbuildWorkspace.OpenSolutionAsync(solutionPath);
            }
            else if (File.Exists(solutionPath) && solutionPath.EndsWith(".csproj"))
            {
                var project = await msbuildWorkspace.OpenProjectAsync(solutionPath);
                _solution = project.Solution;
            }
            else
            {
                Console.Error.WriteLine($"LoadSolutionAsync: Invalid path or file does not exist: {solutionPath}");
                return false;
            }

            await PreloadSemanticModels();
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public async Task<bool> LoadFilesAsync(string[] filePaths)
    {
        try
        {
            // Use direct compilation for simpler approach
            var syntaxTrees = new List<SyntaxTree>();
            
            foreach (var filePath in filePaths)
            {
                if (File.Exists(filePath))
                {
                    var code = await File.ReadAllTextAsync(filePath);
                    var syntaxTree = CSharpSyntaxTree.ParseText(code, path: filePath);
                    syntaxTrees.Add(syntaxTree);
                    _syntaxTrees[filePath] = syntaxTree;
                }
            }

            // Create a compilation with basic references
            var basicReferences = new[]
            {
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(Console).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(System.Linq.Enumerable).Assembly.Location)
            };

            var compilation = CSharpCompilation.Create(
                "TempAssembly",
                syntaxTrees,
                basicReferences);

            // Create semantic models for each syntax tree
            foreach (var syntaxTree in syntaxTrees)
            {
                var semanticModel = compilation.GetSemanticModel(syntaxTree);
                var filePath = syntaxTree.FilePath ?? "unknown";
                _semanticModels[filePath] = semanticModel;
            }

            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private async Task PreloadSemanticModels()
    {
        if (_solution == null) return;

        foreach (var project in _solution.Projects)
        {
            var compilation = await project.GetCompilationAsync();
            if (compilation == null) continue;

            foreach (var document in project.Documents)
            {
                var syntaxTree = await document.GetSyntaxTreeAsync();
                var semanticModel = compilation.GetSemanticModel(syntaxTree);
                
                if (syntaxTree != null && semanticModel != null)
                {
                    _syntaxTrees[document.FilePath ?? document.Name] = syntaxTree;
                    _semanticModels[document.FilePath ?? document.Name] = semanticModel;
                }
            }
        }
    }

    public async Task<List<RefactoringDelta>> RenameSymbolAcrossFilesAsync(string symbolName, string newName, string? targetFilePath = null)
    {
        var deltas = new List<RefactoringDelta>();
        
        if (!_syntaxTrees.Any())
        {
            throw new InvalidOperationException("No files loaded");
        }

        // Find the symbol in the target file or all files
        ISymbol? foundSymbol = null;
        foreach (var kvp in _syntaxTrees)
        {
            var filePath = kvp.Key;
            var syntaxTree = kvp.Value;
            
            // If targetFilePath is specified, only process that file to find the symbol
            if (targetFilePath != null && filePath != targetFilePath)
                continue;

            if (!_semanticModels.TryGetValue(filePath, out var semanticModel))
                continue;

            var root = await syntaxTree.GetRootAsync();
            var symbol = FindSymbolInFile(root, semanticModel, symbolName);
            
            if (symbol != null)
            {
                foundSymbol = symbol;
                break;
            }
        }

        if (foundSymbol == null)
        {
            return deltas; // Symbol not found
        }

        // Find all references across all files
        foreach (var kvp in _syntaxTrees)
        {
            var filePath = kvp.Key;
            var syntaxTree = kvp.Value;
            
            if (!_semanticModels.TryGetValue(filePath, out var semanticModel))
                continue;

            var root = await syntaxTree.GetRootAsync();
            var references = FindReferencesInFile(root, semanticModel, foundSymbol);
            
            foreach (var reference in references)
            {
                var delta = CreateRenameDeltaFromToken(reference, symbolName, newName, filePath, syntaxTree);
                if (delta != null)
                {
                    deltas.Add(delta);
                }
            }
        }

        return deltas;
    }

    private ISymbol? FindSymbolInFile(SyntaxNode root, SemanticModel semanticModel, string symbolName)
    {
        var identifiers = root.DescendantTokens()
            .Where(t => t.ValueText == symbolName)
            .ToList();

        foreach (var identifier in identifiers)
        {
            var symbolInfo = semanticModel.GetSymbolInfo(identifier.Parent);
            if (symbolInfo.Symbol != null)
            {
                return symbolInfo.Symbol;
            }
        }

        return null;
    }

    private List<SyntaxToken> FindReferencesInFile(SyntaxNode root, SemanticModel semanticModel, ISymbol symbol)
    {
        var references = new List<SyntaxToken>();

        var identifiers = root.DescendantTokens()
            .Where(t => t.ValueText == symbol.Name);

        foreach (var identifier in identifiers)
        {
            var parent = identifier.Parent;
            if (parent != null)
            {
                var symbolInfo = semanticModel.GetSymbolInfo(parent);
                if (SymbolEqualityComparer.Default.Equals(symbolInfo.Symbol?.OriginalDefinition, symbol.OriginalDefinition))
                {
                    references.Add(identifier);
                }
            }
        }

        return references;
    }

    private RefactoringDelta? CreateRenameDeltaFromToken(SyntaxToken token, string oldName, string newName, string filePath, SyntaxTree syntaxTree)
    {
        var location = token.GetLocation();
        var lineSpan = location.GetLineSpan();
        var lineNumber = lineSpan.StartLinePosition.Line;

        var sourceText = syntaxTree.GetText();
        var lines = sourceText.Lines;
        
        if (lineNumber >= lines.Count) return null;
        
        var line = lines[lineNumber];
        var lineText = line.ToString();
        var newLineText = lineText.Replace(oldName, newName);

        var change = new Models.TextChange(
            lineNumber,
            lineNumber,
            lineText,
            newLineText,
            ChangeType.Replace);

        return new RefactoringDelta(
            filePath,
            new List<Models.TextChange> { change },
            null,
            new[] { oldName, newName });
    }

    public SemanticModel? GetSemanticModel(string filePath)
    {
        _semanticModels.TryGetValue(filePath, out var semanticModel);
        return semanticModel;
    }

    public SyntaxTree? GetSyntaxTree(string filePath)
    {
        _syntaxTrees.TryGetValue(filePath, out var syntaxTree);
        return syntaxTree;
    }

    public void Dispose()
    {
        _workspace?.Dispose();
    }
}