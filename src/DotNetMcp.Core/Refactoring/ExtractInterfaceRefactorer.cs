using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DotNetMcp.Core.Refactoring;

public class ExtractInterfaceRefactorer
{
    public record ExtractInterfaceResult(
        string InterfaceContent,
        string ModifiedClassContent,
        string InterfaceFilePath,
        string[] ExtractedMembers,
        string[] AffectedFiles);

    public async Task<ExtractInterfaceResult> ExtractInterfaceAsync(
        string filePath, 
        string className, 
        string interfaceName, 
        string[] memberNames)
    {
        var sourceCode = await File.ReadAllTextAsync(filePath);
        var syntaxTree = CSharpSyntaxTree.ParseText(sourceCode);
        var root = await syntaxTree.GetRootAsync();

        // Find the target class
        var classDeclaration = root.DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .FirstOrDefault(c => c.Identifier.ValueText == className);

        if (classDeclaration == null)
        {
            throw new ArgumentException($"Class '{className}' not found in file");
        }

        // Extract public members (or specified members)
        var membersToExtract = GetMembersToExtract(classDeclaration, memberNames);
        var interfaceMembers = CreateInterfaceMembers(membersToExtract);
        
        // Create the interface declaration
        var interfaceDeclaration = CreateInterfaceDeclaration(interfaceName, interfaceMembers);
        
        // Modify the class to implement the interface
        var modifiedClass = AddInterfaceImplementation(classDeclaration, interfaceName);
        var newRoot = root.ReplaceNode(classDeclaration, modifiedClass);

        // Generate interface file content
        var namespaceDeclaration = classDeclaration.FirstAncestorOrSelf<BaseNamespaceDeclarationSyntax>();
        var interfaceContent = GenerateInterfaceFileContent(interfaceDeclaration, namespaceDeclaration);
        
        var interfaceFilePath = Path.Combine(Path.GetDirectoryName(filePath)!, $"{interfaceName}.cs");
        
        // Write the interface file
        await File.WriteAllTextAsync(interfaceFilePath, interfaceContent);

        var extractedMemberNames = membersToExtract.Select(GetMemberName).ToArray();
        
        return new ExtractInterfaceResult(
            interfaceContent,
            newRoot.ToFullString(),
            interfaceFilePath,
            extractedMemberNames,
            [filePath, interfaceFilePath]);
    }

    private static List<MemberDeclarationSyntax> GetMembersToExtract(
        ClassDeclarationSyntax classDeclaration, 
        string[] specificMembers)
    {
        var publicMembers = classDeclaration.Members
            .Where(m => IsPublicMember(m))
            .Where(m => IsExtractableMember(m))
            .ToList();

        if (specificMembers.Length > 0)
        {
            return publicMembers
                .Where(m => specificMembers.Contains(GetMemberName(m)))
                .ToList();
        }

        return publicMembers;
    }

    private static bool IsPublicMember(MemberDeclarationSyntax member)
    {
        return member.Modifiers.Any(m => m.IsKind(SyntaxKind.PublicKeyword));
    }

    private static bool IsExtractableMember(MemberDeclarationSyntax member)
    {
        return member is MethodDeclarationSyntax or PropertyDeclarationSyntax or EventDeclarationSyntax;
    }

    private static string GetMemberName(MemberDeclarationSyntax member)
    {
        return member switch
        {
            MethodDeclarationSyntax method => method.Identifier.ValueText,
            PropertyDeclarationSyntax property => property.Identifier.ValueText,
            EventDeclarationSyntax eventDecl => eventDecl.Identifier.ValueText,
            _ => string.Empty
        };
    }

