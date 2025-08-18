using System.Text.RegularExpressions;

namespace DotNetMcp.Core.AutoFixes;

/// <summary>
/// Pattern-based quick fixes for common code issues
/// </summary>
public static class PatternBasedFixes
{
    /// <summary>
    /// Common build error fixes
    /// </summary>
    public static readonly Dictionary<string, AutoFixRule> BuildErrorFixes = new()
    {
        // CS0246: The type or namespace name 'X' could not be found
        ["CS0246"] = new AutoFixRule
        {
            Pattern = @"CS0246.*'(\w+)'.*could not be found",
            FixGenerator = match => GenerateUsingFix(match.Groups[1].Value),
            Description = "Add missing using statement"
        },

        // CS1061: 'X' does not contain a definition for 'Y'
        ["CS1061"] = new AutoFixRule
        {
            Pattern = @"CS1061.*'(\w+)'.*does not contain.*'(\w+)'",
            FixGenerator = match => GenerateExtensionMethodSuggestion(match.Groups[1].Value, match.Groups[2].Value),
            Description = "Suggest extension method or correct member name"
        },

        // CS8604: Possible null reference argument
        ["CS8604"] = new AutoFixRule
        {
            Pattern = @"CS8604.*null reference argument.*'(\w+)'",
            FixGenerator = match => GenerateNullCheckFix(match.Groups[1].Value),
            Description = "Add null check or null-forgiving operator"
        },

        // CS1998: Async method lacks 'await' operators
        ["CS1998"] = new AutoFixRule
        {
            Pattern = @"CS1998.*async method.*lacks 'await'",
            FixGenerator = match => "Remove 'async' keyword or add 'await' to async operations",
            Description = "Fix async method signature"
        }
    };

    /// <summary>
    /// Code style quick fixes
    /// </summary>
    public static readonly Dictionary<string, string> StyleFixes = new()
    {
        // Naming conventions
        [@"\bprivate\s+(\w+)\s+([a-z]\w*)"] = "private $1 _$2", // private field naming
        [@"\bpublic\s+const\s+(\w+)\s+([a-z]\w*)"] = "public const $1 $2", // const naming
        
        // Spacing improvements
        [@"if\(([^)]+)\)"] = "if ($1)", // add space after if
        
        // String interpolation
        [@"string\.Format\(""([^""]*)"",\s*([^)]+)\)"] = "$\"$1\"", // String.Format to interpolation
        
        // LINQ improvements
        [@"\.Where\(x => x != null\)"] = ".Where(x => x is not null)", // null checks
        [@"\.FirstOrDefault\(\) != null"] = " is not null", // FirstOrDefault checks
        
        // Modern C# patterns
        [@"new\s+(\w+)\(\)"] = "new $1()", // target-typed new
        [@"if\s*\(([^)]+)\s*!=\s*null\)"] = "if ($1 is not null)" // pattern matching
    };

    /// <summary>
    /// Performance-oriented fixes
    /// </summary>
    public static readonly Dictionary<string, string> PerformanceFixes = new()
    {
        // Collection improvements
        [@"new List<(\w+)>\(\)"] = "new List<$1>()",
        [@"\.ToArray\(\)\.Length"] = ".Count()",
        [@"\.ToList\(\)\.Count"] = ".Count()",
        
        // String concatenation
        [@"""\s*\+\s*(\w+)\s*\+\s*"""] = "$\"{{$1}}\"",
        
        // Memory allocation
        [@"new\s+(\w+)\[\s*\]"] = "Array.Empty<$1>()"
    };

    private static string GenerateUsingFix(string typeName)
    {
        if (AutoFixProvider.UsingStatementFixer.CommonMissingUsings.TryGetValue(typeName, out var namespaces))
        {
            return $"Add using: {string.Join(" or ", namespaces)}";
        }
        return $"Add using statement for {typeName}";
    }

    private static string GenerateExtensionMethodSuggestion(string type, string member)
    {
        var commonExtensions = new Dictionary<string, string[]>
        {
            ["string"] = new[] { "IsNullOrEmpty", "IsNullOrWhiteSpace", "Contains", "StartsWith" },
            ["IEnumerable"] = new[] { "Any", "Count", "Select", "Where", "FirstOrDefault" },
            ["Task"] = new[] { "ConfigureAwait", "ContinueWith" }
        };

        if (commonExtensions.TryGetValue(type, out var extensions) && extensions.Contains(member))
        {
            return $"Add using System.Linq for {member} extension method";
        }

        return $"Check spelling of {member} or add extension method";
    }

    private static string GenerateNullCheckFix(string parameter)
    {
        return $"Add null check: {parameter} ?? throw new ArgumentNullException(nameof({parameter}))";
    }

