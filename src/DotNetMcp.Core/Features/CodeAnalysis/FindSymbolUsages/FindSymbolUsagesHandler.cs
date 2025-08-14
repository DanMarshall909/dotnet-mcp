using System.IO.Abstractions;
using DotNetMcp.Core.Common;
using DotNetMcp.Core.Services;
using DotNetMcp.Core.SharedKernel;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Extensions.Logging;

namespace DotNetMcp.Core.Features.CodeAnalysis;

/// <summary>
/// Handler for finding symbol usages in a codebase
/// </summary>
public class FindSymbolUsagesHandler : BaseHandler<FindSymbolUsagesCommand, FindSymbolUsagesResponse>
{
    private readonly IFileSystem _fileSystem;
    private readonly BuildValidationService _buildValidationService;
    private readonly CompilationService _compilationService;

    public FindSymbolUsagesHandler(ILogger<FindSymbolUsagesHandler> logger, IFileSystem fileSystem, BuildValidationService buildValidationService, CompilationService compilationService) 
        : base(logger)
    {
        _fileSystem = fileSystem;
        _buildValidationService = buildValidationService;
        _compilationService = compilationService;
    }

    protected override async Task<Result<FindSymbolUsagesResponse>> HandleAsync(FindSymbolUsagesCommand request, CancellationToken cancellationToken)
    {
        try
        {
            // Validate project path exists
            if (!_fileSystem.Directory.Exists(request.ProjectPath))
            {
                return Result<FindSymbolUsagesResponse>.Failure($"Project path not found: {request.ProjectPath}");
            }

            // Validate build before attempting Roslyn analysis
            var buildValidation = await _buildValidationService.ValidateBuildAsync(request.ProjectPath, cancellationToken);
            if (!buildValidation.IsSuccess && !buildValidation.IsWarning)
            {
                return Result<FindSymbolUsagesResponse>.Failure(
                    $"Cannot perform symbol usage analysis due to build errors: {buildValidation.Message}\n\n{buildValidation.ErrorSummary}");
            }

            // Continue with existing validation
            if (!_fileSystem.Directory.Exists(request.ProjectPath))
            {
                return Result<FindSymbolUsagesResponse>.Failure($"Project path not found: {request.ProjectPath}");
            }

            // Find all C# files
            var csharpFiles = _fileSystem.Directory
                .GetFiles(request.ProjectPath, "*.cs", SearchOption.AllDirectories)
                .Where(f => !f.Contains("bin") && !f.Contains("obj"))
                .ToArray();

            if (!csharpFiles.Any())
            {
                return Result<FindSymbolUsagesResponse>.Failure("No C# files found in the project path");
            }

            // Find symbol declaration first
            SymbolDeclaration? declaration = null;
            if (request.IncludeDeclaration)
            {
                declaration = await FindSymbolDeclaration(csharpFiles, request.SymbolName, request.SymbolType, request.SymbolNamespace);
            }

            // Find all usages
            var allUsages = new List<SymbolUsage>();
            var usagesByFileDict = new Dictionary<string, List<SymbolUsage>>();

            foreach (var filePath in csharpFiles)
            {
                var usages = await FindSymbolUsagesInFile(filePath, request.SymbolName, request.SymbolType, request.SymbolNamespace, declaration);
                allUsages.AddRange(usages);

                if (usages.Any())
                {
                    usagesByFileDict[filePath] = usages;
                }
            }

            // Apply result limiting
            var totalFound = allUsages.Count;
            var usagesToShow = allUsages.Take(request.MaxResults).ToArray();

            // Group by file if requested
            var usagesByFile = Array.Empty<UsagesByFile>();
            if (request.GroupByFile)
            {
                usagesByFile = CreateUsagesByFile(usagesByFileDict, declaration);
            }

            // Create summary
            var summary = CreateSummary(allUsages, declaration, usagesByFileDict);

            // Apply token optimization if requested
            var estimatedTokens = EstimateTokens(usagesToShow, usagesByFile, declaration);
            var summarizationApplied = false;

            if (request.OptimizeForTokens && estimatedTokens > request.MaxTokens)
            {
                var optimized = OptimizeForTokens(usagesToShow, usagesByFile, declaration, request.MaxTokens);
                usagesToShow = optimized.usages;
                usagesByFile = optimized.usagesByFile;
                estimatedTokens = EstimateTokens(usagesToShow, usagesByFile, declaration);
                summarizationApplied = true;
            }

            var response = new FindSymbolUsagesResponse
            {
                Declaration = declaration,
                Usages = usagesToShow,
                UsagesByFile = usagesByFile,
                Summary = summary,
                EstimatedTokens = estimatedTokens,
                SummarizationApplied = summarizationApplied
            };

            return Result<FindSymbolUsagesResponse>.Success(response);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error finding symbol usages for {SymbolName} in {ProjectPath}", request.SymbolName, request.ProjectPath);
            return Result<FindSymbolUsagesResponse>.Failure($"Error finding symbol usages: {ex.Message}", ex);
        }
    }