    private static List<MemberDeclarationSyntax> CreateInterfaceMembers(
        IEnumerable<MemberDeclarationSyntax> classMembers)
    {
        var interfaceMembers = new List<MemberDeclarationSyntax>();

        foreach (var member in classMembers)
        {
            MemberDeclarationSyntax? interfaceMember = member switch
            {
                MethodDeclarationSyntax method => CreateInterfaceMethod(method),
                PropertyDeclarationSyntax property => CreateInterfaceProperty(property),
                EventDeclarationSyntax eventDecl => CreateInterfaceEvent(eventDecl),
                _ => null
            };

            if (interfaceMember != null)
            {
                interfaceMembers.Add(interfaceMember);
            }
        }

        return interfaceMembers;
    }

    private static MethodDeclarationSyntax CreateInterfaceMethod(MethodDeclarationSyntax method)
    {
        return method
            .WithModifiers(SyntaxFactory.TokenList()) // Remove all modifiers (public, virtual, etc.)
            .WithBody(null) // Remove method body
            .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken)) // Add semicolon
            .WithLeadingTrivia(SyntaxFactory.TriviaList())
            .WithTrailingTrivia(SyntaxFactory.TriviaList());
    }

    private static PropertyDeclarationSyntax CreateInterfaceProperty(PropertyDeclarationSyntax property)
    {
        return property
            .WithModifiers(SyntaxFactory.TokenList()) // Remove all modifiers
            .WithLeadingTrivia(SyntaxFactory.TriviaList())
            .WithTrailingTrivia(SyntaxFactory.TriviaList());
    }

    private static EventDeclarationSyntax CreateInterfaceEvent(EventDeclarationSyntax eventDecl)
    {
        return eventDecl
            .WithModifiers(SyntaxFactory.TokenList()) // Remove all modifiers
            .WithLeadingTrivia(SyntaxFactory.TriviaList())
            .WithTrailingTrivia(SyntaxFactory.TriviaList());
    }

    private static InterfaceDeclarationSyntax CreateInterfaceDeclaration(
        string interfaceName, 
        IEnumerable<MemberDeclarationSyntax> members)
    {
        return SyntaxFactory.InterfaceDeclaration(interfaceName)
            .WithModifiers(SyntaxFactory.TokenList(SyntaxFactory.Token(SyntaxKind.PublicKeyword)))
            .WithMembers(SyntaxFactory.List(members));
    }

    private static ClassDeclarationSyntax AddInterfaceImplementation(
        ClassDeclarationSyntax classDeclaration, 
        string interfaceName)
    {
        var baseType = SyntaxFactory.SimpleBaseType(SyntaxFactory.IdentifierName(interfaceName));
        
        if (classDeclaration.BaseList == null)
        {
            var baseList = SyntaxFactory.BaseList(SyntaxFactory.SingletonSeparatedList<BaseTypeSyntax>(baseType));
            return classDeclaration.WithBaseList(baseList);
        }
        else
        {
            var updatedBaseList = classDeclaration.BaseList.AddTypes(baseType);
            return classDeclaration.WithBaseList(updatedBaseList);
        }
    }

    private static string GenerateInterfaceFileContent(
        InterfaceDeclarationSyntax interfaceDeclaration, 
        BaseNamespaceDeclarationSyntax? namespaceDeclaration)
    {
        var compilationUnit = SyntaxFactory.CompilationUnit();

        if (namespaceDeclaration != null)
        {
            MemberDeclarationSyntax interfaceNamespace = namespaceDeclaration switch
            {
                NamespaceDeclarationSyntax ns => ns.WithMembers(SyntaxFactory.SingletonList<MemberDeclarationSyntax>(interfaceDeclaration)),
                FileScopedNamespaceDeclarationSyntax fsns => SyntaxFactory.FileScopedNamespaceDeclaration(fsns.Name)
                    .WithMembers(SyntaxFactory.SingletonList<MemberDeclarationSyntax>(interfaceDeclaration)),
                _ => throw new NotSupportedException("Unsupported namespace type")
            };
            
            compilationUnit = compilationUnit.AddMembers(interfaceNamespace);
        }
        else
        {
            compilationUnit = compilationUnit.AddMembers(interfaceDeclaration);
        }

        return compilationUnit.NormalizeWhitespace().ToFullString();
    }
}