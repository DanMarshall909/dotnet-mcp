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
/// Handler for getting comprehensive class context
/// </summary>
public class GetClassContextHandler : BaseHandler<GetClassContextCommand, GetClassContextResponse>
{
    private readonly IFileSystem _fileSystem;
    private readonly BuildValidationService _buildValidationService;

    public GetClassContextHandler(ILogger<GetClassContextHandler> logger, IFileSystem fileSystem, BuildValidationService buildValidationService) 
        : base(logger)
    {
        _fileSystem = fileSystem;
        _buildValidationService = buildValidationService;
    }

    protected override async Task<Result<GetClassContextResponse>> HandleAsync(GetClassContextCommand request, CancellationToken cancellationToken)
    {
        try
        {
            // Validate project path exists
            if (!_fileSystem.Directory.Exists(request.ProjectPath))
            {
                return Result<GetClassContextResponse>.Failure($"Project path not found: {request.ProjectPath}");
            }

            // Validate build before attempting Roslyn analysis
            var buildValidation = await _buildValidationService.ValidateBuildAsync(request.ProjectPath, cancellationToken);
            if (!buildValidation.IsSuccess && !buildValidation.IsWarning)
            {
                return Result<GetClassContextResponse>.Failure(
                    $"Cannot perform class context analysis due to build errors: {buildValidation.Message}\n\n{buildValidation.ErrorSummary}");
            }

            // Continue with existing validation
            if (!_fileSystem.Directory.Exists(request.ProjectPath))
            {
                return Result<GetClassContextResponse>.Failure($"Project path not found: {request.ProjectPath}");
            }

            // Find all C# files
            var csharpFiles = _fileSystem.Directory
                .GetFiles(request.ProjectPath, "*.cs", SearchOption.AllDirectories)
                .Where(f => !f.Contains("bin") && !f.Contains("obj"))
                .ToArray();

            if (!csharpFiles.Any())
            {
                return Result<GetClassContextResponse>.Failure("No C# files found in the project path");
            }

            // Find the main class
            var mainClass = await FindMainClass(request.ClassName, csharpFiles);
            if (mainClass == null)
            {
                var suggestions = await FindSimilarClassNames(request.ClassName, csharpFiles);
                var errorMessage = $"Class '{request.ClassName}' not found";
                if (suggestions.Any())
                {
                    errorMessage += $". Did you mean: {string.Join(", ", suggestions)}?";
                }
                return Result<GetClassContextResponse>.Failure(errorMessage);
            }

            // Analyze dependencies
            var dependencies = request.IncludeDependencies 
                ? await AnalyzeDependencies(mainClass, csharpFiles, request.MaxDepth)
                : Array.Empty<ClassInfo>();

            // Analyze usages
            var usages = request.IncludeUsages 
                ? await AnalyzeUsages(mainClass, csharpFiles)
                : Array.Empty<UsageInfo>();

            // Analyze inheritance chain
            var inheritanceChain = request.IncludeInheritance 
                ? await AnalyzeInheritanceChain(mainClass, csharpFiles)
                : Array.Empty<ClassInfo>();

            // Analyze test context
            var testContext = request.IncludeTestContext 
                ? await AnalyzeTestContext(mainClass, csharpFiles)
                : null;

            // Create summary
            var summary = CreateSummary(mainClass, dependencies, usages, inheritanceChain);

            // Apply token optimization if requested
            var estimatedTokens = EstimateTokens(mainClass, dependencies, usages, inheritanceChain);
            var summarizationApplied = false;

            if (request.OptimizeForTokens && estimatedTokens > request.MaxTokens)
            {
                var optimized = OptimizeForTokens(mainClass, dependencies, usages, inheritanceChain, request.MaxTokens);
                dependencies = optimized.dependencies;
                usages = optimized.usages;
                inheritanceChain = optimized.inheritanceChain;
                estimatedTokens = EstimateTokens(mainClass, dependencies, usages, inheritanceChain);
                summarizationApplied = true;
            }

            var response = new GetClassContextResponse
            {
                MainClass = mainClass,
                Dependencies = dependencies,
                Usages = usages,
                InheritanceChain = inheritanceChain,
                Summary = summary with { 
                    SummarizationApplied = summarizationApplied,
                    EstimatedTokens = estimatedTokens
                },
                TestContext = testContext,
                EstimatedTokens = estimatedTokens,
                SummarizationApplied = summarizationApplied
            };

            return Result<GetClassContextResponse>.Success(response);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error getting class context for {ClassName} in {ProjectPath}", request.ClassName, request.ProjectPath);
            return Result<GetClassContextResponse>.Failure($"Error analyzing class context: {ex.Message}", ex);
        }
    }

