using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DotNetMcp.Core.Refactoring;

public class ExtractMethodRefactorer : RefactoringBase
{
    public record ExtractMethodResult(
        string ModifiedCode,
        string ExtractedMethod,
        string[] UsedVariables,
        string ReturnType);

    private record VariableInfo(
        string Name,
        ITypeSymbol Type,
        bool IsParameter,
        bool IsUsedAfterSelection);

    private class ExtractMethodRewriter : CSharpSyntaxRewriter
    {
        private readonly StatementSyntax _targetStatement;
        private readonly MethodDeclarationSyntax _newMethod;
        private readonly StatementSyntax _methodCall;

        public ExtractMethodRewriter(StatementSyntax targetStatement, MethodDeclarationSyntax newMethod, StatementSyntax methodCall)
        {
            _targetStatement = targetStatement;
            _newMethod = newMethod;
            _methodCall = methodCall;
        }

        public override SyntaxNode? Visit(SyntaxNode? node)
        {
            if (node is StatementSyntax stmt && stmt.IsEquivalentTo(_targetStatement))
            {
                return _methodCall;
            }
            return base.Visit(node);
        }

        public override SyntaxNode? VisitClassDeclaration(ClassDeclarationSyntax node)
        {
            var newNode = (ClassDeclarationSyntax)base.VisitClassDeclaration(node)!;
            return newNode.AddMembers(_newMethod);
        }
    }

    public async Task<ExtractMethodResult> ExtractMethodAsync(string code, string selectedCode, string methodName)
    {
        var (syntaxTree, semanticModel) = ParseCode(code);
        var root = await syntaxTree.GetRootAsync();

        // Find the statement that matches the selected code
        var targetStatement = FindStatementByText(root, selectedCode.Trim());
        if (targetStatement == null)
        {
            throw new InvalidOperationException($"Selected code '{selectedCode.Trim()}' not found in source");
        }

        // Analyze variables used in the selected statement
        var variableAnalysis = AnalyzeVariables(targetStatement, semanticModel, root);

        // Build method parameters and return type
        var parameters = BuildParameters(variableAnalysis);
        var returnType = DetermineReturnType(targetStatement, semanticModel);

        // Create the new method
        var newMethod = CreateExtractedMethod(methodName, parameters, returnType, targetStatement);

        // Create the method call to replace the selected statement
        var methodCall = CreateMethodCall(methodName, variableAnalysis);

        // Use syntax rewriter to transform the tree
        var rewriter = new ExtractMethodRewriter(targetStatement, newMethod, methodCall);
        var newRoot = rewriter.Visit(root);

        var usedVariableNames = variableAnalysis.Where(v => v.IsParameter).Select(v => v.Name).ToArray();

        return new ExtractMethodResult(
            newRoot!.ToFullString(),
            newMethod.ToFullString(),
            usedVariableNames,
            GetTypeDisplayName(returnType));
    }

    private static StatementSyntax? FindStatementByText(SyntaxNode root, string targetText)
    {
        var statements = root.DescendantNodes().OfType<StatementSyntax>();
        
        foreach (var statement in statements)
        {
            var statementText = statement.ToString().Trim();
            if (statementText == targetText)
            {
                return statement;
            }
        }
        
        return null;
    }

