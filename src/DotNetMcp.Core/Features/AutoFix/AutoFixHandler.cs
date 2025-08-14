using DotNetMcp.Core.AutoFixes;
using DotNetMcp.Core.Common;
using DotNetMcp.Core.SharedKernel;
using Microsoft.Extensions.Logging;

namespace DotNetMcp.Core.Features.AutoFix;

/// <summary>
/// Handler for automatic code fixes
/// </summary>
public class AutoFixHandler : BaseHandler<AutoFixCommand, AutoFixResponse>
{
    public AutoFixHandler(ILogger<AutoFixHandler> logger) : base(logger)
    {
    }

    protected override async Task<Result<AutoFixResponse>> HandleAsync(AutoFixCommand request, CancellationToken cancellationToken)
    {
        var startTime = DateTime.UtcNow;
        var appliedFixes = new List<AppliedFix>();
        var suggestedFixes = new List<SuggestedFix>();
        var warnings = new List<string>();

        try
        {
            Logger.LogInformation("Starting auto-fix operation for {FixTypes}", request.FixTypes);

            var currentCode = request.Code;
            var fixesApplied = 0;

            // 1. Handle build errors first (highest priority)
            if (request.FixTypes.HasFlag(AutoFixTypes.BuildErrors) && request.BuildErrors.Any())
            {
                var buildErrorFixes = ProcessBuildErrors(request.BuildErrors);
                suggestedFixes.AddRange(buildErrorFixes);
            }

            // 2. Apply using statement fixes
            if (request.FixTypes.HasFlag(AutoFixTypes.UsingsStatements))
            {
                var (newCode, fixes) = ApplyUsingsFixes(currentCode);
                if (newCode != currentCode)
                {
                    appliedFixes.AddRange(fixes);
                    currentCode = newCode;
                    fixesApplied += fixes.Length;
                }
            }

            // 3. Apply nullability fixes
            if (request.FixTypes.HasFlag(AutoFixTypes.Nullability))
            {
                var (newCode, fixes) = ApplyNullabilityFixes(currentCode);
                if (newCode != currentCode)
                {
                    appliedFixes.AddRange(fixes);
                    currentCode = newCode;
                    fixesApplied += fixes.Length;
                }
            }

            // 4. Apply async method fixes
            if (request.FixTypes.HasFlag(AutoFixTypes.AsyncMethods))
            {
                var (newCode, fixes) = ApplyAsyncFixes(currentCode);
                if (newCode != currentCode)
                {
                    appliedFixes.AddRange(fixes);
                    currentCode = newCode;
                    fixesApplied += fixes.Length;
                }
            }

            // 5. Apply code style fixes
            if (request.FixTypes.HasFlag(AutoFixTypes.CodeStyle))
            {
                var (newCode, fixes) = ApplyStyleFixes(currentCode);
                if (newCode != currentCode)
                {
                    appliedFixes.AddRange(fixes);
                    currentCode = newCode;
                    fixesApplied += fixes.Length;
                }
            }

            // 6. Apply performance fixes
            if (request.FixTypes.HasFlag(AutoFixTypes.Performance))
            {
                var (newCode, fixes) = ApplyPerformanceFixes(currentCode);
                if (newCode != currentCode)
                {
                    appliedFixes.AddRange(fixes);
                    currentCode = newCode;
                    fixesApplied += fixes.Length;
                }
            }

            // Safety check
            if (fixesApplied > request.MaxFixes)
            {
                warnings.Add($"Reached maximum fix limit ({request.MaxFixes}). Some fixes may not have been applied.");
            }

            var processingTime = DateTime.UtcNow - startTime;
            var summary = CreateSummary(appliedFixes, suggestedFixes, processingTime);

            var response = new AutoFixResponse
            {
                FixedCode = request.ApplyFixes ? currentCode : null,
                AppliedFixes = appliedFixes.ToArray(),
                SuggestedFixes = suggestedFixes.ToArray(),
                Summary = summary,
                Warnings = warnings.ToArray()
            };

            Logger.LogInformation("Auto-fix completed: {AppliedCount} fixes applied, {SuggestedCount} suggested", 
                appliedFixes.Count, suggestedFixes.Count);

            return Result<AutoFixResponse>.Success(response);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Auto-fix operation failed");
            return Result<AutoFixResponse>.Failure($"Auto-fix failed: {ex.Message}", ex);
        }
    }

    private SuggestedFix[] ProcessBuildErrors(string[] buildErrors)
    {
        var suggestions = new List<SuggestedFix>();

        foreach (var error in buildErrors)
        {
            var fixSuggestions = PatternBasedFixes.GetFixSuggestions(error);
            foreach (var suggestion in fixSuggestions)
            {
                suggestions.Add(new SuggestedFix
                {
                    Type = "BuildError",
                    Description = "Fix build error",
                    Suggestion = suggestion,
                    Reasoning = $"Addresses build error: {error}",
                    Confidence = FixConfidence.High
                });
            }
        }

        return suggestions.ToArray();
    }