    private async Task<ClassInfo?> FindMainClass(string className, string[] csharpFiles)
    {
        foreach (var filePath in csharpFiles)
        {
            try
            {
                var content = await _fileSystem.File.ReadAllTextAsync(filePath);
                var syntaxTree = CSharpSyntaxTree.ParseText(content);
                var root = await syntaxTree.GetRootAsync();

                var classNode = root.DescendantNodes()
                    .OfType<ClassDeclarationSyntax>()
                    .FirstOrDefault(c => c.Identifier.ValueText == className);

                if (classNode != null)
                {
                    return await CreateClassInfo(classNode, filePath, syntaxTree);
                }

                // Also check interfaces
                var interfaceNode = root.DescendantNodes()
                    .OfType<InterfaceDeclarationSyntax>()
                    .FirstOrDefault(i => i.Identifier.ValueText == className);

                if (interfaceNode != null)
                {
                    return await CreateClassInfoFromInterface(interfaceNode, filePath, syntaxTree);
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Error parsing file {FilePath}", filePath);
            }
        }

        return null;
    }

    private async Task<ClassInfo> CreateClassInfo(ClassDeclarationSyntax classNode, string filePath, SyntaxTree syntaxTree)
    {
        var compilation = CSharpCompilation.Create(
            "TempAssembly",
            syntaxTrees: new[] { syntaxTree },
            references: GetBasicReferences());

        var semanticModel = compilation.GetSemanticModel(syntaxTree);
        var symbol = semanticModel.GetDeclaredSymbol(classNode);

        var linePosition = classNode.GetLocation().GetLineSpan().StartLinePosition;

        // Extract methods
        var methods = classNode.Members
            .OfType<MethodDeclarationSyntax>()
            .Select(m => CreateMethodInfo(m, semanticModel))
            .Where(m => m != null)
            .Cast<MethodInfo>()
            .ToArray();

        // Extract properties
        var properties = classNode.Members
            .OfType<PropertyDeclarationSyntax>()
            .Select(p => CreatePropertyInfo(p))
            .ToArray();

        // Extract fields
        var fields = classNode.Members
            .OfType<FieldDeclarationSyntax>()
            .SelectMany(f => f.Declaration.Variables.Select(v => CreateFieldInfo(v, f)))
            .ToArray();

        return new ClassInfo
        {
            Name = classNode.Identifier.ValueText,
            Namespace = GetNamespace(classNode),
            FilePath = filePath,
            LineNumber = linePosition.Line + 1,
            BaseClass = classNode.BaseList?.Types.FirstOrDefault()?.Type.ToString(),
            Interfaces = classNode.BaseList?.Types.Skip(1).Select(t => t.Type.ToString()).ToArray(),
            IsInterface = false,
            IsAbstract = classNode.Modifiers.Any(m => m.IsKind(SyntaxKind.AbstractKeyword)),
            IsStatic = classNode.Modifiers.Any(m => m.IsKind(SyntaxKind.StaticKeyword)),
            AccessModifier = GetAccessModifier(classNode.Modifiers),
            Methods = methods,
            Properties = properties,
            Fields = fields,
            Documentation = symbol?.GetDocumentationCommentXml(),
            Attributes = classNode.AttributeLists.SelectMany(al => al.Attributes.Select(a => a.Name.ToString())).ToArray()
        };
    }

    private async Task<ClassInfo> CreateClassInfoFromInterface(InterfaceDeclarationSyntax interfaceNode, string filePath, SyntaxTree syntaxTree)
    {
        var compilation = CSharpCompilation.Create(
            "TempAssembly",
            syntaxTrees: new[] { syntaxTree },
            references: GetBasicReferences());

        var semanticModel = compilation.GetSemanticModel(syntaxTree);
        var symbol = semanticModel.GetDeclaredSymbol(interfaceNode);

        var linePosition = interfaceNode.GetLocation().GetLineSpan().StartLinePosition;

        // Extract methods
        var methods = interfaceNode.Members
            .OfType<MethodDeclarationSyntax>()
            .Select(m => CreateMethodInfo(m, semanticModel))
            .Where(m => m != null)
            .Cast<MethodInfo>()
            .ToArray();

        // Extract properties
        var properties = interfaceNode.Members
            .OfType<PropertyDeclarationSyntax>()
            .Select(p => CreatePropertyInfo(p))
            .ToArray();

        return new ClassInfo
        {
            Name = interfaceNode.Identifier.ValueText,
            Namespace = GetNamespace(interfaceNode),
            FilePath = filePath,
            LineNumber = linePosition.Line + 1,
            BaseClass = null,
            Interfaces = interfaceNode.BaseList?.Types.Select(t => t.Type.ToString()).ToArray(),
            IsInterface = true,
            IsAbstract = false,
            IsStatic = false,
            AccessModifier = GetAccessModifier(interfaceNode.Modifiers),
            Methods = methods,
            Properties = properties,
            Fields = Array.Empty<FieldInfo>(),
            Documentation = symbol?.GetDocumentationCommentXml(),
            Attributes = interfaceNode.AttributeLists.SelectMany(al => al.Attributes.Select(a => a.Name.ToString())).ToArray()
        };
    }

    private MethodInfo? CreateMethodInfo(MethodDeclarationSyntax method, SemanticModel semanticModel)
    {
        try
        {
            var linePosition = method.GetLocation().GetLineSpan().StartLinePosition;
            var symbol = semanticModel.GetDeclaredSymbol(method);

            var parameters = method.ParameterList.Parameters
                .Select(p => new ParameterInfo
                {
                    Name = p.Identifier.ValueText,
                    Type = p.Type?.ToString() ?? "unknown",
                    DefaultValue = p.Default?.Value.ToString(),
                    IsOptional = p.Default != null
                })
                .ToArray();

            return new MethodInfo
            {
                Name = method.Identifier.ValueText,
                ReturnType = method.ReturnType.ToString(),
                Parameters = parameters,
                AccessModifier = GetAccessModifier(method.Modifiers),
                IsStatic = method.Modifiers.Any(m => m.IsKind(SyntaxKind.StaticKeyword)),
                IsVirtual = method.Modifiers.Any(m => m.IsKind(SyntaxKind.VirtualKeyword)),
                IsOverride = method.Modifiers.Any(m => m.IsKind(SyntaxKind.OverrideKeyword)),
                IsAbstract = method.Modifiers.Any(m => m.IsKind(SyntaxKind.AbstractKeyword)),
                LineNumber = linePosition.Line + 1,
                Documentation = symbol?.GetDocumentationCommentXml()
            };
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Error creating method info for {MethodName}", method.Identifier.ValueText);
            return null;
        }
    }

    private PropertyInfo CreatePropertyInfo(PropertyDeclarationSyntax property)
    {
        var linePosition = property.GetLocation().GetLineSpan().StartLinePosition;

        return new PropertyInfo
        {
            Name = property.Identifier.ValueText,
            Type = property.Type.ToString(),
            AccessModifier = GetAccessModifier(property.Modifiers),
            HasGetter = property.AccessorList?.Accessors.Any(a => a.IsKind(SyntaxKind.GetAccessorDeclaration)) ?? false,
            HasSetter = property.AccessorList?.Accessors.Any(a => a.IsKind(SyntaxKind.SetAccessorDeclaration)) ?? false,
            IsStatic = property.Modifiers.Any(m => m.IsKind(SyntaxKind.StaticKeyword)),
            LineNumber = linePosition.Line + 1
        };
    }

    private FieldInfo CreateFieldInfo(VariableDeclaratorSyntax variable, FieldDeclarationSyntax field)
    {
        var linePosition = variable.GetLocation().GetLineSpan().StartLinePosition;

        return new FieldInfo
        {
            Name = variable.Identifier.ValueText,
            Type = field.Declaration.Type.ToString(),
            AccessModifier = GetAccessModifier(field.Modifiers),
            IsStatic = field.Modifiers.Any(m => m.IsKind(SyntaxKind.StaticKeyword)),
            IsReadOnly = field.Modifiers.Any(m => m.IsKind(SyntaxKind.ReadOnlyKeyword)),
            IsConst = field.Modifiers.Any(m => m.IsKind(SyntaxKind.ConstKeyword)),
            LineNumber = linePosition.Line + 1,
            DefaultValue = variable.Initializer?.Value.ToString()
        };
    }

    private async Task<ClassInfo[]> AnalyzeDependencies(ClassInfo mainClass, string[] csharpFiles, int maxDepth)
    {
        // This is a simplified implementation - would need more sophisticated dependency analysis
        var dependencies = new List<ClassInfo>();
        
        // Extract type references from the main class
        // This would require semantic analysis to find all referenced types
        
        return await Task.FromResult(dependencies.ToArray());
    }

    private async Task<UsageInfo[]> AnalyzeUsages(ClassInfo mainClass, string[] csharpFiles)
    {
        var usages = new List<UsageInfo>();

        foreach (var filePath in csharpFiles)
        {
            try
            {
                var content = await _fileSystem.File.ReadAllTextAsync(filePath);
                var syntaxTree = CSharpSyntaxTree.ParseText(content);
                var root = await syntaxTree.GetRootAsync();

                // Find constructor parameter usages
                var constructorParams = root.DescendantNodes()
                    .OfType<ParameterSyntax>()
                    .Where(p => p.Type?.ToString().Contains(mainClass.Name) == true);

                foreach (var param in constructorParams)
                {
                    var linePosition = param.GetLocation().GetLineSpan().StartLinePosition;
                    usages.Add(new UsageInfo
                    {
                        FilePath = filePath,
                        LineNumber = linePosition.Line + 1,
                        UsageType = "Constructor Parameter",
                        Context = param.Parent?.Parent?.ToString() ?? "",
                        ClassName = GetContainingClassName(param)
                    });
                }

                // Find field/property declarations
                var fieldUsages = root.DescendantNodes()
                    .OfType<VariableDeclaratorSyntax>()
                    .Where(v => v.Parent is VariableDeclarationSyntax vd && 
                               vd.Type.ToString().Contains(mainClass.Name));

                foreach (var field in fieldUsages)
                {
                    var linePosition = field.GetLocation().GetLineSpan().StartLinePosition;
                    usages.Add(new UsageInfo
                    {
                        FilePath = filePath,
                        LineNumber = linePosition.Line + 1,
                        UsageType = "Field Declaration",
                        Context = field.Parent?.ToString() ?? "",
                        ClassName = GetContainingClassName(field)
                    });
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Error analyzing usages in file {FilePath}", filePath);
            }
        }

        return usages.ToArray();
    }

    private async Task<ClassInfo[]> AnalyzeInheritanceChain(ClassInfo mainClass, string[] csharpFiles)
    {
        var inheritanceChain = new List<ClassInfo>();

        if (!string.IsNullOrEmpty(mainClass.BaseClass))
        {
            var baseClass = await FindMainClass(mainClass.BaseClass, csharpFiles);
            if (baseClass != null)
            {
                inheritanceChain.Add(baseClass);
                // Could recursively find the full chain
            }
        }

        return inheritanceChain.ToArray();
    }

    private async Task<TestContextInfo> AnalyzeTestContext(ClassInfo mainClass, string[] csharpFiles)
    {
        var testFiles = csharpFiles.Where(f => f.Contains("Test", StringComparison.OrdinalIgnoreCase)).ToArray();
        var testMethods = new List<TestMethodInfo>();

        foreach (var testFile in testFiles)
        {
            try
            {
                var content = await _fileSystem.File.ReadAllTextAsync(testFile);
                if (content.Contains(mainClass.Name))
                {
                    var syntaxTree = CSharpSyntaxTree.ParseText(content);
                    var root = await syntaxTree.GetRootAsync();

                    var methods = root.DescendantNodes()
                        .OfType<MethodDeclarationSyntax>()
                        .Where(m => m.AttributeLists.Any(al => 
                            al.Attributes.Any(a => a.Name.ToString().Contains("Test") || 
                                                  a.Name.ToString().Contains("Fact"))))
                        .Select(m => new TestMethodInfo
                        {
                            Name = m.Identifier.ValueText,
                            TestClass = GetContainingClassName(m) ?? "",
                            FilePath = testFile,
                            TestAttributes = m.AttributeLists
                                .SelectMany(al => al.Attributes.Select(a => a.Name.ToString()))
                                .ToArray()
                        });

                    testMethods.AddRange(methods);
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Error analyzing test file {FilePath}", testFile);
            }
        }

        return new TestContextInfo
        {
            TestFiles = testFiles,
            TestMethods = testMethods.ToArray(),
            CoverageGaps = Array.Empty<string>(), // Would need actual coverage analysis
            EstimatedCoverage = testMethods.Any() ? 0.5 : 0.0 // Rough estimate
        };
    }

    private ClassContextSummary CreateSummary(ClassInfo mainClass, ClassInfo[] dependencies, UsageInfo[] usages, ClassInfo[] inheritanceChain)
    {
        var keyInsights = new List<string>();

        if (mainClass.IsInterface)
        {
            keyInsights.Add("This is an interface defining a contract");
        }

        if (dependencies.Length > 10)
        {
            keyInsights.Add("High dependency count - consider refactoring");
        }

        if (usages.Length > 20)
        {
            keyInsights.Add("Widely used class - changes may have broad impact");
        }

        if (mainClass.Methods.Length > 15)
        {
            keyInsights.Add("Large number of methods - consider breaking into smaller classes");
        }

        return new ClassContextSummary
        {
            TotalLines = EstimateLineCount(mainClass),
            CoreComplexity = CalculateComplexity(mainClass),
            DependencyCount = dependencies.Length,
            UsageCount = usages.Length,
            KeyInsights = keyInsights.ToArray()
        };
    }

    private static string GetNamespace(SyntaxNode node)
    {
        var namespaceNode = node.Ancestors().OfType<NamespaceDeclarationSyntax>().FirstOrDefault();
        if (namespaceNode != null)
        {
            return namespaceNode.Name.ToString();
        }

        var fileScopedNamespace = node.Ancestors().OfType<FileScopedNamespaceDeclarationSyntax>().FirstOrDefault();
        return fileScopedNamespace?.Name.ToString() ?? "Global";
    }

    private static string? GetContainingClassName(SyntaxNode node)
    {
        var classNode = node.Ancestors().OfType<ClassDeclarationSyntax>().FirstOrDefault();
        return classNode?.Identifier.ValueText;
    }

    private static AccessModifier GetAccessModifier(SyntaxTokenList modifiers)
    {
        if (modifiers.Any(m => m.IsKind(SyntaxKind.PublicKeyword))) return AccessModifier.Public;
        if (modifiers.Any(m => m.IsKind(SyntaxKind.PrivateKeyword))) return AccessModifier.Private;
        if (modifiers.Any(m => m.IsKind(SyntaxKind.ProtectedKeyword))) return AccessModifier.Protected;
        if (modifiers.Any(m => m.IsKind(SyntaxKind.InternalKeyword))) return AccessModifier.Internal;
        return AccessModifier.Private;
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

    private async Task<string[]> FindSimilarClassNames(string className, string[] csharpFiles)
    {
        var allClassNames = new List<string>();

        foreach (var filePath in csharpFiles.Take(10)) // Limit for performance
        {
            try
            {
                var content = await _fileSystem.File.ReadAllTextAsync(filePath);
                var syntaxTree = CSharpSyntaxTree.ParseText(content);
                var root = await syntaxTree.GetRootAsync();

                var names = root.DescendantNodes()
                    .OfType<ClassDeclarationSyntax>()
                    .Select(c => c.Identifier.ValueText)
                    .Concat(root.DescendantNodes()
                        .OfType<InterfaceDeclarationSyntax>()
                        .Select(i => i.Identifier.ValueText));

                allClassNames.AddRange(names);
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Error reading file {FilePath}", filePath);
            }
        }

        return allClassNames
            .Where(name => name.Contains(className, StringComparison.OrdinalIgnoreCase))
            .Distinct()
            .Take(3)
            .ToArray();
    }

    private static int EstimateLineCount(ClassInfo classInfo)
    {
        return classInfo.Methods.Length * 10 + classInfo.Properties.Length * 3 + classInfo.Fields.Length * 2 + 10;
    }

    private static int CalculateComplexity(ClassInfo classInfo)
    {
        return classInfo.Methods.Length + classInfo.Properties.Length / 2;
    }

    private static int EstimateTokens(ClassInfo mainClass, ClassInfo[] dependencies, UsageInfo[] usages, ClassInfo[] inheritanceChain)
    {
        var mainClassTokens = EstimateClassTokens(mainClass);
        var dependencyTokens = dependencies.Sum(EstimateClassTokens);
        var usageTokens = usages.Length * 50; // Rough estimate
        var inheritanceTokens = inheritanceChain.Sum(EstimateClassTokens);

        return mainClassTokens + dependencyTokens + usageTokens + inheritanceTokens;
    }

    private static int EstimateClassTokens(ClassInfo classInfo)
    {
        return (classInfo.Name?.Length ?? 0) + 
               (classInfo.Documentation?.Length ?? 0) + 
               classInfo.Methods.Sum(m => (m.Name?.Length ?? 0) + (m.Documentation?.Length ?? 0)) +
               classInfo.Properties.Sum(p => p.Name?.Length ?? 0) +
               200; // Base overhead
    }

    private static (ClassInfo[] dependencies, UsageInfo[] usages, ClassInfo[] inheritanceChain) OptimizeForTokens(
        ClassInfo mainClass, ClassInfo[] dependencies, UsageInfo[] usages, ClassInfo[] inheritanceChain, int maxTokens)
    {
        // Prioritize most important dependencies and usages
        var prioritizedDependencies = dependencies
            .OrderByDescending(d => d.Methods.Length + d.Properties.Length)
            .Take(5)
            .ToArray();

        var prioritizedUsages = usages
            .OrderBy(u => u.UsageType == "Constructor Parameter" ? 0 : 1)
            .Take(10)
            .ToArray();

        var prioritizedInheritance = inheritanceChain.Take(3).ToArray();

        return (prioritizedDependencies, prioritizedUsages, prioritizedInheritance);
    }
}