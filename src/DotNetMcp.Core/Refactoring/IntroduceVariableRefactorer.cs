using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DotNetMcp.Core.Refactoring;

public class IntroduceVariableRefactorer : RefactoringBase
{
    public record IntroduceVariableResult(
        string ModifiedCode,
        string VariableDeclaration,
        string VariableType,
        string Scope,
        int ReplacementCount);

    public async Task<IntroduceVariableResult> IntroduceVariableAsync(
        string code,
        string expression,
        string variableName,
        string scope = "local",
        bool replaceAll = true)
    {
        var (syntaxTree, semanticModel) = ParseCode(code);
        var root = await syntaxTree.GetRootAsync();

        // Find all occurrences of the expression
        var expressionNodes = FindExpressionOccurrences(root, expression);
        if (!expressionNodes.Any())
        {
            throw new InvalidOperationException($"Expression '{expression}' not found in code");
        }

        // Determine the type of the expression
        var firstExpression = expressionNodes.First();
        var variableType = DetermineVariableType(firstExpression, semanticModel);

        // Create the variable declaration
        var variableDeclaration = CreateVariableDeclaration(variableName, variableType, firstExpression);

        // Replace expression occurrences with variable reference
        var modifiedRoot = root;
        var replacementCount = 0;

        var expressionsToReplace = replaceAll ? expressionNodes : expressionNodes.Take(1);
        
        foreach (var expr in expressionsToReplace)
        {
            var variableReference = SyntaxFactory.IdentifierName(variableName);
            modifiedRoot = modifiedRoot.ReplaceNode(expr, variableReference);
            replacementCount++;
        }

        // Insert the variable declaration in the appropriate scope
        modifiedRoot = InsertVariableDeclaration(modifiedRoot, variableDeclaration, firstExpression, scope);

        return new IntroduceVariableResult(
            modifiedRoot.ToFullString(),
            variableDeclaration.ToFullString(),
            variableType,
            scope,
            replacementCount);
    }

    private static IEnumerable<ExpressionSyntax> FindExpressionOccurrences(SyntaxNode root, string expression)
    {
        var normalizedExpression = expression.Replace(" ", "").Replace("\n", "").Replace("\r", "");
        
        return root.DescendantNodes()
            .OfType<ExpressionSyntax>()
            .Where(expr => expr.ToFullString().Replace(" ", "").Replace("\n", "").Replace("\r", "") == normalizedExpression);
    }

    private static string DetermineVariableType(ExpressionSyntax expression, SemanticModel semanticModel)
    {
        // For method calls and complex expressions, prefer "var"
        if (expression is InvocationExpressionSyntax)
        {
            return "var";
        }

        var typeInfo = semanticModel.GetTypeInfo(expression);
        
        if (typeInfo.Type != null)
        {
            return typeInfo.Type.ToDisplayString();
        }

        // Fallback to analyzing the expression syntax
        return expression switch
        {
            LiteralExpressionSyntax literal when literal.Token.IsKind(SyntaxKind.StringLiteralToken) => "string",
            LiteralExpressionSyntax literal when literal.Token.IsKind(SyntaxKind.NumericLiteralToken) => 
                literal.Token.ValueText.Contains('.') ? "double" : "int",
            LiteralExpressionSyntax literal when literal.Token.IsKind(SyntaxKind.TrueKeyword) || literal.Token.IsKind(SyntaxKind.FalseKeyword) => "bool",
            ObjectCreationExpressionSyntax creation => creation.Type?.ToString() ?? "object",
            _ => "var"
        };
    }

    private static VariableDeclarationSyntax CreateVariableDeclaration(string variableName, string variableType, ExpressionSyntax initializer)
    {
        var typeSyntax = variableType == "var" 
            ? SyntaxFactory.IdentifierName("var")
            : SyntaxFactory.ParseTypeName(variableType);

        var declarator = SyntaxFactory.VariableDeclarator(variableName)
            .WithInitializer(SyntaxFactory.EqualsValueClause(initializer));

        return SyntaxFactory.VariableDeclaration(typeSyntax)
            .WithVariables(SyntaxFactory.SingletonSeparatedList(declarator));
    }

