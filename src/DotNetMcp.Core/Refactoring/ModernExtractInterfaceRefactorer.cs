using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using DotNetMcp.Core.Common;

namespace DotNetMcp.Core.Refactoring;

/// <summary>
/// Modern interface extraction using functional patterns and discriminated unions
/// </summary>
public class ModernExtractInterfaceRefactorer : RefactoringBase
{
    public record ExtractInterfaceRequest(
        string Code, 
        string ClassName, 
        string InterfaceName, 
        Option<string[]>? MemberNames = null);
    
    public record ExtractInterfaceResponse(
        string ModifiedCode,
        string ExtractedInterface,
        string[] ExtractedMembers,
        string InterfaceName);

    // Discriminated union for extractable members
    public abstract record ExtractableMember
    {
        public abstract string Name { get; }
        public abstract MemberDeclarationSyntax ToInterfaceMember();
        
        public sealed record Method(MethodDeclarationSyntax Declaration) : ExtractableMember
        {
            public override string Name => Declaration.Identifier.ValueText;
            
            public override MemberDeclarationSyntax ToInterfaceMember() =>
                SyntaxFactory.MethodDeclaration(Declaration.ReturnType, Declaration.Identifier)
                    .WithParameterList(Declaration.ParameterList)
                    .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken));
        }
        
        public sealed record Property(PropertyDeclarationSyntax Declaration) : ExtractableMember
        {
            public override string Name => Declaration.Identifier.ValueText;
            
            public override MemberDeclarationSyntax ToInterfaceMember()
            {
                var accessorList = SyntaxFactory.AccessorList();
                
                // Pattern match on existing accessors
                var accessors = Declaration.AccessorList?.Accessors ?? default;
                
                if (accessors.Any(a => a.IsKind(SyntaxKind.GetAccessorDeclaration)))
                {
                    accessorList = accessorList.AddAccessors(
                        SyntaxFactory.AccessorDeclaration(SyntaxKind.GetAccessorDeclaration)
                            .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken)));
                }
                
                if (accessors.Any(a => a.IsKind(SyntaxKind.SetAccessorDeclaration)))
                {
                    accessorList = accessorList.AddAccessors(
                        SyntaxFactory.AccessorDeclaration(SyntaxKind.SetAccessorDeclaration)
                            .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken)));
                }
                
                return SyntaxFactory.PropertyDeclaration(Declaration.Type, Declaration.Identifier)
                    .WithAccessorList(accessorList);
            }
        }
    }

    /// <summary>
    /// Extract interface using functional pipeline
    /// </summary>
    public Task<Result<ExtractInterfaceResponse>> ExtractInterfaceAsync(ExtractInterfaceRequest request)
    {
        return Task.FromResult(
            ParseCodeAsync(request.Code)
                .Bind(syntax => FindTargetClass(syntax, request.ClassName))
                .Bind(context => SelectMembersToExtract(context, request.MemberNames))
                .Bind(context => BuildInterface(context, request.InterfaceName))
                .Bind(ApplyInterfaceExtraction));
    }

    private static Result<(SyntaxTree Tree, SemanticModel Model, SyntaxNode Root)> ParseCodeAsync(string code)
    {
        try
        {
            var (syntaxTree, semanticModel) = ParseCode(code);
            var root = syntaxTree.GetRoot();
            return Result.Success((syntaxTree, semanticModel, root));
        }
        catch (Exception ex)
        {
            return Result.Failure<(SyntaxTree, SemanticModel, SyntaxNode)>("Failed to parse code", ex);
        }
    }

    private static Result<ClassContext> FindTargetClass(
        (SyntaxTree Tree, SemanticModel Model, SyntaxNode Root) syntax,
        string className)
    {
        var (tree, model, root) = syntax;
        
        var classDeclaration = root.DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .FirstOrDefault(c => c.Identifier.ValueText == className);

        return classDeclaration switch
        {
            null => Result.Failure<ClassContext>($"Class '{className}' not found"),
            var cls => Result.Success(new ClassContext(tree, model, root, cls))
        };
    }

    private static Result<MemberContext> SelectMembersToExtract(
        ClassContext context, 
        Option<string[]> memberNames)
    {
        var extractableMembers = context.ClassDeclaration.Members
            .Where(member => member.IsExtractable())
            .Select(member => CreateExtractableMember(member))
            .Where(member => member.IsSome)
            .Select(member => member.GetValueOrDefault(() => throw new InvalidOperationException()))
            .ToList();

        var selectedMembers = memberNames?.Match(
            names => extractableMembers.Where(m => names.Contains(m.Name)).ToList(),
            () => extractableMembers) ?? extractableMembers;

        return selectedMembers.Any() 
            ? Result.Success(new MemberContext(context, selectedMembers))
            : Result.Failure<MemberContext>("No extractable members found");
    }

    private static Option<ExtractableMember> CreateExtractableMember(MemberDeclarationSyntax member) => member switch
    {
        MethodDeclarationSyntax method => Option.Some<ExtractableMember>(new ExtractableMember.Method(method)),
        PropertyDeclarationSyntax property => Option.Some<ExtractableMember>(new ExtractableMember.Property(property)),
        _ => Option.None<ExtractableMember>()
    };

    private static Result<InterfaceContext> BuildInterface(MemberContext context, string interfaceName)
    {
        try
        {
            var interfaceMembers = context.Members
                .Select(member => member.ToInterfaceMember())
                .ToArray();

            var interfaceDeclaration = SyntaxBuilders.Interface(interfaceName)
                .WithModifiers(SyntaxKind.PublicKeyword)
                .WithMembers(interfaceMembers)
                .Build();

            return Result.Success(new InterfaceContext(context, interfaceDeclaration, interfaceName));
        }
        catch (Exception ex)
        {
            return Result.Failure<InterfaceContext>("Failed to build interface", ex);
        }
    }

    private static Result<ExtractInterfaceResponse> ApplyInterfaceExtraction(InterfaceContext interfaceContext)
    {
        try
        {
            var context = interfaceContext.Context.Context;
            var interfaceDecl = interfaceContext.InterfaceDeclaration;
            var interfaceName = interfaceContext.InterfaceName;
            
            // Add interface to class
            var modifiedClass = AddInterfaceToClass(context.ClassDeclaration, interfaceName);
            
            // Replace class in syntax tree
            var modifiedRoot = context.Root.ReplaceNode(context.ClassDeclaration, modifiedClass);
            
            // Add interface to compilation unit
            if (modifiedRoot is CompilationUnitSyntax compilationUnit)
            {
                modifiedRoot = compilationUnit.AddMembers(interfaceDecl);
            }

            var extractedMemberNames = interfaceContext.Context.Members
                .Select(member => member.Name)
                .ToArray();

            return Result.Success(new ExtractInterfaceResponse(
                modifiedRoot.NormalizeWhitespace().ToFullString(),
                interfaceDecl.NormalizeWhitespace().ToFullString(),
                extractedMemberNames,
                interfaceName));
        }
        catch (Exception ex)
        {
            return Result.Failure<ExtractInterfaceResponse>("Failed to apply interface extraction", ex);
        }
    }

    private static ClassDeclarationSyntax AddInterfaceToClass(ClassDeclarationSyntax classDeclaration, string interfaceName)
    {
        var interfaceType = SyntaxFactory.SimpleBaseType(SyntaxFactory.IdentifierName(interfaceName));
        
        return classDeclaration.BaseList switch
        {
            null => classDeclaration.WithBaseList(
                SyntaxFactory.BaseList(SyntaxFactory.SingletonSeparatedList<BaseTypeSyntax>(interfaceType))),
            var baseList => classDeclaration.WithBaseList(baseList.AddTypes(interfaceType))
        };
    }

    // Context records for pipeline
    private record ClassContext(SyntaxTree Tree, SemanticModel Model, SyntaxNode Root, ClassDeclarationSyntax ClassDeclaration);
    private record MemberContext(ClassContext Context, List<ExtractableMember> Members);
    private record InterfaceContext(MemberContext Context, InterfaceDeclarationSyntax InterfaceDeclaration, string InterfaceName);
}