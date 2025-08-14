using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DotNetMcp.Core.Refactoring;

public class SimpleExtractInterfaceRefactorer : SimpleRefactoringBase
{
    public record ExtractInterfaceResult(
        string ModifiedCode,
        string ExtractedInterface,
        string[] ExtractedMembers,
        string InterfaceName);

    public async Task<ExtractInterfaceResult> ExtractInterfaceAsync(
        string code, 
        string className, 
        string interfaceName, 
        string[]? memberNames = null)
    {
        var (syntaxTree, semanticModel) = ParseCode(code);
        var root = await syntaxTree.GetRootAsync();

        // Find the target class
        var classDeclaration = root.DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .FirstOrDefault(c => c.Identifier.ValueText == className);

        if (classDeclaration == null)
        {
            throw new InvalidOperationException($"Class '{className}' not found");
        }

        // Get members to extract
        var membersToExtract = GetMembersToExtract(classDeclaration, memberNames);
        
        // Create the interface
        var interfaceDeclaration = CreateInterface(interfaceName, membersToExtract, semanticModel);
        
        // Modify the class to implement the interface
        var modifiedClass = AddInterfaceToClass(classDeclaration, interfaceName);
        
        // Replace the class in the syntax tree
        var modifiedRoot = root.ReplaceNode(classDeclaration, modifiedClass);
        
        // Add the interface to the compilation unit
        var compilationUnit = modifiedRoot as CompilationUnitSyntax;
        if (compilationUnit != null)
        {
            modifiedRoot = compilationUnit.AddMembers(interfaceDeclaration);
        }

        var extractedMemberNames = membersToExtract.Select(GetMemberName).ToArray();

        return new ExtractInterfaceResult(
            modifiedRoot.ToFullString(),
            interfaceDeclaration.ToFullString(),
            extractedMemberNames,
            interfaceName);
    }

    private static IEnumerable<MemberDeclarationSyntax> GetMembersToExtract(
        ClassDeclarationSyntax classDeclaration, 
        string[]? memberNames = null)
    {
        var publicMembers = classDeclaration.Members
            .Where(m => m.Modifiers.Any(mod => mod.IsKind(SyntaxKind.PublicKeyword)))
            .Where(m => m is MethodDeclarationSyntax or PropertyDeclarationSyntax);

        if (memberNames != null && memberNames.Length > 0)
        {
            return publicMembers.Where(m => memberNames.Contains(GetMemberName(m)));
        }

        return publicMembers;
    }

    private static string GetMemberName(MemberDeclarationSyntax member)
    {
        return member switch
        {
            MethodDeclarationSyntax method => method.Identifier.ValueText,
            PropertyDeclarationSyntax property => property.Identifier.ValueText,
            _ => "Unknown"
        };
    }

    private static InterfaceDeclarationSyntax CreateInterface(
        string interfaceName, 
        IEnumerable<MemberDeclarationSyntax> members,
        SemanticModel semanticModel)
    {
        var interfaceMembers = new List<MemberDeclarationSyntax>();

        foreach (var member in members)
        {
            MemberDeclarationSyntax? interfaceMember = member switch
            {
                MethodDeclarationSyntax method => CreateInterfaceMethod(method),
                PropertyDeclarationSyntax property => CreateInterfaceProperty(property),
                _ => null
            };

            if (interfaceMember != null)
            {
                interfaceMembers.Add(interfaceMember);
            }
        }

        return SyntaxFactory.InterfaceDeclaration(interfaceName)
            .WithModifiers(SyntaxFactory.TokenList(SyntaxFactory.Token(SyntaxKind.PublicKeyword)))
            .WithMembers(SyntaxFactory.List(interfaceMembers));
    }

    private static MethodDeclarationSyntax CreateInterfaceMethod(MethodDeclarationSyntax method)
    {
        return SyntaxFactory.MethodDeclaration(method.ReturnType, method.Identifier)
            .WithParameterList(method.ParameterList)
            .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken));
    }

    private static PropertyDeclarationSyntax CreateInterfaceProperty(PropertyDeclarationSyntax property)
    {
        var accessorList = SyntaxFactory.AccessorList();
        
        // Add getter if present
        if (property.AccessorList?.Accessors.Any(a => a.IsKind(SyntaxKind.GetAccessorDeclaration)) == true)
        {
            accessorList = accessorList.AddAccessors(
                SyntaxFactory.AccessorDeclaration(SyntaxKind.GetAccessorDeclaration)
                    .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken)));
        }
        
        // Add setter if present
        if (property.AccessorList?.Accessors.Any(a => a.IsKind(SyntaxKind.SetAccessorDeclaration)) == true)
        {
            accessorList = accessorList.AddAccessors(
                SyntaxFactory.AccessorDeclaration(SyntaxKind.SetAccessorDeclaration)
                    .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken)));
        }

        return SyntaxFactory.PropertyDeclaration(property.Type, property.Identifier)
            .WithAccessorList(accessorList);
    }

    private static ClassDeclarationSyntax AddInterfaceToClass(ClassDeclarationSyntax classDeclaration, string interfaceName)
    {
        var interfaceType = SyntaxFactory.SimpleBaseType(SyntaxFactory.IdentifierName(interfaceName));
        
        if (classDeclaration.BaseList == null)
        {
            return classDeclaration.WithBaseList(
                SyntaxFactory.BaseList(SyntaxFactory.SingletonSeparatedList<BaseTypeSyntax>(interfaceType)));
        }
        
        return classDeclaration.WithBaseList(
            classDeclaration.BaseList.AddTypes(interfaceType));
    }
}