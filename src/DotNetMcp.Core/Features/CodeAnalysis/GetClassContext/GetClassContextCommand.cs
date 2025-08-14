using DotNetMcp.Core.SharedKernel;
using FluentValidation;

namespace DotNetMcp.Core.Features.CodeAnalysis;

/// <summary>
/// Command to get comprehensive context for a class
/// </summary>
public record GetClassContextCommand : IAppRequest<GetClassContextResponse>
{
    public required string ProjectPath { get; init; }
    public required string ClassName { get; init; }
    public bool IncludeDependencies { get; init; } = true;
    public bool IncludeUsages { get; init; } = true;
    public bool IncludeInheritance { get; init; } = true;
    public bool IncludeImplementations { get; init; } = false;
    public bool IncludeTestContext { get; init; } = false;
    public int MaxDepth { get; init; } = 2;
    public bool OptimizeForTokens { get; init; } = false;
    public int MaxTokens { get; init; } = 2000;
}

/// <summary>
/// Response containing comprehensive class context
/// </summary>
public record GetClassContextResponse
{
    public required ClassInfo MainClass { get; init; }
    public required ClassInfo[] Dependencies { get; init; }
    public required UsageInfo[] Usages { get; init; }
    public required ClassInfo[] InheritanceChain { get; init; }
    public required ClassContextSummary Summary { get; init; }
    public TestContextInfo? TestContext { get; init; }
    public int EstimatedTokens { get; init; }
    public bool SummarizationApplied { get; init; }
}

/// <summary>
/// Detailed information about a class
/// </summary>
public record ClassInfo
{
    public required string Name { get; init; }
    public required string Namespace { get; init; }
    public required string FilePath { get; init; }
    public required int LineNumber { get; init; }
    public string? BaseClass { get; init; }
    public string[]? Interfaces { get; init; }
    public bool IsInterface { get; init; }
    public bool IsAbstract { get; init; }
    public bool IsStatic { get; init; }
    public AccessModifier AccessModifier { get; init; }
    public MethodInfo[] Methods { get; init; } = Array.Empty<MethodInfo>();
    public PropertyInfo[] Properties { get; init; } = Array.Empty<PropertyInfo>();
    public FieldInfo[] Fields { get; init; } = Array.Empty<FieldInfo>();
    public string? Documentation { get; init; }
    public string[]? Attributes { get; init; }
}

/// <summary>
/// Information about a method
/// </summary>
public record MethodInfo
{
    public required string Name { get; init; }
    public required string ReturnType { get; init; }
    public required ParameterInfo[] Parameters { get; init; }
    public required AccessModifier AccessModifier { get; init; }
    public bool IsStatic { get; init; }
    public bool IsVirtual { get; init; }
    public bool IsOverride { get; init; }
    public bool IsAbstract { get; init; }
    public int LineNumber { get; init; }
    public string? Documentation { get; init; }
}

/// <summary>
/// Information about a property
/// </summary>
public record PropertyInfo
{
    public required string Name { get; init; }
    public required string Type { get; init; }
    public required AccessModifier AccessModifier { get; init; }
    public bool HasGetter { get; init; }
    public bool HasSetter { get; init; }
    public bool IsStatic { get; init; }
    public int LineNumber { get; init; }
    public string? Documentation { get; init; }
}

/// <summary>
/// Information about a field
/// </summary>
public record FieldInfo
{
    public required string Name { get; init; }
    public required string Type { get; init; }
    public required AccessModifier AccessModifier { get; init; }
    public bool IsStatic { get; init; }
    public bool IsReadOnly { get; init; }
    public bool IsConst { get; init; }
    public int LineNumber { get; init; }
    public string? DefaultValue { get; init; }
}

/// <summary>
/// Information about how a class is used
/// </summary>
public record UsageInfo
{
    public required string FilePath { get; init; }
    public required int LineNumber { get; init; }
    public required string UsageType { get; init; } // "Constructor Parameter", "Field", "Property", "Method Call", etc.
    public required string Context { get; init; }
    public string? MethodName { get; init; }
    public string? ClassName { get; init; }
}

/// <summary>
/// Test context information
/// </summary>
public record TestContextInfo
{
    public string[] TestFiles { get; init; } = Array.Empty<string>();
    public TestMethodInfo[] TestMethods { get; init; } = Array.Empty<TestMethodInfo>();
    public string[] CoverageGaps { get; init; } = Array.Empty<string>();
    public double EstimatedCoverage { get; init; }
}

/// <summary>
/// Information about test methods
/// </summary>
public record TestMethodInfo
{
    public required string Name { get; init; }
    public required string TestClass { get; init; }
    public required string FilePath { get; init; }
    public string? TestedMethod { get; init; }
    public string[]? TestAttributes { get; init; }
}

/// <summary>
/// Summary of class context analysis
/// </summary>
public record ClassContextSummary
{
    public int TotalLines { get; init; }
    public int CoreComplexity { get; init; }
    public int DependencyCount { get; init; }
    public int UsageCount { get; init; }
    public string[] KeyInsights { get; init; } = Array.Empty<string>();
    public bool SummarizationApplied { get; init; }
    public int EstimatedTokens { get; init; }
}

/// <summary>
/// Validator for get class context command
/// </summary>
public class GetClassContextCommandValidator : AbstractValidator<GetClassContextCommand>
{
    public GetClassContextCommandValidator()
    {
        RuleFor(x => x.ProjectPath)
            .NotEmpty()
            .WithMessage("Project path cannot be empty");

        RuleFor(x => x.ClassName)
            .NotEmpty()
            .WithMessage("Class name cannot be empty")
            .Matches(@"^[a-zA-Z_][a-zA-Z0-9_]*$")
            .WithMessage("Class name must be a valid C# identifier");

        RuleFor(x => x.MaxDepth)
            .GreaterThan(0)
            .LessThanOrEqualTo(5)
            .WithMessage("Max depth must be between 1 and 5");

        RuleFor(x => x.MaxTokens)
            .GreaterThan(0)
            .When(x => x.OptimizeForTokens)
            .WithMessage("Max tokens must be greater than 0 when token optimization is enabled");
    }
}