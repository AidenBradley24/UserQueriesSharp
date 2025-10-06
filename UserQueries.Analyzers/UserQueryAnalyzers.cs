using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System;
using System.Collections.Immutable;
using System.Linq;

namespace UserQueryies.Analyzers
{
	[DiagnosticAnalyzer(LanguageNames.CSharp)]
	public class UserQueryAnalyzers : DiagnosticAnalyzer
	{
		#region RULES

		private static readonly DiagnosticDescriptor UserQueryableNameRule =
			new DiagnosticDescriptor(
				id: "UQUERY01",
				title: "Invalid UserQueryable name",
				messageFormat: "The name '{0}' is not valid for UserQueryableAttribute",
				category: "Usage",
				defaultSeverity: DiagnosticSeverity.Error,
				isEnabledByDefault: true,
				description: "Names must begin with a letter and only contain numbers and underscores."
				);

		private static readonly DiagnosticDescriptor PrimaryUserQueryablePropertyExistRule =
			new DiagnosticDescriptor(
				id: "UQUERY02",
				title: "Property doesn't exist for PrimaryUserQueryable",
				messageFormat: "The property '{0}' does not exist within {1}.",
				category: "Usage",
				defaultSeverity: DiagnosticSeverity.Error,
				isEnabledByDefault: true
				);

		private static readonly DiagnosticDescriptor PrimaryUserQueryablePropertyQueryableRule =
			new DiagnosticDescriptor(
				id: "UQUERY03",
				title: "Invalid PrimaryUserQueryable",
				messageFormat: "The property '{0}' is not queryable. Add the UserQueryableAttribute to it.",
				category: "Usage",
				defaultSeverity: DiagnosticSeverity.Error,
				isEnabledByDefault: true
				);
		#endregion

		public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
			ImmutableArray.Create(UserQueryableNameRule,
									PrimaryUserQueryablePropertyExistRule,
									PrimaryUserQueryablePropertyQueryableRule);

		public override void Initialize(AnalysisContext context)
		{
			context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
			context.EnableConcurrentExecution();
			context.RegisterSyntaxNodeAction(AnalyzeAttribute, SyntaxKind.Attribute);
			context.RegisterSyntaxNodeAction(AnalyzeClass, SyntaxKind.ClassDeclaration);
		}

		private void AnalyzeAttribute(SyntaxNodeAnalysisContext context)
		{
			var attributeSyntax = (AttributeSyntax)context.Node;
			if (!(context.SemanticModel.GetSymbolInfo(attributeSyntax).Symbol is IMethodSymbol symbol)) return;

			var attributeType = symbol.ContainingType;

			switch (attributeType.ToDisplayString())
			{
				case "UserQueries.UserQueryableAttribute":
					if (attributeSyntax.ArgumentList?.Arguments.Count > 0)
					{
						var arg = attributeSyntax.ArgumentList.Arguments[0];
						var constant = context.SemanticModel.GetConstantValue(arg.Expression);
						if (constant.HasValue && constant.Value is string s)
						{
							if (!IsValidQueryablePropertyName(s))
							{
								var diagnostic = Diagnostic.Create(UserQueryableNameRule, arg.GetLocation(), s);
								context.ReportDiagnostic(diagnostic);
							}
						}
					}
					break;
			}
		}

		private static void AnalyzeClass(SyntaxNodeAnalysisContext context)
		{
			var classDecl = (ClassDeclarationSyntax)context.Node;
			var semanticModel = context.SemanticModel;

			foreach (var attributeList in classDecl.AttributeLists)
			{
				foreach (var attribute in attributeList.Attributes)
				{
					var typeInfo = semanticModel.GetTypeInfo(attribute);
					var attributeType = typeInfo.Type;

					// Replace with your attribute’s full name
					if (attributeType == null || attributeType.ToDisplayString() != "UserQueries.PrimaryUserQueryableAttribute")
						continue;

					if (attribute.ArgumentList == null || attribute.ArgumentList.Arguments.Count == 0)
						continue;

					var argExpr = attribute.ArgumentList.Arguments[0].Expression;
					string propertyName = null;

					if (argExpr is LiteralExpressionSyntax literal &&
						literal.IsKind(SyntaxKind.StringLiteralExpression))
					{
						propertyName = literal.Token.ValueText;
					}
					else if (argExpr is InvocationExpressionSyntax invocation &&
							 invocation.Expression is IdentifierNameSyntax id &&
							 id.Identifier.Text == "nameof" &&
							 invocation.ArgumentList.Arguments.Count == 1)
					{
						var nameofArg = invocation.ArgumentList.Arguments[0].Expression;
						propertyName = nameofArg.ToString();
					}

					if (propertyName == null)
						continue;

					var classSymbol = semanticModel.GetDeclaredSymbol(classDecl);
					var propertySymbol = classSymbol
						.GetMembers()
						.OfType<IPropertySymbol>()
						.FirstOrDefault(p => p.Name == propertyName);

					if (propertySymbol == null)
					{
						var diag = Diagnostic.Create(PrimaryUserQueryablePropertyExistRule
							, attribute.GetLocation(),
							propertyName, classSymbol.Name);
						context.ReportDiagnostic(diag);
						continue;
					}

					// Property exists — check if it has a specific attribute
					var hasRequiredAttribute = propertySymbol
						.GetAttributes()
						.Any(attr => attr.AttributeClass?.ToDisplayString() == "UserQueries.UserQueryableAttribute");

					if (!hasRequiredAttribute)
					{
						var diag = Diagnostic.Create(
							PrimaryUserQueryablePropertyQueryableRule,
							attribute.GetLocation(),
							propertyName);
						context.ReportDiagnostic(diag);
					}
				}
			}
		}

		public static bool IsValidQueryablePropertyName(string propertyName)
		{
			if (string.IsNullOrEmpty(propertyName)) return false;
			if (!char.IsLetter(propertyName[0])) return false;
			foreach (char c in propertyName.AsSpan().Slice(1))
			{
				if (!char.IsLetterOrDigit(c) && c != '_')
					return false;
			}
			return true;
		}
	}
}
