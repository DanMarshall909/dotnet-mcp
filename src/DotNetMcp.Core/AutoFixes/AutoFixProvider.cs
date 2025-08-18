using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DotNetMcp.Core.AutoFixes;

/// <summary>
/// Provides common auto-fixes for repetitive code issues
/// </summary>
public static class AutoFixProvider
{
    /// <summary>
    /// Add missing using statements to source code
    /// </summary>
    public static string AddMissingUsings(string sourceCode, string[] missingNamespaces)
    {
        return UsingStatementFixer.AddMissingUsings(sourceCode, missingNamespaces);
    }

    /// <summary>
    /// Fix nullability warnings in source code
    /// </summary>
    public static string FixNullabilityWarnings(string sourceCode)
    {
        return NullabilityFixer.FixNullableWarnings(sourceCode);
    }

    /// <summary>
    /// Convert methods to async when they contain await
    /// </summary>
    public static string ConvertToAsyncMethod(string sourceCode)
    {
        return AsyncMethodFixer.FixAsyncMethods(sourceCode);
    }

    /// <summary>
    /// Modernize collection usage patterns
    /// </summary>
    public static string ModernizeCollections(string sourceCode)
    {
        var modernizations = new Dictionary<string, string>
        {
            ["ArrayList"] = "List<object>",
            ["Hashtable"] = "Dictionary<object, object>",
            ["new ArrayList()"] = "new List<object>()",
            ["new Hashtable()"] = "new Dictionary<object, object>()"
        };

        var result = sourceCode;
        foreach (var (old, modern) in modernizations)
        {
            result = result.Replace(old, modern);
        }
        return result;
    }

    /// <summary>
    /// Remove unused using statements
    /// </summary>
    public static string RemoveUnusedUsings(string sourceCode)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(sourceCode);
        var root = syntaxTree.GetCompilationUnitRoot();
        
        // Simple heuristic: remove usings that don't appear in the code
        var usings = root.Usings.ToArray();
        var usingsToKeep = new List<UsingDirectiveSyntax>();
        
        foreach (var usingDirective in usings)
        {
            var namespaceName = usingDirective.Name?.ToString();
            if (namespaceName != null)
            {
                // Check if namespace is used in code (simple text search)
                var lastPart = namespaceName.Split('.').Last();
                if (sourceCode.Contains(lastPart) && !IsSystemNamespace(namespaceName))
                {
                    usingsToKeep.Add(usingDirective);
                }
                // Always keep System namespaces that are commonly used
                else if (IsSystemNamespace(namespaceName) && IsUsedSystemNamespace(namespaceName, sourceCode))
                {
                    usingsToKeep.Add(usingDirective);
                }
            }
        }
        
