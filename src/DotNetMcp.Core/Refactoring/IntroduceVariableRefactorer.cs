using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DotNetMcp.Core.Refactoring;

public class IntroduceVariableRefactorer
{
    public record IntroduceVariableResult(
        string ModifiedContent,
        string VariableDeclaration,
        string VariableType,
        string Scope,
        int ReplacementCount);

    public async Task<IntroduceVariableResult> IntroduceVariableAsync(
        string filePath, 
        string expression, 
        string variableName, 
        string scope, 
        bool replaceAll)
    {
        var sourceCode = await File.ReadAllTextAsync(filePath);
        var syntaxTree = CSharpSyntaxTree.ParseText(sourceCode);
        var root = await syntaxTree.GetRootAsync();

        // Find the expression in the code
        var expressionSpan = sourceCode.IndexOf(expression.Trim());
        if (expressionSpan == -1)
        {
            throw new ArgumentException("Expression not found in the file");
        }

        var expressionNode = root.FindNode(new Microsoft.CodeAnalysis.Text.TextSpan(expressionSpan, expression.Trim().Length));
        
        // Parse the expression to ensure it's valid
        var parsedExpression = SyntaxFactory.ParseExpression(expression.Trim());
        if (parsedExpression.ContainsDiagnostics)
        {
            throw new ArgumentException("Invalid expression syntax");
        }

        // Determine the scope and location for variable declaration
        var (declarationLocation, actualScope) = DetermineDeclarationLocation(expressionNode, scope);
        
        // Infer the variable type (simplified - could use semantic analysis for better type inference)
        var variableType = InferVariableType(parsedExpression);
        
        // Create the variable declaration
        var variableDeclaration = CreateVariableDeclaration(variableName, variableType, parsedExpression);
        
        // Find all occurrences of the expression (if replaceAll is true)
        var occurrences = replaceAll 
            ? FindAllOccurrences(root, expression.Trim())
            : [expressionNode];

        // Replace occurrences with variable reference
        var newRoot = root;
        var replacementCount = 0;

        foreach (var occurrence in occurrences)
        {
            if (occurrence.Parent != null)
            {
                var variableReference = SyntaxFactory.IdentifierName(variableName);
                newRoot = newRoot.ReplaceNode(occurrence, variableReference);
                replacementCount++;
            }
        }

        // Insert the variable declaration at the appropriate location
        newRoot = InsertVariableDeclaration(newRoot, declarationLocation, variableDeclaration, actualScope);

        var modifiedContent = newRoot.ToFullString();
        
        return new IntroduceVariableResult(
            modifiedContent,
            variableDeclaration.ToFullString().Trim(),
            variableType,
            actualScope,
            replacementCount);
    }

    private static (SyntaxNode location, string scope) DetermineDeclarationLocation(SyntaxNode expressionNode, string requestedScope)
    {
        return requestedScope.ToLower() switch
        {
            "field" => (expressionNode.FirstAncestorOrSelf<ClassDeclarationSyntax>() ?? expressionNode, "field"),
            "property" => (expressionNode.FirstAncestorOrSelf<ClassDeclarationSyntax>() ?? expressionNode, "property"),
            "local" or _ => DetermineLocalScope(expressionNode)
        };
    }

    private static (SyntaxNode location, string scope) DetermineLocalScope(SyntaxNode expressionNode)
    {
        // Try to find the containing method, constructor, or property accessor
        var containingMember = expressionNode.FirstAncestorOrSelf<MethodDeclarationSyntax>() ??
                              expressionNode.FirstAncestorOrSelf<ConstructorDeclarationSyntax>() ??
                              (SyntaxNode?)expressionNode.FirstAncestorOrSelf<AccessorDeclarationSyntax>() ??
                              expressionNode.FirstAncestorOrSelf<LocalFunctionStatementSyntax>();

        if (containingMember != null)
        {
            return (containingMember, "local");
        }

        // Fall back to block scope
        var containingBlock = expressionNode.FirstAncestorOrSelf<BlockSyntax>();
        return (containingBlock ?? expressionNode, "local");
    }

    private static string InferVariableType(ExpressionSyntax expression)
    {
        // Simplified type inference - in a real implementation, you'd use semantic analysis
        return expression switch
        {
            LiteralExpressionSyntax literal => literal.Token.ValueText switch
            {
                var s when s.StartsWith("\"") => "string",
                var s when s.Contains('.') => "double",
                var s when int.TryParse(s, out _) => "int",
                "true" or "false" => "bool",
                _ => "var"
            },
            InvocationExpressionSyntax => "var",
            MemberAccessExpressionSyntax => "var",
            ObjectCreationExpressionSyntax objCreation => objCreation.Type?.ToString() ?? "var",
            _ => "var"
        };
    }

    private static LocalDeclarationStatementSyntax CreateVariableDeclaration(
        string variableName, 
        string variableType, 
        ExpressionSyntax initializer)
    {
        var variableDeclarator = SyntaxFactory.VariableDeclarator(variableName)
            .WithInitializer(SyntaxFactory.EqualsValueClause(initializer));

        var variableDeclaration = SyntaxFactory.VariableDeclaration(SyntaxFactory.IdentifierName(variableType))
            .WithVariables(SyntaxFactory.SingletonSeparatedList(variableDeclarator));

        return SyntaxFactory.LocalDeclarationStatement(variableDeclaration);
    }

