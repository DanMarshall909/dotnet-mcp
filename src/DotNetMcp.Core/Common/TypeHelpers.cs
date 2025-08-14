using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DotNetMcp.Core.Common;

/// <summary>
/// Modern pattern-matching based type helpers
/// </summary>
public static class TypeHelpers
{
    /// <summary>
    /// Convert ITypeSymbol to TypeSyntax using pattern matching
    /// </summary>
    public static TypeSyntax ToTypeSyntax(this ITypeSymbol typeSymbol) => typeSymbol.SpecialType switch
    {
        SpecialType.System_Boolean => SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.BoolKeyword)),
        SpecialType.System_Int32 => SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.IntKeyword)),
        SpecialType.System_String => SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.StringKeyword)),
        SpecialType.System_Double => SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.DoubleKeyword)),
        SpecialType.System_Single => SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.FloatKeyword)),
        SpecialType.System_Int64 => SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.LongKeyword)),
        SpecialType.System_Void => SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.VoidKeyword)),
        _ => SyntaxFactory.IdentifierName(typeSymbol.Name)
    };

    /// <summary>
    /// Get display name for type using pattern matching
    /// </summary>
    public static string GetDisplayName(this ITypeSymbol typeSymbol) => typeSymbol.SpecialType switch
    {
        SpecialType.System_Boolean => "bool",
        SpecialType.System_Int32 => "int", 
        SpecialType.System_String => "string",
        SpecialType.System_Double => "double",
        SpecialType.System_Single => "float",
        SpecialType.System_Int64 => "long",
        SpecialType.System_Void => "void",
        _ => typeSymbol.Name
    };

    /// <summary>
    /// Extract member name using pattern matching
    /// </summary>
    public static string GetMemberName(this MemberDeclarationSyntax member) => member switch
    {
        MethodDeclarationSyntax method => method.Identifier.ValueText,
        PropertyDeclarationSyntax property => property.Identifier.ValueText,
        FieldDeclarationSyntax field => field.Declaration.Variables.FirstOrDefault()?.Identifier.ValueText ?? "Unknown",
        EventDeclarationSyntax eventDecl => eventDecl.Identifier.ValueText,
        _ => "Unknown"
    };

    /// <summary>
    /// Check if member is public using pattern matching
    /// </summary>
    public static bool IsPublic(this MemberDeclarationSyntax member) =>
        member.Modifiers.Any(modifier => modifier.IsKind(SyntaxKind.PublicKeyword));

    /// <summary>
    /// Check if member is extractable using pattern matching and guards
    /// </summary>
    public static bool IsExtractable(this MemberDeclarationSyntax member) => member switch
    {
        MethodDeclarationSyntax method when method.IsPublic() => true,
        PropertyDeclarationSyntax property when property.IsPublic() => true,
        _ => false
    };
}