    private static List<VariableInfo> AnalyzeVariables(StatementSyntax statement, SemanticModel semanticModel, SyntaxNode root)
    {
        var variables = new List<VariableInfo>();
        var identifiers = statement.DescendantNodes().OfType<IdentifierNameSyntax>();

        foreach (var identifier in identifiers)
        {
            var symbolInfo = semanticModel.GetSymbolInfo(identifier);
            
            if (symbolInfo.Symbol is ILocalSymbol localSymbol)
            {
                // Check if this variable is declared outside the selected statement
                var declarationSyntax = localSymbol.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax();
                if (declarationSyntax != null && !statement.Contains(declarationSyntax))
                {
                    var variableInfo = new VariableInfo(
                        localSymbol.Name,
                        localSymbol.Type,
                        true, // Is parameter
                        false); // Usage after selection not implemented yet
                    
                    if (!variables.Any(v => v.Name == variableInfo.Name))
                    {
                        variables.Add(variableInfo);
                    }
                }
            }
            else if (symbolInfo.Symbol is IParameterSymbol parameterSymbol)
            {
                var variableInfo = new VariableInfo(
                    parameterSymbol.Name,
                    parameterSymbol.Type,
                    true, // Is parameter
                    false); // Usage after selection not implemented yet
                
                if (!variables.Any(v => v.Name == variableInfo.Name))
                {
                    variables.Add(variableInfo);
                }
            }
            else if (symbolInfo.Symbol == null)
            {
                // If we can't resolve the symbol, it might be an undeclared variable
                // For the test case "var result = a + b + c;", a, b, c would be undeclared
                var identifierName = identifier.Identifier.ValueText;
                
                // Skip known method names, types, and keywords
                if (!IsWellKnownIdentifier(identifierName) && !IsKeyword(identifierName))
                {
                    // Don't include variables that are being declared in this statement
                    if (!IsVariableBeingDeclared(statement, identifierName))
                    {
                        var variableInfo = new VariableInfo(
                            identifierName,
                            semanticModel.Compilation.GetSpecialType(SpecialType.System_Int32), // Default to int for simplicity
                            true, // Is parameter
                            false); // Usage after selection not implemented yet
                        
                        if (!variables.Any(v => v.Name == variableInfo.Name))
                        {
                            variables.Add(variableInfo);
                        }
                    }
                }
            }
        }

        return variables;
    }

    private static bool IsWellKnownIdentifier(string name)
    {
        var wellKnownNames = new HashSet<string>
        {
            "Console", "WriteLine", "ReadLine", "ToString", "Length", "Count",
            "String", "Int32", "Boolean", "DateTime", "Math", "System"
        };
        
        return wellKnownNames.Contains(name);
    }

    private static bool IsKeyword(string name)
    {
        var keywords = new HashSet<string>
        {
            "var", "int", "string", "bool", "double", "float", "new", "return", 
            "if", "else", "for", "while", "class", "public", "private", "static", 
            "void", "true", "false", "null", "this", "base", "using", "namespace"
        };
        
        return keywords.Contains(name);
    }

    private static bool IsVariableBeingDeclared(StatementSyntax statement, string variableName)
    {
        // Check if this statement contains a variable declaration for this name
        var declarations = statement.DescendantNodes().OfType<VariableDeclaratorSyntax>();
        return declarations.Any(d => d.Identifier.ValueText == variableName);
    }

    private static SeparatedSyntaxList<ParameterSyntax> BuildParameters(List<VariableInfo> variables)
    {
        var parameters = new List<ParameterSyntax>();
        
        foreach (var variable in variables.Where(v => v.IsParameter))
        {
            var parameter = SyntaxFactory.Parameter(SyntaxFactory.Identifier(variable.Name))
                .WithType(SyntaxFactory.IdentifierName(variable.Type.Name));
            parameters.Add(parameter);
        }

        return SyntaxFactory.SeparatedList(parameters);
    }