    /// <summary>
    /// Apply pattern-based fixes to source code
    /// </summary>
    public static string ApplyPatternFixes(string sourceCode, PatternFixType fixType = PatternFixType.All)
    {
        var result = sourceCode;

        if (fixType.HasFlag(PatternFixType.Style))
        {
            foreach (var (pattern, replacement) in StyleFixes)
            {
                result = Regex.Replace(result, pattern, replacement, RegexOptions.Multiline);
            }
        }

        if (fixType.HasFlag(PatternFixType.Performance))
        {
            foreach (var (pattern, replacement) in PerformanceFixes)
            {
                result = Regex.Replace(result, pattern, replacement, RegexOptions.Multiline);
            }
        }

        return result;
    }

    /// <summary>
    /// Apply build error fixes and return suggested fixes
    /// </summary>
    public static string[] ApplyBuildErrorFixes(string errorMessage)
    {
        var suggestions = new List<string>();

        foreach (var (errorCode, rule) in BuildErrorFixes)
        {
            if (errorMessage.Contains(errorCode))
            {
                var match = Regex.Match(errorMessage, rule.Pattern);
                if (match.Success)
                {
                    suggestions.Add(rule.FixGenerator(match));
                }
            }
        }

        // For CS0246 errors, try to extract type name and suggest common namespaces
        if (errorMessage.Contains("CS0246"))
        {
            var typeMatches = Regex.Matches(errorMessage, @"'(\w+)'.*could not be found");
            foreach (Match match in typeMatches)
            {
                var typeName = match.Groups[1].Value;
                suggestions.AddRange(GetCommonNamespacesForType(typeName));
            }
        }

        return suggestions.ToArray();
    }

    /// <summary>
    /// Apply code style fixes to source code
    /// </summary>
    public static string ApplyCodeStyleFixes(string sourceCode)
    {
        var result = sourceCode;
        
        // Public fields to properties with capitalization
        result = Regex.Replace(result, @"public\s+(\w+)\s+([a-z]\w*);", m => 
        {
            var type = m.Groups[1].Value;
            var fieldName = m.Groups[2].Value;
            var propertyName = char.ToUpper(fieldName[0]) + fieldName.Substring(1);
            return $"public {type} {propertyName} {{ get; set; }}";
        });
        
        // Add spaces around control flow keywords
        result = Regex.Replace(result, @"(\w+)\(", m => 
        {
            var keyword = m.Groups[1].Value;
            if (new[] { "if", "while", "for", "foreach", "switch" }.Contains(keyword))
            {
                return $"{keyword} (";
            }
            return m.Value;
        });
        
        return result;
    }

    /// <summary>
    /// Apply performance fixes to source code
    /// </summary>
    public static string ApplyPerformanceFixes(string sourceCode)
    {
        var result = sourceCode;
        
        // Replace common performance anti-patterns
        result = result.Replace("items.ToList().Count", "items.Count()");
        result = result.Replace("string.Concat(", "string.Join(\"\", new[] { ");
        result = result.Replace("items.Where(x => x != null).ToList()", "items.Where(x => x != null).ToArray()");
        
        return result;
    }

    /// <summary>
    /// Get fix suggestions for build errors
    /// </summary>
    public static string[] GetFixSuggestions(string buildError)
    {
        return ApplyBuildErrorFixes(buildError);
    }

    /// <summary>
    /// Get fixes for a specific error message
    /// </summary>
    public static string[] GetFixesForError(string errorMessage)
    {
        return ApplyBuildErrorFixes(errorMessage);
    }

    private static string[] GetCommonNamespacesForType(string typeName)
    {
        var commonTypes = new Dictionary<string, string[]>
        {
            ["List"] = new[] { "using System.Collections.Generic;" },
            ["Dictionary"] = new[] { "using System.Collections.Generic;" },
            ["Task"] = new[] { "using System.Threading.Tasks;" },
            ["HttpClient"] = new[] { "using System.Net.Http;" },
            ["JsonSerializer"] = new[] { "using System.Text.Json;" },
            ["ILogger"] = new[] { "using Microsoft.Extensions.Logging;" },
            ["StringBuilder"] = new[] { "using System.Text;" },
            ["Regex"] = new[] { "using System.Text.RegularExpressions;" },
            ["CancellationToken"] = new[] { "using System.Threading;" },
            ["IServiceCollection"] = new[] { "using Microsoft.Extensions.DependencyInjection;" }
        };

        if (commonTypes.TryGetValue(typeName, out var namespaces))
        {
            return namespaces;
        }

        return Array.Empty<string>();
    }
}

public record AutoFixRule
{
    public string Pattern { get; init; } = "";
    public Func<Match, string> FixGenerator { get; init; } = _ => "";
    public string Description { get; init; } = "";
}

[Flags]
public enum PatternFixType
{
    Style = 1,
    Performance = 2,
    BuildErrors = 4,
    All = Style | Performance | BuildErrors
}