    private static FieldDeclarationSyntax CreateFieldDeclaration(
        string variableName, 
        string variableType, 
        ExpressionSyntax initializer)
    {
        var variableDeclarator = SyntaxFactory.VariableDeclarator(variableName)
            .WithInitializer(SyntaxFactory.EqualsValueClause(initializer));

        var variableDeclaration = SyntaxFactory.VariableDeclaration(SyntaxFactory.IdentifierName(variableType))
            .WithVariables(SyntaxFactory.SingletonSeparatedList(variableDeclarator));

        return SyntaxFactory.FieldDeclaration(variableDeclaration)
            .WithModifiers(SyntaxFactory.TokenList(SyntaxFactory.Token(SyntaxKind.PrivateKeyword)));
    }

    private static PropertyDeclarationSyntax CreatePropertyDeclaration(
        string variableName, 
        string variableType, 
        ExpressionSyntax initializer)
    {
        return SyntaxFactory.PropertyDeclaration(SyntaxFactory.IdentifierName(variableType), variableName)
            .WithModifiers(SyntaxFactory.TokenList(SyntaxFactory.Token(SyntaxKind.PublicKeyword)))
            .WithAccessorList(SyntaxFactory.AccessorList(SyntaxFactory.List(new[]
            {
                SyntaxFactory.AccessorDeclaration(SyntaxKind.GetAccessorDeclaration).WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken)),
                SyntaxFactory.AccessorDeclaration(SyntaxKind.SetAccessorDeclaration).WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken))
            })))
            .WithInitializer(SyntaxFactory.EqualsValueClause(initializer))
            .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken));
    }

    private static List<SyntaxNode> FindAllOccurrences(SyntaxNode root, string expression)
    {
        var parsedExpression = SyntaxFactory.ParseExpression(expression);
        var occurrences = new List<SyntaxNode>();

        foreach (var node in root.DescendantNodes())
        {
            if (node is ExpressionSyntax expr && AreEquivalent(expr, parsedExpression))
            {
                occurrences.Add(node);
            }
        }

        return occurrences;
    }

    private static bool AreEquivalent(ExpressionSyntax expr1, ExpressionSyntax expr2)
    {
        // Simplified equivalence check - normalize whitespace and compare text
        return expr1.NormalizeWhitespace().ToString() == expr2.NormalizeWhitespace().ToString();
    }

    private static SyntaxNode InsertVariableDeclaration(
        SyntaxNode root, 
        SyntaxNode declarationLocation, 
        LocalDeclarationStatementSyntax variableDeclaration, 
        string scope)
    {
        return scope switch
        {
            "field" => InsertFieldDeclaration(root, declarationLocation, variableDeclaration),
            "property" => InsertPropertyDeclaration(root, declarationLocation, variableDeclaration),
            "local" or _ => InsertLocalDeclaration(root, declarationLocation, variableDeclaration)
        };
    }

    private static SyntaxNode InsertLocalDeclaration(
        SyntaxNode root, 
        SyntaxNode declarationLocation, 
        LocalDeclarationStatementSyntax variableDeclaration)
    {
        if (declarationLocation is BlockSyntax block)
        {
            var updatedBlock = block.WithStatements(block.Statements.Insert(0, variableDeclaration));
            return root.ReplaceNode(block, updatedBlock);
        }

        if (declarationLocation is MethodDeclarationSyntax method && method.Body != null)
        {
            var updatedBody = method.Body.WithStatements(method.Body.Statements.Insert(0, variableDeclaration));
            var updatedMethod = method.WithBody(updatedBody);
            return root.ReplaceNode(method, updatedMethod);
        }

        // For other cases, try to find a suitable block
        var parentBlock = declarationLocation.FirstAncestorOrSelf<BlockSyntax>();
        if (parentBlock != null)
        {
            var updatedBlock = parentBlock.WithStatements(parentBlock.Statements.Insert(0, variableDeclaration));
            return root.ReplaceNode(parentBlock, updatedBlock);
        }

        return root; // Fallback - no changes
    }

    private static SyntaxNode InsertFieldDeclaration(
        SyntaxNode root, 
        SyntaxNode declarationLocation, 
        LocalDeclarationStatementSyntax localDeclaration)
    {
        if (declarationLocation is ClassDeclarationSyntax classDecl)
        {
            // Convert local declaration to field declaration
            var variableDeclarator = localDeclaration.Declaration.Variables.First();
            var fieldDeclaration = CreateFieldDeclaration(
                variableDeclarator.Identifier.ValueText,
                localDeclaration.Declaration.Type.ToString(),
                variableDeclarator.Initializer?.Value ?? SyntaxFactory.LiteralExpression(SyntaxKind.NullLiteralExpression));

            var updatedClass = classDecl.WithMembers(classDecl.Members.Insert(0, fieldDeclaration));
            return root.ReplaceNode(classDecl, updatedClass);
        }

        return root;
    }

    private static SyntaxNode InsertPropertyDeclaration(
        SyntaxNode root, 
        SyntaxNode declarationLocation, 
        LocalDeclarationStatementSyntax localDeclaration)
    {
        if (declarationLocation is ClassDeclarationSyntax classDecl)
        {
            // Convert local declaration to property declaration
            var variableDeclarator = localDeclaration.Declaration.Variables.First();
            var propertyDeclaration = CreatePropertyDeclaration(
                variableDeclarator.Identifier.ValueText,
                localDeclaration.Declaration.Type.ToString(),
                variableDeclarator.Initializer?.Value ?? SyntaxFactory.LiteralExpression(SyntaxKind.NullLiteralExpression));

            var updatedClass = classDecl.WithMembers(classDecl.Members.Insert(0, propertyDeclaration));
            return root.ReplaceNode(classDecl, updatedClass);
        }

        return root;
    }
}