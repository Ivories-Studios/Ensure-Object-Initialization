using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace IvoriesStudios.EnsureInitialization
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class EnsureInitializationAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "EnsureInitialization";

        private static readonly LocalizableString Title = "Every instance must be initialized";
        private static readonly LocalizableString MessageFormat = "The method '{0}' was not called on the instantiated object";
        private static readonly LocalizableString Description = "Ensure that the specified initialization method is called on every instance created via Instantiate.";
        private const string Category = "Usage";

        private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category, DiagnosticSeverity.Error, isEnabledByDefault: true, description: Description);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterSyntaxNodeAction(AnalyzeObjectInstantiation, SyntaxKind.InvocationExpression);
        }

        private static void AnalyzeObjectInstantiation(SyntaxNodeAnalysisContext context)
        {
            InvocationExpressionSyntax invocationExpr = (InvocationExpressionSyntax)context.Node;

            ExpressionSyntax expression = invocationExpr.Expression;
            IMethodSymbol methodSymbol = null;

            // Check if the expression is a member access (e.g., UnityEngine.Object.Instantiate)
            if (expression is MemberAccessExpressionSyntax memberAccessExpr)
            {
                methodSymbol = context.SemanticModel.GetSymbolInfo(memberAccessExpr).Symbol as IMethodSymbol;
            }
            // Check if the expression is a simple identifier (e.g., Instantiate)
            else if (expression is IdentifierNameSyntax identifierNameSyntax)
            {
                methodSymbol = context.SemanticModel.GetSymbolInfo(identifierNameSyntax).Symbol as IMethodSymbol;
            }

            if (methodSymbol == null)
            {
                return;
            }

            // Check if the method is UnityEngine.Object.Instantiate or just Instantiate
            INamedTypeSymbol containingType = methodSymbol.ContainingType;
            if (containingType == null || containingType.ToString() != "UnityEngine.Object")
            {
                return;
            }

            // Get the type of the instantiated object
            SeparatedSyntaxList<ArgumentSyntax> argumentList = invocationExpr.ArgumentList.Arguments;
            if (argumentList.Count == 0)
            {
                return;
            }

            TypeInfo typeInfo = context.SemanticModel.GetTypeInfo(argumentList[0].Expression);
            ITypeSymbol instantiatedType = typeInfo.Type;

            if (instantiatedType == null || !IsMonoBehaviour(instantiatedType))
            {
                return;
            }

            // Check if the type has the RequiresInitialization attribute
            AttributeData requiresInitializationAttr = instantiatedType.GetAttributes()
                .FirstOrDefault(attr => attr.AttributeClass.Name == "RequiresInitializationAttribute");

            if (requiresInitializationAttr == null)
            {
                return;
            }

            // Get the specified initialization method name from the attribute
            string initializationMethodName = requiresInitializationAttr.ConstructorArguments[0].Value as string;

            if (string.IsNullOrEmpty(initializationMethodName))
            {
                return;
            }

            // Find the variable that holds the instantiated object
            string variableName = null;

            // Check if the parent node is a simple variable declaration
            if (invocationExpr.Parent is EqualsValueClauseSyntax equalsValueClause)
            {
                if (equalsValueClause.Parent is VariableDeclaratorSyntax variableDeclarator)
                {
                    variableName = variableDeclarator.Identifier.Text;
                }
            }
            // Check if it's part of an assignment statement
            else if (invocationExpr.Parent is AssignmentExpressionSyntax assignmentExpr)
            {
                if (assignmentExpr.Left is IdentifierNameSyntax identifierName)
                {
                    variableName = identifierName.Identifier.Text;
                }
            }

            // If no variable found, return early
            if (variableName == null)
            {
                return;
            }

            // Check if the instance is followed by the specified initialization method call
            BlockSyntax parentBlock = invocationExpr.FirstAncestorOrSelf<BlockSyntax>();
            if (parentBlock == null)
            {
                return;
            }

            // Look for the specified initialization method call on the instantiated object
            IEnumerable<InvocationExpressionSyntax> methodCalls = parentBlock.DescendantNodes()
                .OfType<InvocationExpressionSyntax>();

            bool isInitialized = methodCalls
                .Any(invocation =>
                {
                    MemberAccessExpressionSyntax memberAccess = invocation.Expression as MemberAccessExpressionSyntax;
                    return memberAccess != null && memberAccess.Expression.ToString() == variableName && memberAccess.Name.Identifier.Text == initializationMethodName;
                });

            if (!isInitialized)
            {
                Diagnostic diagnostic = Diagnostic.Create(Rule, invocationExpr.GetLocation(), initializationMethodName);
                context.ReportDiagnostic(diagnostic);
            }
        }

        private static bool IsMonoBehaviour(ITypeSymbol typeSymbol)
        {
            while (typeSymbol != null)
            {
                if (typeSymbol.ToString() == "UnityEngine.MonoBehaviour")
                {
                    return true;
                }
                typeSymbol = typeSymbol.BaseType;
            }
            return false;
        }
    }
}
