using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DotNetMcp.Core.Common;

/// <summary>
/// Immutable fluent builders for Roslyn syntax nodes
/// </summary>
public static class SyntaxBuilders
{
    public static MethodBuilder Method(string name, TypeSyntax returnType) => new(name, returnType);
    public static ParameterBuilder Parameter(string name, TypeSyntax type) => new(name, type);
    public static InterfaceBuilder Interface(string name) => new(name);
}

/// <summary>
/// Immutable builder for method declarations with proper spacing
/// </summary>
public record MethodBuilder(
    string Name,
    TypeSyntax ReturnType,
    SyntaxTokenList Modifiers = default,
    ParameterListSyntax? Parameters = null,
    BlockSyntax? Body = null,
    SyntaxTriviaList LeadingTrivia = default)
{
    public MethodBuilder WithModifiers(params SyntaxKind[] modifiers) =>
        this with { Modifiers = SyntaxFactory.TokenList(modifiers.Select(CreateModifierWithSpace)) };

    public MethodBuilder WithParameters(params ParameterSyntax[] parameters) =>
        this with { Parameters = SyntaxFactory.ParameterList(SyntaxFactory.SeparatedList(parameters)) };

    public MethodBuilder WithBody(params StatementSyntax[] statements) =>
        this with { Body = SyntaxFactory.Block(statements) };

    public MethodBuilder WithLeadingTrivia(SyntaxTriviaList trivia) =>
        this with { LeadingTrivia = trivia };

    public MethodDeclarationSyntax Build() =>
        SyntaxFactory.MethodDeclaration(ReturnType.WithTrailingTrivia(SyntaxFactory.Space), Name)
            .WithModifiers(Modifiers)
            .WithParameterList((Parameters ?? SyntaxFactory.ParameterList()).WithTrailingTrivia(SyntaxFactory.Space))
            .WithBody(Body ?? SyntaxFactory.Block())
            .WithLeadingTrivia(LeadingTrivia.Any() ? LeadingTrivia : DefaultMethodTrivia());

    private static SyntaxToken CreateModifierWithSpace(SyntaxKind kind) =>
        SyntaxFactory.Token(kind).WithTrailingTrivia(SyntaxFactory.Space);

    private static SyntaxTriviaList DefaultMethodTrivia() =>
        SyntaxFactory.TriviaList(
            SyntaxFactory.CarriageReturnLineFeed,
            SyntaxFactory.CarriageReturnLineFeed,
            SyntaxFactory.Whitespace("    "));
}

/// <summary>
/// Immutable builder for parameters with proper typing
/// </summary>
public record ParameterBuilder(string Name, TypeSyntax Type)
{
    public ParameterSyntax Build() =>
        SyntaxFactory.Parameter(SyntaxFactory.Identifier(Name))
            .WithType(Type.WithTrailingTrivia(SyntaxFactory.Space));
}

/// <summary>
/// Immutable builder for interfaces
/// </summary>
public record InterfaceBuilder(
    string Name,
    SyntaxTokenList Modifiers = default,
    SyntaxList<MemberDeclarationSyntax> Members = default)
{
    public InterfaceBuilder WithModifiers(params SyntaxKind[] modifiers) =>
        this with { Modifiers = SyntaxFactory.TokenList(modifiers.Select(m => SyntaxFactory.Token(m))) };

    public InterfaceBuilder WithMembers(params MemberDeclarationSyntax[] members) =>
        this with { Members = SyntaxFactory.List(members) };

    public InterfaceDeclarationSyntax Build() =>
        SyntaxFactory.InterfaceDeclaration(Name)
            .WithModifiers(Modifiers.Any() ? Modifiers : SyntaxFactory.TokenList(SyntaxFactory.Token(SyntaxKind.PublicKeyword)))
            .WithMembers(Members);
}