    private async Task<SymbolDeclaration?> FindSymbolDeclaration(string[] csharpFiles, string symbolName, SymbolType symbolType, string? symbolNamespace)
    {
        foreach (var filePath in csharpFiles)
        {
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
                    continue;
                }

                // Find declaration based on symbol type
                var declarationNode = FindDeclarationNode(root, symbolName, symbolType, symbolNamespace);
                if (declarationNode != null)
                {
                    var symbol = semanticModel.GetDeclaredSymbol(declarationNode);
                    if (symbol != null)
                    {
                        return CreateSymbolDeclaration(declarationNode, symbol, filePath);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Error parsing file {FilePath}", filePath);
            }
        }

        return null;
    }

    private async Task<List<SymbolUsage>> FindSymbolUsagesInFile(string filePath, string symbolName, SymbolType symbolType, string? symbolNamespace, SymbolDeclaration? declaration)
    {
        var usages = new List<SymbolUsage>();

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
                return usages;
            }
            var lines = content.Split('\n');

            // Find all identifiers matching the symbol name
            var identifiers = root.DescendantTokens()
                .Where(token => token.IsKind(SyntaxKind.IdentifierToken) && 
                               token.ValueText == symbolName)
                .ToArray();

            foreach (var identifier in identifiers)
            {
                var usage = CreateSymbolUsage(identifier, semanticModel, lines, filePath, declaration);
                if (usage != null)
                {
                    usages.Add(usage);
                }
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Error finding usages in file {FilePath}", filePath);
        }

