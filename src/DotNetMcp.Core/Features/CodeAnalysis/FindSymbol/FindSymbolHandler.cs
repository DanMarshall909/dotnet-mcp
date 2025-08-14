using System.IO.Abstractions;
using System.Text;
using DotNetMcp.Core.Common;
using DotNetMcp.Core.Services;
using DotNetMcp.Core.SharedKernel;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Extensions.Logging;

namespace DotNetMcp.Core.Features.CodeAnalysis;

/// <summary>
/// Handler for finding symbols in a codebase
/// </summary>
public class FindSymbolHandler : BaseHandler<FindSymbolCommand, FindSymbolResponse>
{
    private readonly IFileSystem _fileSystem;
    private readonly BuildValidationService _buildValidationService;
    private readonly CompilationService _compilationService;

    public FindSymbolHandler(ILogger<FindSymbolHandler> logger, IFileSystem fileSystem, BuildValidationService buildValidationService, CompilationService compilationService) 
        : base(logger)
    {
        _fileSystem = fileSystem;
        _buildValidationService = buildValidationService;
        _compilationService = compilationService;
    }

    protected override async Task<Result<FindSymbolResponse>> HandleAsync(FindSymbolCommand request, CancellationToken cancellationToken)
    {
        try
        {
            // Validate project path exists
            if (!_fileSystem.Directory.Exists(request.ProjectPath))
            {
                return Result<FindSymbolResponse>.Failure($"Project path not found: {request.ProjectPath}");
            }

            // Validate build before attempting Roslyn analysis
            var buildValidation = await _buildValidationService.ValidateBuildAsync(request.ProjectPath, cancellationToken);
            if (!buildValidation.IsSuccess && !buildValidation.IsWarning)
            {
                return Result<FindSymbolResponse>.Failure(
                    $"Cannot perform symbol analysis due to build errors: {buildValidation.Message}\n\n{buildValidation.ErrorSummary}");
            }

            // Find all C# files in the project
            var csharpFiles = _fileSystem.Directory
                .GetFiles(request.ProjectPath, "*.cs", SearchOption.AllDirectories)
                .Where(f => !f.Contains("bin") && !f.Contains("obj"))
                .ToArray();

            if (!csharpFiles.Any())
            {
                return Result<FindSymbolResponse>.Failure("No C# files found in the project path");
            }

            // Search for symbols across all files
            var foundSymbols = new List<SymbolInfo>();
            var symbolsByType = new Dictionary<SymbolType, int>();
            var symbolsByNamespace = new Dictionary<string, int>();

            foreach (var filePath in csharpFiles)
            {
                var symbols = await FindSymbolsInFile(filePath, request.SymbolName, request.SymbolType, request.IncludeImplementations);
                foundSymbols.AddRange(symbols);
            }

            // Calculate statistics
            foreach (var symbol in foundSymbols)
            {
                symbolsByType[symbol.SymbolType] = symbolsByType.GetValueOrDefault(symbol.SymbolType, 0) + 1;
                symbolsByNamespace[symbol.Namespace] = symbolsByNamespace.GetValueOrDefault(symbol.Namespace, 0) + 1;
            }

            // Apply result limiting
            var totalFound = foundSymbols.Count;
            var resultsToShow = foundSymbols.Take(request.MaxResults).ToArray();

            // Apply token optimization if requested
            var estimatedTokens = EstimateTokens(resultsToShow);
            var summarizationApplied = false;

            if (request.OptimizeForTokens && estimatedTokens > request.MaxTokens)
            {
                resultsToShow = OptimizeForTokens(resultsToShow, request.MaxTokens);
                estimatedTokens = EstimateTokens(resultsToShow);
                summarizationApplied = true;
            }

            // Find similar symbols if no exact matches
            var similarSymbols = totalFound == 0 
                ? await FindSimilarSymbols(request.SymbolName, foundSymbols)
                : null;

            var summary = new FindSymbolSummary
            {
                TotalFound = totalFound,
                TotalShown = resultsToShow.Length,
                SymbolsByType = symbolsByType,
                SymbolsByNamespace = symbolsByNamespace,
                SimilarSymbols = similarSymbols
            };

            var response = new FindSymbolResponse
            {
                Symbols = resultsToShow,
                Summary = summary,
                EstimatedTokens = estimatedTokens,
                SummarizationApplied = summarizationApplied
            };

            return Result<FindSymbolResponse>.Success(response);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error finding symbols for {SymbolName} in {ProjectPath}", request.SymbolName, request.ProjectPath);
            return Result<FindSymbolResponse>.Failure($"Error searching for symbols: {ex.Message}", ex);
        }
    }