        var newRoot = root.WithUsings(SyntaxFactory.List(usingsToKeep));
        return newRoot.ToFullString();
    }

    private static bool IsSystemNamespace(string namespaceName)
    {
        return namespaceName.StartsWith("System");
    }

    private static bool IsUsedSystemNamespace(string namespaceName, string sourceCode)
    {
        var commonUsages = new Dictionary<string, string[]>
        {
            ["System.Collections.Generic"] = new[] { "List<", "Dictionary<", "IEnumerable<" },
            ["System.Threading.Tasks"] = new[] { "Task<", "Task ", "async ", "await " },
            ["System.Linq"] = new[] { ".Where(", ".Select(", ".FirstOrDefault(" }
        };

        if (commonUsages.TryGetValue(namespaceName, out var patterns))
        {
            return patterns.Any(pattern => sourceCode.Contains(pattern));
        }

        return false;
    }
    /// <summary>
    /// Auto-fix for missing using statements
    /// </summary>
    public static class UsingStatementFixer
    {
        public static string AddMissingUsings(string sourceCode, string[] missingNamespaces)
        {
            var lines = sourceCode.Split('\n');
            var namespaceLineIndex = -1;
            
            // Find where to insert using statements (before namespace declaration or at the top)
            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].Trim().StartsWith("namespace "))
                {
                    namespaceLineIndex = i;
                    break;
                }
            }
            
            var usingStatements = missingNamespaces
                .Select(ns => $"using {ns};")
                .ToArray();
            
            if (namespaceLineIndex >= 0)
            {
                // Insert before namespace
                var newLines = lines.Take(namespaceLineIndex)
                    .Concat(usingStatements)
                    .Concat(new[] { "" }) // Empty line
                    .Concat(lines.Skip(namespaceLineIndex));
                return string.Join('\n', newLines);
            }
            else
            {
                // Insert at the beginning
                var newLines = usingStatements
                    .Concat(new[] { "" }) // Empty line
                    .Concat(lines);
                return string.Join('\n', newLines);
            }
        }
        
        public static Dictionary<string, string[]> CommonMissingUsings = new()
        {
            ["ILogger"] = new[] { "Microsoft.Extensions.Logging" },
            ["IServiceCollection"] = new[] { "Microsoft.Extensions.DependencyInjection" },
            ["JsonObject"] = new[] { "System.Text.Json.Nodes" },
            ["CancellationToken"] = new[] { "System.Threading" },
            ["Task"] = new[] { "System.Threading.Tasks" },
            ["IAsyncEnumerable"] = new[] { "System.Collections.Generic" },
            ["RegexOptions"] = new[] { "System.Text.RegularExpressions" },
            ["StringComparison"] = new[] { "System" }
        };
    }

    /// <summary>
    /// Auto-fix for nullability warnings
    /// </summary>
    public static class NullabilityFixer
    {
        public static string FixNullableWarnings(string sourceCode)
        {
            var fixes = new Dictionary<string, string>
            {
                // Common nullable fixes
                ["string error"] = "string? error",
                ["Exception exception"] = "Exception? exception", 
                ["object data"] = "object? data",
                
                // Null-conditional operators
                [".GetValue<"] = "?.GetValue<",
                [".ToString()"] = "?.ToString()",
                
                // Null coalescing
                [" ?? \"\""] = " ?? \"\"",
                [" ?? Array.Empty"] = " ?? Array.Empty"
            };

            var result = sourceCode;
            foreach (var (pattern, replacement) in fixes)
            {
                result = result.Replace(pattern, replacement);
            }
            
            return result;
        }
    }

    /// <summary>
    /// Auto-fix for async method signatures
    /// </summary>
    public static class AsyncMethodFixer
    {
        public static string FixAsyncMethods(string sourceCode)
        {
            var syntaxTree = CSharpSyntaxTree.ParseText(sourceCode);
            var root = syntaxTree.GetCompilationUnitRoot();
            
            var methodsNeedingAsync = root.DescendantNodes()
                .OfType<MethodDeclarationSyntax>()
                .Where(m => ContainsAwait(m) && !m.Modifiers.Any(SyntaxKind.AsyncKeyword))
                .ToArray();

            var newRoot = root;
            foreach (var method in methodsNeedingAsync)
            {
                var asyncModifier = SyntaxFactory.Token(SyntaxKind.AsyncKeyword);
                var newMethod = method.AddModifiers(asyncModifier);
                newRoot = newRoot.ReplaceNode(method, newMethod);
            }

            return newRoot.ToFullString();
        }

        private static bool ContainsAwait(MethodDeclarationSyntax method)
        {
            return method.DescendantNodes()
                .OfType<AwaitExpressionSyntax>()
                .Any();
        }
    }

    /// <summary>
    /// Auto-fix for dependency injection registration
    /// </summary>
    public static class DIRegistrationFixer
    {
        public static string AddServiceRegistration(string serviceCollectionCode, string serviceType, string? implementationType = null)
        {
            implementationType ??= serviceType;
            
            var registration = implementationType == serviceType
                ? $"services.AddScoped<{serviceType}>();"
                : $"services.AddScoped<{serviceType}, {implementationType}>();";

            // Find the return services; line and insert before it
            var lines = serviceCollectionCode.Split('\n');
            var insertIndex = Array.FindLastIndex(lines, line => line.Trim().StartsWith("return services"));
            
            if (insertIndex > 0)
            {
                var newLines = lines.Take(insertIndex)
                    .Concat(new[] { $"        {registration}" })
                    .Concat(lines.Skip(insertIndex));
                return string.Join('\n', newLines);
            }

            return serviceCollectionCode + $"\n        {registration}";
        }
    }
}

/// <summary>
/// Batch auto-fix processor
/// </summary>
public class AutoFixBatch
{
    private readonly List<AutoFixOperation> _operations = new();

    public AutoFixBatch AddUsingsFix(string[] namespaces)
    {
        _operations.Add(new AutoFixOperation
        {
            Type = AutoFixType.UsingsStatement,
            Data = namespaces
        });
        return this;
    }

    public AutoFixBatch AddNullabilityFix()
    {
        _operations.Add(new AutoFixOperation { Type = AutoFixType.Nullability });
        return this;
    }

    public AutoFixBatch AddAsyncFix()
    {
        _operations.Add(new AutoFixOperation { Type = AutoFixType.AsyncMethods });
        return this;
    }

    public string ApplyTo(string sourceCode)
    {
        var result = sourceCode;
        
        foreach (var operation in _operations)
        {
            result = operation.Type switch
            {
                AutoFixType.UsingsStatement => AutoFixProvider.UsingStatementFixer.AddMissingUsings(result, (string[])operation.Data),
                AutoFixType.Nullability => AutoFixProvider.NullabilityFixer.FixNullableWarnings(result),
                AutoFixType.AsyncMethods => AutoFixProvider.AsyncMethodFixer.FixAsyncMethods(result),
                _ => result
            };
        }

        return result;
    }
}

public record AutoFixOperation
{
    public AutoFixType Type { get; init; }
    public object Data { get; init; } = new object();
}

public enum AutoFixType
{
    UsingsStatement,
    Nullability,
    AsyncMethods,
    DIRegistration,
    CodeStyle
}