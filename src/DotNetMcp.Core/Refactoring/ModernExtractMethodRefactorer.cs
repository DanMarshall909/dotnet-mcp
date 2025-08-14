using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using DotNetMcp.Core.Common;
using DotNetMcp.Core.Models;

namespace DotNetMcp.Core.Refactoring;

/// <summary>
/// Modern extract method refactorer using functional patterns and better abstractions
/// </summary>
public class ModernExtractMethodRefactorer : RefactoringBase
{
    public record ExtractMethodRequest(string Code, string SelectedCode, string MethodName);
    
    public record ExtractMethodResponse(
        string ModifiedCode,
        string ExtractedMethod,
        string[] UsedVariables,
        string ReturnType);

    // Discriminated union for variable analysis
    public abstract record VariableAnalysis
    {
        public sealed record Parameter(string Name, ITypeSymbol Type) : VariableAnalysis;
        public sealed record LocalVariable(string Name, ITypeSymbol Type) : VariableAnalysis;
        public sealed record ReturnVariable(string Name, ITypeSymbol Type) : VariableAnalysis;
    }

    /// <summary>
    /// Extract method using functional pipeline approach
    /// </summary>
    public Task<Result<ExtractMethodResponse>> ExtractMethodAsync(ExtractMethodRequest request)
    {
        return Task.FromResult(
            ParseCodeAsync(request.Code)
                .Bind(syntax => FindTargetStatement(syntax, request.SelectedCode))
                .Bind(context => AnalyzeVariables(context))
                .Bind(context => BuildExtractedMethod(context, request.MethodName))
                .Bind(ApplyRefactoring));
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

    private static Result<ExtractionContext> FindTargetStatement(
        (SyntaxTree Tree, SemanticModel Model, SyntaxNode Root) syntax, 
        string selectedCode)
    {
        var (tree, model, root) = syntax;
        
        var targetStatement = root.DescendantNodes()
            .OfType<StatementSyntax>()
            .FirstOrDefault(stmt => stmt.ToString().Trim() == selectedCode.Trim());

        return targetStatement switch
        {
            null => Result.Failure<ExtractionContext>($"Statement not found: {selectedCode}"),
            var stmt => Result.Success(new ExtractionContext(tree, model, root, stmt))
        };
    }

    private static Result<AnalyzedContext> AnalyzeVariables(ExtractionContext context)
    {
        try
        {
            var variables = context.Statement.DescendantNodes()
                .OfType<IdentifierNameSyntax>()
                .Select(id => AnalyzeIdentifier(id, context.Model))
                .Where(analysis => analysis != null)
                .Cast<VariableAnalysis>()
                .ToList();

            var returnType = DetermineReturnType(context.Statement, context.Model);
            
            return Result.Success(new AnalyzedContext(context, variables, returnType));
        }
        catch (Exception ex)
        {
            return Result.Failure<AnalyzedContext>("Failed to analyze variables", ex);
        }
    }

    private static VariableAnalysis? AnalyzeIdentifier(IdentifierNameSyntax identifier, SemanticModel model)
    {
        var symbolInfo = model.GetSymbolInfo(identifier);
        
        return symbolInfo.Symbol switch
        {
            IParameterSymbol param => new VariableAnalysis.Parameter(param.Name, param.Type),
            ILocalSymbol local => new VariableAnalysis.LocalVariable(local.Name, local.Type),
            _ => null
        };
    }

    private static TypeSyntax DetermineReturnType(StatementSyntax statement, SemanticModel model) =>
        statement switch
        {
            ReturnStatementSyntax returnStmt when returnStmt.Expression != null =>
                GetReturnTypeFromExpression(returnStmt.Expression, model),
            _ when ContainsReturnStatements(statement) =>
                GetReturnTypeFromNestedReturns(statement, model),
            _ => SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.VoidKeyword))
        };

    private static TypeSyntax GetReturnTypeFromExpression(ExpressionSyntax expression, SemanticModel model)
    {
        var typeInfo = model.GetTypeInfo(expression);
        return typeInfo.Type?.ToTypeSyntax() ?? 
               SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.VoidKeyword));
    }

    private static bool ContainsReturnStatements(StatementSyntax statement) =>
        statement.DescendantNodes().OfType<ReturnStatementSyntax>().Any();

    private static TypeSyntax GetReturnTypeFromNestedReturns(StatementSyntax statement, SemanticModel model)
    {
        var returnStatements = statement.DescendantNodes()
            .OfType<ReturnStatementSyntax>()
            .Where(r => r.Expression != null)
            .ToList();

        if (!returnStatements.Any())
            return SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.VoidKeyword));

        var firstReturnType = model.GetTypeInfo(returnStatements.First().Expression!).Type;
        return firstReturnType?.ToTypeSyntax() ?? 
               SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.VoidKeyword));
    }

    private static Result<MethodContext> BuildExtractedMethod(AnalyzedContext context, string methodName)
    {
        try
        {
            var parameters = context.Variables
                .OfType<VariableAnalysis.Parameter>()
                .Select(p => SyntaxBuilders.Parameter(p.Name, p.Type.ToTypeSyntax()).Build())
                .ToArray();

            var method = SyntaxBuilders.Method(methodName, context.ReturnType)
                .WithModifiers(SyntaxKind.PrivateKeyword)
                .WithParameters(parameters)
                .WithBody(context.Context.Statement)
                .Build();

            return Result.Success(new MethodContext(context, method));
        }
        catch (Exception ex)
        {
            return Result.Failure<MethodContext>("Failed to build extracted method", ex);
        }
    }

    private static Result<ExtractMethodResponse> ApplyRefactoring(MethodContext methodContext)
    {
        try
        {
            var context = methodContext.Context.Context;
            var method = methodContext.Method;
            
            // Create method call
            var arguments = methodContext.Context.Variables
                .OfType<VariableAnalysis.Parameter>()
                .Select(p => SyntaxFactory.Argument(SyntaxFactory.IdentifierName(p.Name)))
                .ToArray();

            var methodCall = SyntaxFactory.ExpressionStatement(
                SyntaxFactory.InvocationExpression(
                    SyntaxFactory.IdentifierName(method.Identifier.ValueText))
                .WithArgumentList(SyntaxFactory.ArgumentList(SyntaxFactory.SeparatedList(arguments))));

            // Apply transformation
            var rewriter = new ModernExtractMethodRewriter(context.Statement, method, methodCall);
            var newRoot = rewriter.Visit(context.Root);

            var usedVariables = methodContext.Context.Variables
                .OfType<VariableAnalysis.Parameter>()
                .Select(p => p.Name)
                .ToArray();

            return Result.Success(new ExtractMethodResponse(
                newRoot?.ToFullString() ?? string.Empty,
                method.ToFullString(),
                usedVariables,
                methodContext.Context.ReturnType.ToString()));
        }
        catch (Exception ex)
        {
            return Result.Failure<ExtractMethodResponse>("Failed to apply refactoring", ex);
        }
    }

    // Context records for pipeline
    private record ExtractionContext(SyntaxTree Tree, SemanticModel Model, SyntaxNode Root, StatementSyntax Statement);
    private record AnalyzedContext(ExtractionContext Context, List<VariableAnalysis> Variables, TypeSyntax ReturnType);
    private record MethodContext(AnalyzedContext Context, MethodDeclarationSyntax Method);

    // Modern rewriter using pattern matching
    private class ModernExtractMethodRewriter : CSharpSyntaxRewriter
    {
        private readonly StatementSyntax _targetStatement;
        private readonly MethodDeclarationSyntax _newMethod;
        private readonly StatementSyntax _methodCall;

        public ModernExtractMethodRewriter(StatementSyntax targetStatement, MethodDeclarationSyntax newMethod, StatementSyntax methodCall)
        {
            _targetStatement = targetStatement;
            _newMethod = newMethod;
            _methodCall = methodCall;
        }

        public override SyntaxNode? Visit(SyntaxNode? node) => node switch
        {
            StatementSyntax stmt when stmt.IsEquivalentTo(_targetStatement) => _methodCall,
            _ => base.Visit(node)
        };

        public override SyntaxNode? VisitClassDeclaration(ClassDeclarationSyntax node)
        {
            var newNode = (ClassDeclarationSyntax)base.VisitClassDeclaration(node)!;
            return newNode.AddMembers(_newMethod);
        }
    }
}