    private async Task<List<SymbolInfo>> FindSymbolsInFile(string filePath, string symbolName, SymbolType symbolType, bool includeImplementations)
    {
        var symbols = new List<SymbolInfo>();

        try
        {
            var content = await _fileSystem.File.ReadAllTextAsync(filePath);
            var syntaxTree = CSharpSyntaxTree.ParseText(content, path: filePath);
            var root = await syntaxTree.GetRootAsync();

            // Use CompilationService to handle duplicate file names properly
            var compilation = await _compilationService.CreateSingleFileCompilationAsync(filePath);
            var semanticModel = _compilationService.GetSemanticModel(compilation, filePath);

            if (semanticModel == null)
            {
                Logger.LogWarning("Could not create semantic model for file: {FilePath}", filePath);
                return symbols;
            }

            // Find symbols based on type
            var foundNodes = new List<(SyntaxNode node, SymbolType type)>();

            if (symbolType == SymbolType.Any || symbolType == SymbolType.Class)
            {
                foundNodes.AddRange(root.DescendantNodes()
                    .OfType<ClassDeclarationSyntax>()
                    .Where(c => MatchesSymbolName(c.Identifier.ValueText, symbolName))
                    .Select(c => ((SyntaxNode)c, SymbolType.Class)));
            }

            if (symbolType == SymbolType.Any || symbolType == SymbolType.Interface)
            {
                foundNodes.AddRange(root.DescendantNodes()
                    .OfType<InterfaceDeclarationSyntax>()
                    .Where(i => MatchesSymbolName(i.Identifier.ValueText, symbolName))
                    .Select(i => ((SyntaxNode)i, SymbolType.Interface)));
            }

            if (symbolType == SymbolType.Any || symbolType == SymbolType.Method)
            {
                foundNodes.AddRange(root.DescendantNodes()
                    .OfType<MethodDeclarationSyntax>()
                    .Where(m => MatchesSymbolName(m.Identifier.ValueText, symbolName))
                    .Select(m => ((SyntaxNode)m, SymbolType.Method)));
            }

            if (symbolType == SymbolType.Any || symbolType == SymbolType.Property)
            {
                foundNodes.AddRange(root.DescendantNodes()
                    .OfType<PropertyDeclarationSyntax>()
                    .Where(p => MatchesSymbolName(p.Identifier.ValueText, symbolName))
                    .Select(p => ((SyntaxNode)p, SymbolType.Property)));
            }

            if (symbolType == SymbolType.Any || symbolType == SymbolType.Field)
            {
                foundNodes.AddRange(root.DescendantNodes()
                    .OfType<FieldDeclarationSyntax>()
                    .SelectMany(f => f.Declaration.Variables)
                    .Where(v => MatchesSymbolName(v.Identifier.ValueText, symbolName))
                    .Select(v => ((SyntaxNode)v, SymbolType.Field)));
            }

            // Convert syntax nodes to SymbolInfo
            foreach (var (node, type) in foundNodes)
            {
                var symbolInfo = await CreateSymbolInfo(node, type, filePath, semanticModel, includeImplementations);
                if (symbolInfo != null)
                {
                    symbols.Add(symbolInfo);
                }
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Error parsing file {FilePath}", filePath);
        }

        return symbols;
    }

    #pragma warning disable CS1998 // Async method lacks 'await' operators - intentional for future extensibility
    private async Task<SymbolInfo?> CreateSymbolInfo(SyntaxNode node, SymbolType symbolType, string filePath, SemanticModel semanticModel, bool includeImplementations)
    #pragma warning restore CS1998
    {
        try
        {
            var linePosition = node.GetLocation().GetLineSpan().StartLinePosition;
            var symbol = semanticModel.GetDeclaredSymbol(node);
            
            if (symbol == null) return null;

            var symbolInfo = new SymbolInfo
            {
                Name = symbol.Name,
                SymbolType = symbolType,
                Namespace = symbol.ContainingNamespace?.ToDisplayString() ?? "Global",
                FilePath = filePath,
                LineNumber = linePosition.Line + 1,
                Signature = symbol.ToDisplayString(),
                Documentation = symbol.GetDocumentationCommentXml(),
                AccessModifier = GetAccessModifier(symbol.DeclaredAccessibility),
                IsStatic = symbol.IsStatic,
                IsAbstract = symbol.IsAbstract,
                IsVirtual = symbol.IsVirtual
            };

            // Add method-specific information
            if (symbol is IMethodSymbol methodSymbol)
            {
                symbolInfo = symbolInfo with
                {
                    Parameters = methodSymbol.Parameters.Select(p => new ParameterInfo
                    {
                        Name = p.Name,
                        Type = p.Type.ToDisplayString(),
                        DefaultValue = p.HasExplicitDefaultValue ? p.ExplicitDefaultValue?.ToString() : null,
                        IsOptional = p.IsOptional
                    }).ToArray(),
                    ReturnType = methodSymbol.ReturnType.ToDisplayString()
                };
            }

            // Find implementations if requested
            if (includeImplementations && symbol is INamedTypeSymbol namedType && namedType.TypeKind == TypeKind.Interface)
            {
                // This would require cross-project analysis - placeholder for now
                symbolInfo = symbolInfo with { Implementations = Array.Empty<SymbolInfo>() };
            }

            return symbolInfo;
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Error creating symbol info for node in {FilePath}", filePath);
            return null;
        }
    }

    private static bool MatchesSymbolName(string symbolName, string searchPattern)
    {
        if (searchPattern.Contains('*'))
        {
            // Simple wildcard matching
            var pattern = searchPattern.Replace("*", ".*");
            return System.Text.RegularExpressions.Regex.IsMatch(symbolName, pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        }
        
        return string.Equals(symbolName, searchPattern, StringComparison.OrdinalIgnoreCase);
    }

    private static AccessModifier GetAccessModifier(Accessibility accessibility)
    {
        return accessibility switch
        {
            Accessibility.Public => AccessModifier.Public,
            Accessibility.Private => AccessModifier.Private,
            Accessibility.Protected => AccessModifier.Protected,
            Accessibility.Internal => AccessModifier.Internal,
            Accessibility.ProtectedAndInternal => AccessModifier.PrivateProtected,
            Accessibility.ProtectedOrInternal => AccessModifier.ProtectedInternal,
            _ => AccessModifier.Private
        };
    }

    private static MetadataReference[] GetBasicReferences()
    {
        var references = new List<MetadataReference>();
        
        var runtimeLocation = typeof(object).Assembly.Location;
        var runtimeDirectory = Path.GetDirectoryName(runtimeLocation)!;
        
        references.Add(MetadataReference.CreateFromFile(runtimeLocation));
        references.Add(MetadataReference.CreateFromFile(Path.Combine(runtimeDirectory, "System.Runtime.dll")));
        references.Add(MetadataReference.CreateFromFile(Path.Combine(runtimeDirectory, "System.Collections.dll")));
        
        return references.ToArray();
    }

    private async Task<string[]> FindSimilarSymbols(string searchName, List<SymbolInfo> allSymbols)
    {
        // Simple similarity matching based on Levenshtein distance or contains logic
        var similar = allSymbols
            .Where(s => s.Name.Contains(searchName, StringComparison.OrdinalIgnoreCase) || 
                       LevenshteinDistance(s.Name.ToLower(), searchName.ToLower()) <= 2)
            .Select(s => s.Name)
            .Distinct()
            .Take(5)
            .ToArray();

        return await Task.FromResult(similar);
    }

    private static int LevenshteinDistance(string a, string b)
    {
        if (string.IsNullOrEmpty(a)) return b?.Length ?? 0;
        if (string.IsNullOrEmpty(b)) return a.Length;

        var lengthA = a.Length;
        var lengthB = b.Length;
        var distances = new int[lengthA + 1, lengthB + 1];

        for (int i = 0; i <= lengthA; distances[i, 0] = i++) { }
        for (int j = 0; j <= lengthB; distances[0, j] = j++) { }

        for (int i = 1; i <= lengthA; i++)
        {
            for (int j = 1; j <= lengthB; j++)
            {
                var cost = b[j - 1] == a[i - 1] ? 0 : 1;
                distances[i, j] = Math.Min(
                    Math.Min(distances[i - 1, j] + 1, distances[i, j - 1] + 1),
                    distances[i - 1, j - 1] + cost);
            }
        }

        return distances[lengthA, lengthB];
    }

    private static int EstimateTokens(SymbolInfo[] symbols)
    {
        var totalLength = symbols.Sum(s => 
            (s.Name?.Length ?? 0) + 
            (s.Namespace?.Length ?? 0) + 
            (s.Signature?.Length ?? 0) + 
            (s.Documentation?.Length ?? 0) + 
            50); // Base overhead per symbol

        return totalLength / 4; // Rough tokens estimation
    }

    private static SymbolInfo[] OptimizeForTokens(SymbolInfo[] symbols, int maxTokens)
    {
        // Priority order: exact matches, then by symbol type importance
        var prioritized = symbols
            .OrderBy(s => s.SymbolType == SymbolType.Class ? 0 : 
                         s.SymbolType == SymbolType.Interface ? 1 : 
                         s.SymbolType == SymbolType.Method ? 2 : 3)
            .ThenBy(s => s.Name)
            .ToList();

        var result = new List<SymbolInfo>();
        var currentTokens = 0;

        foreach (var symbol in prioritized)
        {
            var symbolTokens = EstimateTokens(new[] { symbol });
            if (currentTokens + symbolTokens <= maxTokens)
            {
                result.Add(symbol);
                currentTokens += symbolTokens;
            }
            else
            {
                break;
            }
        }

        return result.ToArray();
    }
}