        return usages;
    }

    private static SyntaxNode? FindDeclarationNode(SyntaxNode root, string symbolName, SymbolType symbolType, string? symbolNamespace)
    {
        return symbolType switch
        {
            SymbolType.Class => root.DescendantNodes()
                .OfType<ClassDeclarationSyntax>()
                .FirstOrDefault(c => c.Identifier.ValueText == symbolName &&
                                   (symbolNamespace == null || GetContainingNamespace(c) == symbolNamespace)),

            SymbolType.Interface => root.DescendantNodes()
                .OfType<InterfaceDeclarationSyntax>()
                .FirstOrDefault(i => i.Identifier.ValueText == symbolName &&
                                   (symbolNamespace == null || GetContainingNamespace(i) == symbolNamespace)),

            SymbolType.Method => root.DescendantNodes()
                .OfType<MethodDeclarationSyntax>()
                .FirstOrDefault(m => m.Identifier.ValueText == symbolName),

            SymbolType.Property => root.DescendantNodes()
                .OfType<PropertyDeclarationSyntax>()
                .FirstOrDefault(p => p.Identifier.ValueText == symbolName),

            SymbolType.Field => root.DescendantNodes()
                .OfType<FieldDeclarationSyntax>()
                .SelectMany(f => f.Declaration.Variables)
                .FirstOrDefault(v => v.Identifier.ValueText == symbolName),

            _ => root.DescendantNodes()
                .Where(n => HasIdentifier(n, symbolName))
                .FirstOrDefault()
        };
    }

    private static SymbolDeclaration CreateSymbolDeclaration(SyntaxNode declarationNode, ISymbol symbol, string filePath)
    {
        var linePosition = declarationNode.GetLocation().GetLineSpan().StartLinePosition;
        var containingClass = GetContainingClassName(declarationNode);

        return new SymbolDeclaration
        {
            Name = symbol.Name,
            SymbolType = GetSymbolTypeFromSymbol(symbol),
            Namespace = symbol.ContainingNamespace?.ToDisplayString() ?? "Global",
            FilePath = filePath,
            LineNumber = linePosition.Line + 1,
            Signature = symbol.ToDisplayString(),
            AccessModifier = GetAccessModifier(symbol.DeclaredAccessibility),
            IsStatic = symbol.IsStatic,
            IsAbstract = symbol.IsAbstract,
            IsVirtual = symbol.IsVirtual,
            Documentation = symbol.GetDocumentationCommentXml(),
            ContainingClass = containingClass
        };
    }

    private SymbolUsage? CreateSymbolUsage(SyntaxToken identifier, SemanticModel semanticModel, string[] lines, string filePath, SymbolDeclaration? declaration)
    {
        try
        {
            var linePosition = identifier.GetLocation().GetLineSpan().StartLinePosition;
            var lineNumber = linePosition.Line + 1;
            var columnNumber = linePosition.Character + 1;

            // Get the context (line of code)
            var context = lineNumber <= lines.Length ? lines[lineNumber - 1].Trim() : "";

            // Determine usage type
            var usageType = DetermineUsageType(identifier);

            // Get containing method and class
            var containingMethod = GetContainingMethodName(identifier.Parent) ?? "Global";
            var containingClass = GetContainingClassName(identifier.Parent) ?? "Global";

            // Check if usage is in same class/namespace as declaration
            var isInSameClass = declaration?.ContainingClass == containingClass;
            var isInSameNamespace = declaration?.Namespace == GetContainingNamespace(identifier.Parent);

            return new SymbolUsage
            {
                FilePath = filePath,
                LineNumber = lineNumber,
                ColumnNumber = columnNumber,
                UsageType = usageType,
                Context = context,
                ContainingMethod = containingMethod,
                ContainingClass = containingClass,
                IsInSameClass = isInSameClass,
                IsInSameNamespace = isInSameNamespace == true
            };
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Error creating symbol usage for identifier in {FilePath}", filePath);
            return null;
        }
    }

    private static UsageType DetermineUsageType(SyntaxToken identifier)
    {
        var parent = identifier.Parent;
        
        return parent switch
        {
            ClassDeclarationSyntax => UsageType.Declaration,
            InterfaceDeclarationSyntax => UsageType.Declaration,
            MethodDeclarationSyntax => UsageType.Declaration,
            PropertyDeclarationSyntax => UsageType.Declaration,
            VariableDeclaratorSyntax => UsageType.Declaration,
            InvocationExpressionSyntax => UsageType.MethodCall,
            MemberAccessExpressionSyntax memberAccess when memberAccess.Name.Identifier == identifier => 
                DetermineMemberAccessType(memberAccess),
            ObjectCreationExpressionSyntax => UsageType.Constructor,
            ParameterSyntax => UsageType.Parameter,
            AssignmentExpressionSyntax assignment when assignment.Left.DescendantTokens().Contains(identifier) => 
                UsageType.Assignment,
            ReturnStatementSyntax => UsageType.Return,
            LocalDeclarationStatementSyntax => UsageType.LocalVariable,
            BaseListSyntax => UsageType.Inheritance,
            _ => UsageType.TypeReference
        };
    }

    private static UsageType DetermineMemberAccessType(MemberAccessExpressionSyntax memberAccess)
    {
        var grandParent = memberAccess.Parent;
        
        return grandParent switch
        {
            InvocationExpressionSyntax => UsageType.MethodCall,
            AssignmentExpressionSyntax => UsageType.PropertyAccess,
            _ => UsageType.PropertyAccess
        };
    }

    private UsagesByFile[] CreateUsagesByFile(Dictionary<string, List<SymbolUsage>> usagesByFileDict, SymbolDeclaration? declaration)
    {
        return usagesByFileDict.Select(kvp =>
        {
            var filePath = kvp.Key;
            var usages = kvp.Value.ToArray();
            var fileName = _fileSystem.Path.GetFileName(filePath);

            var statistics = new UsageStatistics
            {
                TotalUsages = usages.Length,
                MethodCalls = usages.Count(u => u.UsageType == UsageType.MethodCall),
                PropertyAccesses = usages.Count(u => u.UsageType == UsageType.PropertyAccess),
                TypeReferences = usages.Count(u => u.UsageType == UsageType.TypeReference),
                Assignments = usages.Count(u => u.UsageType == UsageType.Assignment),
                HasDeclaration = declaration?.FilePath == filePath,
                UsageDensity = CalculateUsageDensity(filePath, usages.Length)
            };

            return new UsagesByFile
            {
                FilePath = filePath,
                FileName = fileName,
                Usages = usages,
                Statistics = statistics
            };
        }).ToArray();
    }

    private FindSymbolUsagesSummary CreateSummary(List<SymbolUsage> allUsages, SymbolDeclaration? declaration, Dictionary<string, List<SymbolUsage>> usagesByFile)
    {
        var totalUsages = allUsages.Count;
        var filesWithUsages = usagesByFile.Count;
        var namespacesWithUsages = allUsages.Select(u => GetNamespaceFromUsage(u)).Distinct().Count();

        // Analyze project distribution
        var projectNames = usagesByFile.Keys
            .Select(filePath => ExtractProjectName(filePath))
            .Where(name => !string.IsNullOrEmpty(name))
            .Distinct()
            .ToArray();

        var projectsWithUsages = projectNames.Length;
        var isSolutionWide = projectsWithUsages > 1;

        var usageTypes = allUsages.Select(u => u.UsageType).Distinct().ToArray();

        var distribution = new UsageDistribution
        {
            SameClass = allUsages.Count(u => u.IsInSameClass),
            SameNamespace = allUsages.Count(u => u.IsInSameNamespace),
            DifferentNamespace = allUsages.Count(u => !u.IsInSameNamespace),
            ExternalProjects = Math.Max(0, projectsWithUsages - 1) // Projects other than the main one
        };

        var heaviestFiles = usagesByFile
            .OrderByDescending(kvp => kvp.Value.Count)
            .Take(5)
            .Select(kvp => $"{_fileSystem.Path.GetFileName(kvp.Key)} ({kvp.Value.Count} usages)")
            .ToArray();

        var impactAnalysis = GenerateImpactAnalysis(allUsages, declaration, isSolutionWide, projectsWithUsages);

        return new FindSymbolUsagesSummary
        {
            TotalUsages = totalUsages,
            FilesWithUsages = filesWithUsages,
            NamespacesWithUsages = namespacesWithUsages,
            ProjectsWithUsages = projectsWithUsages,
            ProjectNames = projectNames,
            UsageTypes = usageTypes,
            Distribution = distribution,
            HeaviestFiles = heaviestFiles,
            ImpactAnalysis = impactAnalysis,
            IsSolutionWide = isSolutionWide
        };
    }

    private double CalculateUsageDensity(string filePath, int usageCount)
    {
        try
        {
            var content = _fileSystem.File.ReadAllText(filePath);
            var lineCount = content.Split('\n').Length;
            return (usageCount / (double)lineCount) * 100;
        }
        catch
        {
            return 0.0;
        }
    }

    private static string GetNamespaceFromUsage(SymbolUsage usage)
    {
        // Extract namespace from the containing class (simplified)
        var parts = usage.ContainingClass.Split('.');
        return parts.Length > 1 ? string.Join(".", parts.Take(parts.Length - 1)) : "Global";
    }

    private static string[] GenerateImpactAnalysis(List<SymbolUsage> allUsages, SymbolDeclaration? declaration, bool isSolutionWide, int projectCount)
    {
        var insights = new List<string>();

        if (isSolutionWide)
        {
            insights.Add($"Solution-wide usage across {projectCount} projects - coordinate changes carefully");
        }

        if (allUsages.Count > 50)
        {
            insights.Add("High usage count - changes may have significant impact");
        }

        var externalUsages = allUsages.Count(u => !u.IsInSameClass);
        if (externalUsages > 20)
        {
            insights.Add($"Used across {externalUsages} external locations - consider stability");
        }

        var methodCalls = allUsages.Count(u => u.UsageType == UsageType.MethodCall);
        if (methodCalls > 10)
        {
            insights.Add("Frequently called method - performance impact should be considered");
        }

        if (declaration?.AccessModifier == AccessModifier.Public && allUsages.Count > 5)
        {
            insights.Add("Public API with multiple usages - breaking changes should be avoided");
        }

        return insights.ToArray();
    }

    private static string ExtractProjectName(string filePath)
    {
        try
        {
            // Extract project name from path like /path/to/ProjectName/file.cs
            var parts = filePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
            
            // Look for common .NET project patterns
            for (int i = 0; i < parts.Length - 1; i++)
            {
                var part = parts[i];
                
                // Check if this looks like a project directory
                if (part.Contains('.') && !part.EndsWith(".cs") && 
                    (part.StartsWith("src") == false && part.StartsWith("test") == false))
                {
                    return part;
                }
                
                // Alternative: look for src/test structure
                if (i > 0 && (parts[i-1] == "src" || parts[i-1] == "test"))
                {
                    return part;
                }
            }
            
            return "";
        }
        catch
        {
            return "";
        }
    }

    private static bool HasIdentifier(SyntaxNode node, string identifier)
    {
        return node switch
        {
            ClassDeclarationSyntax cls => cls.Identifier.ValueText == identifier,
            InterfaceDeclarationSyntax iface => iface.Identifier.ValueText == identifier,
            MethodDeclarationSyntax method => method.Identifier.ValueText == identifier,
            PropertyDeclarationSyntax prop => prop.Identifier.ValueText == identifier,
            VariableDeclaratorSyntax var => var.Identifier.ValueText == identifier,
            _ => false
        };
    }

    private static string? GetContainingClassName(SyntaxNode? node)
    {
        return node?.Ancestors().OfType<ClassDeclarationSyntax>().FirstOrDefault()?.Identifier.ValueText;
    }

    private static string? GetContainingMethodName(SyntaxNode? node)
    {
        return node?.Ancestors().OfType<MethodDeclarationSyntax>().FirstOrDefault()?.Identifier.ValueText;
    }

    private static string? GetContainingNamespace(SyntaxNode? node)
    {
        var namespaceNode = node?.Ancestors().OfType<NamespaceDeclarationSyntax>().FirstOrDefault();
        if (namespaceNode != null)
        {
            return namespaceNode.Name.ToString();
        }

        var fileScopedNamespace = node?.Ancestors().OfType<FileScopedNamespaceDeclarationSyntax>().FirstOrDefault();
        return fileScopedNamespace?.Name.ToString();
    }

    private static SymbolType GetSymbolTypeFromSymbol(ISymbol symbol)
    {
        return symbol switch
        {
            INamedTypeSymbol namedType when namedType.TypeKind == TypeKind.Class => SymbolType.Class,
            INamedTypeSymbol namedType when namedType.TypeKind == TypeKind.Interface => SymbolType.Interface,
            IMethodSymbol => SymbolType.Method,
            IPropertySymbol => SymbolType.Property,
            IFieldSymbol => SymbolType.Field,
            _ => SymbolType.Any
        };
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

    private static int EstimateTokens(SymbolUsage[] usages, UsagesByFile[] usagesByFile, SymbolDeclaration? declaration)
    {
        var usageTokens = usages.Sum(u => u.Context.Length + u.ContainingMethod.Length + u.ContainingClass.Length + 50);
        var fileTokens = usagesByFile.Sum(f => f.FilePath.Length + f.Usages.Length * 30);
        var declarationTokens = declaration != null ? declaration.Signature.Length + 100 : 0;
        
        return usageTokens + fileTokens + declarationTokens + 300; // Base overhead
    }

    private static (SymbolUsage[] usages, UsagesByFile[] usagesByFile) OptimizeForTokens(
        SymbolUsage[] usages, UsagesByFile[] usagesByFile, SymbolDeclaration? declaration, int maxTokens)
    {
        // Prioritize usages by importance
        var prioritizedUsages = usages
            .OrderBy(u => u.UsageType == UsageType.Declaration ? 0 :
                         u.UsageType == UsageType.MethodCall ? 1 :
                         u.UsageType == UsageType.Constructor ? 2 : 3)
            .ThenByDescending(u => u.IsInSameClass ? 0 : 1)
            .Take(50)
            .ToArray();

        // Keep top files with most usages
        var prioritizedFileUsages = usagesByFile
            .OrderByDescending(f => f.Statistics.TotalUsages)
            .Take(10)
            .ToArray();

        return (prioritizedUsages, prioritizedFileUsages);
    }
}