    private static SyntaxNode InsertVariableDeclaration(
        SyntaxNode root, 
        VariableDeclarationSyntax variableDeclaration, 
        ExpressionSyntax originalExpression, 
        string scope)
    {
        return scope.ToLower() switch
        {
            "local" => InsertLocalVariable(root, variableDeclaration, originalExpression),
            "field" => InsertFieldVariable(root, variableDeclaration),
            "property" => InsertPropertyVariable(root, variableDeclaration),
            _ => InsertLocalVariable(root, variableDeclaration, originalExpression)
        };
    }

    private static SyntaxNode InsertLocalVariable(SyntaxNode root, VariableDeclarationSyntax variableDeclaration, ExpressionSyntax originalExpression)
    {
        // Find the method or block containing the expression
        var containingMethod = originalExpression.FirstAncestorOrSelf<MethodDeclarationSyntax>();
        if (containingMethod?.Body != null)
        {
            var declarationStatement = SyntaxFactory.LocalDeclarationStatement(variableDeclaration);
            var newBody = containingMethod.Body.WithStatements(
                containingMethod.Body.Statements.Insert(0, declarationStatement));
            
            return root.ReplaceNode(containingMethod.Body, newBody);
        }

        // If not in a method, try to find the nearest block
        var containingBlock = originalExpression.FirstAncestorOrSelf<BlockSyntax>();
        if (containingBlock != null)
        {
            var declarationStatement = SyntaxFactory.LocalDeclarationStatement(variableDeclaration);
            var newBlock = containingBlock.WithStatements(
                containingBlock.Statements.Insert(0, declarationStatement));
            
            return root.ReplaceNode(containingBlock, newBlock);
        }

        return root;
    }

    private static SyntaxNode InsertFieldVariable(SyntaxNode root, VariableDeclarationSyntax variableDeclaration)
    {
        var classDeclaration = root.DescendantNodes().OfType<ClassDeclarationSyntax>().FirstOrDefault();
        if (classDeclaration != null)
        {
            var fieldDeclaration = SyntaxFactory.FieldDeclaration(variableDeclaration)
                .WithModifiers(SyntaxFactory.TokenList(SyntaxFactory.Token(SyntaxKind.PrivateKeyword)));
            
            var newClass = classDeclaration.WithMembers(
                classDeclaration.Members.Insert(0, fieldDeclaration));
            
            return root.ReplaceNode(classDeclaration, newClass);
        }
        
        return root;
    }

    private static SyntaxNode InsertPropertyVariable(SyntaxNode root, VariableDeclarationSyntax variableDeclaration)
    {
        // Use string-based approach for simplicity
        var code = root.ToFullString();
        var variable = variableDeclaration.Variables.First();
        var propertyName = variable.Identifier.ValueText;
        var propertyType = variableDeclaration.Type.ToString();
        
        var propertyDeclaration = $@"    public {propertyType} {propertyName} {{ get; set; }}";
        
        if (variable.Initializer != null)
        {
            var initValue = variable.Initializer.Value.ToString();
            propertyDeclaration = $@"    public {propertyType} {propertyName} {{ get; set; }} = {initValue};";
        }
        
        // Find the class and insert the property
        var classStartIndex = code.IndexOf("public class");
        if (classStartIndex >= 0)
        {
            var openBraceIndex = code.IndexOf('{', classStartIndex);
            if (openBraceIndex >= 0)
            {
                code = code.Insert(openBraceIndex + 1, $"\n{propertyDeclaration}\n");
            }
        }
        
        return CSharpSyntaxTree.ParseText(code).GetRoot();
    }
}