    private (string newCode, AppliedFix[] fixes) ApplyUsingsFixes(string code)
    {
        var fixes = new List<AppliedFix>();
        
        // Analyze code for missing types and add appropriate usings
        var missingTypes = AnalyzeMissingTypes(code);
        var namespacesToAdd = new List<string>();

        foreach (var type in missingTypes)
        {
            if (AutoFixProvider.UsingStatementFixer.CommonMissingUsings.TryGetValue(type, out var namespaces))
            {
                namespacesToAdd.AddRange(namespaces);
                fixes.Add(new AppliedFix
                {
                    Type = "UsingsStatement",
                    Description = $"Add using for {type}",
                    OriginalCode = code,
                    FixedCode = "", // Will be filled in after transformation
                    Confidence = FixConfidence.High
                });
            }
        }

        if (namespacesToAdd.Any())
        {
            var newCode = AutoFixProvider.UsingStatementFixer.AddMissingUsings(code, namespacesToAdd.Distinct().ToArray());
            return (newCode, fixes.ToArray());
        }

        return (code, Array.Empty<AppliedFix>());
    }

    private (string newCode, AppliedFix[] fixes) ApplyNullabilityFixes(string code)
    {
        var originalCode = code;
        var newCode = AutoFixProvider.NullabilityFixer.FixNullableWarnings(code);
        
        if (newCode != originalCode)
        {
            var fix = new AppliedFix
            {
                Type = "Nullability",
                Description = "Fix nullability warnings",
                OriginalCode = originalCode,
                FixedCode = newCode,
                Confidence = FixConfidence.Medium
            };
            return (newCode, new[] { fix });
        }

        return (code, Array.Empty<AppliedFix>());
    }

    private (string newCode, AppliedFix[] fixes) ApplyAsyncFixes(string code)
    {
        var originalCode = code;
        var newCode = AutoFixProvider.AsyncMethodFixer.FixAsyncMethods(code);
        
        if (newCode != originalCode)
        {
            var fix = new AppliedFix
            {
                Type = "AsyncMethods",
                Description = "Fix async method signatures",
                OriginalCode = originalCode,
                FixedCode = newCode,
                Confidence = FixConfidence.High
            };
            return (newCode, new[] { fix });
        }

        return (code, Array.Empty<AppliedFix>());
    }

    private (string newCode, AppliedFix[] fixes) ApplyStyleFixes(string code)
    {
        var originalCode = code;
        var newCode = PatternBasedFixes.ApplyPatternFixes(code, PatternFixType.Style);
        
        if (newCode != originalCode)
        {
            var fix = new AppliedFix
            {
                Type = "CodeStyle",
                Description = "Apply code style improvements",
                OriginalCode = originalCode,
                FixedCode = newCode,
                Confidence = FixConfidence.Medium
            };
            return (newCode, new[] { fix });
        }

        return (code, Array.Empty<AppliedFix>());
    }

    private (string newCode, AppliedFix[] fixes) ApplyPerformanceFixes(string code)
    {
        var originalCode = code;
        var newCode = PatternBasedFixes.ApplyPatternFixes(code, PatternFixType.Performance);
        
        if (newCode != originalCode)
        {
            var fix = new AppliedFix
            {
                Type = "Performance",
                Description = "Apply performance optimizations",
                OriginalCode = originalCode,
                FixedCode = newCode,
                Confidence = FixConfidence.Medium
            };
            return (newCode, new[] { fix });
        }

        return (code, Array.Empty<AppliedFix>());
    }

    private string[] AnalyzeMissingTypes(string code)
    {
        var missingTypes = new List<string>();
        
        // Simple analysis - look for common types that appear without full qualification
        var commonTypes = AutoFixProvider.UsingStatementFixer.CommonMissingUsings.Keys;
        
        foreach (var type in commonTypes)
        {
            if (code.Contains(type) && !code.Contains($"using") && !code.Contains($"System."))
            {
                missingTypes.Add(type);
            }
        }

        return missingTypes.ToArray();
    }

    private AutoFixSummary CreateSummary(List<AppliedFix> appliedFixes, List<SuggestedFix> suggestedFixes, TimeSpan processingTime)
    {
        var categories = appliedFixes.Select(f => f.Type).Concat(suggestedFixes.Select(f => f.Type)).Distinct().ToArray();
        
        return new AutoFixSummary
        {
            TotalFixesApplied = appliedFixes.Count,
            TotalSuggestionsGenerated = suggestedFixes.Count,
            BuildErrorsAddressed = appliedFixes.Count(f => f.Type == "BuildError") + suggestedFixes.Count(f => f.Type == "BuildError"),
            StyleImprovements = appliedFixes.Count(f => f.Type == "CodeStyle"),
            PerformanceOptimizations = appliedFixes.Count(f => f.Type == "Performance"),
            ProcessingTime = processingTime,
            Categories = categories
        };
    }
}