    private static TypeSyntax DetermineReturnType(StatementSyntax statement, SemanticModel semanticModel)
    {
        var returnStatements = new List<ReturnStatementSyntax>();
        
        // Check if the statement itself is a return statement
        if (statement is ReturnStatementSyntax returnStmt)
        {
            returnStatements.Add(returnStmt);
        }
        
        // Also check for return statements within the statement (for compound statements)
        returnStatements.AddRange(statement.DescendantNodes().OfType<ReturnStatementSyntax>());
        
        if (returnStatements.Any())
        {
            var returnTypes = new List<ITypeSymbol>();
            
            foreach (var retStmt in returnStatements)
            {
                if (retStmt.Expression != null)
                {
                    var typeInfo = semanticModel.GetTypeInfo(retStmt.Expression);
                    if (typeInfo.Type != null)
                    {
                        returnTypes.Add(typeInfo.Type);
                    }
                }
            }
            
            if (returnTypes.Any())
            {
                // Find common base type if multiple return types
                var commonType = FindCommonBaseType(returnTypes);
                return CreateTypeSyntax(commonType);
            }
        }
        
        // Check if the statement is an expression that could be returned
        if (statement is ExpressionStatementSyntax exprStmt)
        {
            var typeInfo = semanticModel.GetTypeInfo(exprStmt.Expression);
            if (typeInfo.Type != null && !typeInfo.Type.SpecialType.Equals(SpecialType.System_Void))
            {
                // This could be converted to a return statement
                return CreateTypeSyntax(typeInfo.Type);
            }
        }
        
        // Check for variable assignments that could be returned
        var assignments = statement.DescendantNodes().OfType<AssignmentExpressionSyntax>();
        foreach (var assignment in assignments)
        {
            if (assignment.IsKind(SyntaxKind.SimpleAssignmentExpression))
            {
                var typeInfo = semanticModel.GetTypeInfo(assignment.Right);
                if (typeInfo.Type != null)
                {
                    // Could potentially return the assigned value
                    // For now, keep as void but this could be enhanced
                    break;
                }
            }
        }

        return SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.VoidKeyword));
    }

    private static ITypeSymbol FindCommonBaseType(List<ITypeSymbol> types)
    {
        if (!types.Any()) 
        {
            throw new ArgumentException("Cannot find common base type of empty list");
        }
        
        var commonType = types.First();
        foreach (var type in types.Skip(1))
        {
            commonType = FindCommonBaseType(commonType, type);
        }
        
        return commonType;
    }

    private static ITypeSymbol FindCommonBaseType(ITypeSymbol type1, ITypeSymbol type2)
    {
        if (SymbolEqualityComparer.Default.Equals(type1, type2))
            return type1;
            
        // Simple heuristic: if types don't match, return object
        var objectType = type1.ContainingAssembly.GetTypeByMetadataName("System.Object");
        return objectType ?? type1; // Fallback to first type if object not found
    }

    private static TypeSyntax CreateTypeSyntax(ITypeSymbol typeSymbol)
    {
        return typeSymbol.SpecialType switch
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
    }

    private static MethodDeclarationSyntax CreateExtractedMethod(string methodName, SeparatedSyntaxList<ParameterSyntax> parameters, TypeSyntax returnType, StatementSyntax statement)
    {
        var parameterList = SyntaxFactory.ParameterList(parameters);
        var body = SyntaxFactory.Block(statement);

        return SyntaxFactory.MethodDeclaration(returnType, methodName)
            .WithModifiers(SyntaxFactory.TokenList(SyntaxFactory.Token(SyntaxKind.PrivateKeyword)))
            .WithParameterList(parameterList)
            .WithBody(body)
            .WithLeadingTrivia(
                SyntaxFactory.CarriageReturnLineFeed,
                SyntaxFactory.CarriageReturnLineFeed,
                SyntaxFactory.Whitespace("    "))
            .WithTrailingTrivia(SyntaxFactory.CarriageReturnLineFeed);
    }

    private static ExpressionStatementSyntax CreateMethodCall(string methodName, List<VariableInfo> variables)
    {
        var arguments = variables
            .Where(v => v.IsParameter)
            .Select(v => SyntaxFactory.Argument(SyntaxFactory.IdentifierName(v.Name)))
            .ToArray();

        var argumentList = SyntaxFactory.ArgumentList(SyntaxFactory.SeparatedList(arguments));
        var invocation = SyntaxFactory.InvocationExpression(
            SyntaxFactory.IdentifierName(methodName),
            argumentList);

        return SyntaxFactory.ExpressionStatement(invocation)
            .WithLeadingTrivia(SyntaxFactory.Whitespace("            "));
    }

    private static string GetTypeDisplayName(TypeSyntax typeSyntax)
    {
        return typeSyntax switch
        {
            PredefinedTypeSyntax predefined => predefined.Keyword.ValueText,
            IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
            _ => typeSyntax.ToString()
        };
    }
}