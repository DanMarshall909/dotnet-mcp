using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Text;

namespace DotNetMcp.Core.Refactoring;

public class ExtractMethodRefactorer
{
    public record ExtractMethodResult(
        string ModifiedContent,
        string ExtractedMethodSignature,
        string[] Parameters,
        string ReturnType);

    public async Task<ExtractMethodResult> ExtractMethodAsync(string filePath, string selectedCode, string methodName)
    {
        var sourceCode = await File.ReadAllTextAsync(filePath);
        var syntaxTree = CSharpSyntaxTree.ParseText(sourceCode);
        var root = await syntaxTree.GetRootAsync();

        // Find the selected code in the syntax tree
        var selectedSpan = sourceCode.IndexOf(selectedCode.Trim());
        if (selectedSpan == -1)
        {
            throw new ArgumentException("Selected code not found in the file");
        }

        var selectedNode = root.FindNode(new Microsoft.CodeAnalysis.Text.TextSpan(selectedSpan, selectedCode.Trim().Length));
        
        // Ensure we have a valid statement or expression
        var statements = ExtractStatements(selectedNode);
        if (!statements.Any())
        {
            throw new ArgumentException("Selected code does not contain valid statements");
        }

        // Analyze the selected code for variables and dependencies
        var analysis = AnalyzeSelectedCode(statements, root);
        
        // Generate the new method
        var extractedMethod = GenerateExtractedMethod(methodName, statements, analysis);
        
        // Find the containing class to add the method
        var containingClass = selectedNode.FirstAncestorOrSelf<ClassDeclarationSyntax>();
        if (containingClass == null)
        {
            throw new InvalidOperationException("Selected code is not within a class");
        }

        // Replace selected code with method call
        var methodCall = GenerateMethodCall(methodName, analysis.Parameters);
        var newRoot = ReplaceSelectedCodeWithMethodCall(root, selectedNode, methodCall, statements);
        
        // Add the extracted method to the class
        var updatedClass = containingClass.AddMembers(extractedMethod);
        newRoot = newRoot.ReplaceNode(containingClass, updatedClass);

        var modifiedContent = newRoot.ToFullString();
        
        return new ExtractMethodResult(
            modifiedContent,
            extractedMethod.Identifier.ValueText + extractedMethod.ParameterList.ToString(),
            analysis.Parameters.Select(p => $"{p.Type} {p.Name}").ToArray(),
            analysis.ReturnType);
    }

    private static IEnumerable<StatementSyntax> ExtractStatements(SyntaxNode selectedNode)
    {
        return selectedNode switch
        {
            StatementSyntax statement => [statement],
            BlockSyntax block => block.Statements.AsEnumerable(),
            _ => selectedNode.DescendantNodes().OfType<StatementSyntax>()
        };
    }

    private static CodeAnalysis AnalyzeSelectedCode(IEnumerable<StatementSyntax> statements, SyntaxNode root)
    {
        var declaredVariables = new HashSet<string>();
        var usedVariables = new HashSet<string>();
        var returnType = "void";
        var hasReturnStatement = false;

        foreach (var statement in statements)
        {
            // Find variable declarations
            var declarations = statement.DescendantNodes().OfType<VariableDeclarationSyntax>();
            foreach (var declaration in declarations)
            {
                foreach (var variable in declaration.Variables)
                {
                    declaredVariables.Add(variable.Identifier.ValueText);
                }
            }

            // Find variable usages
            var identifiers = statement.DescendantNodes().OfType<IdentifierNameSyntax>();
            foreach (var identifier in identifiers)
            {
                usedVariables.Add(identifier.Identifier.ValueText);
            }

            // Check for return statements
            if (statement is ReturnStatementSyntax returnStmt)
            {
                hasReturnStatement = true;
                if (returnStmt.Expression != null)
                {
                    returnType = "object"; // Simplified - should use semantic analysis
                }
            }
        }

        // Determine parameters (variables used but not declared in selection)
        var parameterNames = usedVariables.Except(declaredVariables).ToList();
        var parameters = parameterNames.Select(name => new Parameter("object", name)).ToList(); // Simplified type detection

        return new CodeAnalysis(parameters, returnType, hasReturnStatement);
    }

    private static MethodDeclarationSyntax GenerateExtractedMethod(string methodName, IEnumerable<StatementSyntax> statements, CodeAnalysis analysis)
    {
        var parameters = analysis.Parameters.Select(p =>
            SyntaxFactory.Parameter(SyntaxFactory.Identifier(p.Name))
                .WithType(SyntaxFactory.ParseTypeName(p.Type))
        );

        var method = SyntaxFactory.MethodDeclaration(
                SyntaxFactory.ParseTypeName(analysis.ReturnType),
                methodName)
            .WithModifiers(SyntaxFactory.TokenList(SyntaxFactory.Token(SyntaxKind.PrivateKeyword)))
            .WithParameterList(SyntaxFactory.ParameterList(SyntaxFactory.SeparatedList(parameters)))
            .WithBody(SyntaxFactory.Block(statements));

        return method;
    }

    private static ExpressionStatementSyntax GenerateMethodCall(string methodName, List<Parameter> parameters)
    {
        var arguments = parameters.Select(p => SyntaxFactory.Argument(SyntaxFactory.IdentifierName(p.Name)));
        
        var methodCall = SyntaxFactory.InvocationExpression(
            SyntaxFactory.IdentifierName(methodName),
            SyntaxFactory.ArgumentList(SyntaxFactory.SeparatedList(arguments))
        );

        return SyntaxFactory.ExpressionStatement(methodCall);
    }

    private static SyntaxNode ReplaceSelectedCodeWithMethodCall(
        SyntaxNode root, 
        SyntaxNode selectedNode, 
        StatementSyntax methodCall, 
        IEnumerable<StatementSyntax> statements)
    {
        if (selectedNode is StatementSyntax)
        {
            return root.ReplaceNode(selectedNode, methodCall);
        }

        // If multiple statements, replace them with the method call
        var firstStatement = statements.First();
        var newRoot = root.ReplaceNode(firstStatement, methodCall);
        
        foreach (var statement in statements.Skip(1))
        {
            newRoot = newRoot.RemoveNode(newRoot.FindNode(statement.Span), SyntaxRemoveOptions.KeepNoTrivia)!;
        }

        return newRoot;
    }

    private record Parameter(string Type, string Name);
    private record CodeAnalysis(List<Parameter> Parameters, string ReturnType, bool HasReturn);
}