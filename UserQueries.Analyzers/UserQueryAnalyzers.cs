using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System;
using System.Collections.Immutable;
using System.Linq;

namespace UserQueries.Analyzers
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

		private static readonly DiagnosticDescriptor EmbeddedUserQueryableNameRule =
			new DiagnosticDescriptor(
				id: "UQUERY04",
				title: "Invalid EmbeddedUserQueryable name",
				messageFormat: "The name '{0}' is not valid for EmbeddedUserQueryableAttribute",
				category: "Usage",
				defaultSeverity: DiagnosticSeverity.Error,
				isEnabledByDefault: true
				);


		private static readonly DiagnosticDescriptor EmbeddedPropertyExistRule =
			new DiagnosticDescriptor(
				id: "UQUERY05",
				title: "Property doesn't exist for EmbeddedPropertyName",
				messageFormat: "The property '{0}' does not exist within {1}.",
				category: "Usage",
				defaultSeverity: DiagnosticSeverity.Error,
				isEnabledByDefault: true
				);

		private static readonly DiagnosticDescriptor EmbeddedPropertyQueryableRule =
			new DiagnosticDescriptor(
				id: "UQUERY06",
				title: "Invalid EmbeddedUserQueryable",
				messageFormat: "The property '{0}' is not queryable. Add the UserQueryableAttribute to it.",
				category: "Usage",
				defaultSeverity: DiagnosticSeverity.Error,
				isEnabledByDefault: true
				);
		#endregion

		public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
			ImmutableArray.Create(
				UserQueryableNameRule,
				PrimaryUserQueryablePropertyExistRule,
				PrimaryUserQueryablePropertyQueryableRule,
				EmbeddedUserQueryableNameRule,
				EmbeddedPropertyExistRule);

		public override void Initialize(AnalysisContext context)
		{
			context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
			context.EnableConcurrentExecution();
			context.RegisterSyntaxNodeAction(AnalyzeAttribute, SyntaxKind.Attribute);
			context.RegisterSyntaxNodeAction(AnalyzeClass, SyntaxKind.ClassDeclaration);
			context.RegisterSyntaxNodeAction(AnalyzeProperty, SyntaxKind.PropertyDeclaration);
		}

		private void AnalyzeAttribute(SyntaxNodeAnalysisContext context)
		{
			var attributeSyntax = (AttributeSyntax)context.Node;
			if (!(context.SemanticModel.GetSymbolInfo(attributeSyntax).Symbol is IMethodSymbol symbol)) return;

			var attributeType = symbol.ContainingType;

			switch (attributeType.ToDisplayString())
			{
				case "UserQueries.UserQueryableAttribute":
					{
						var queryName = GetAttributeStringArgument(attributeSyntax, context.SemanticModel, "queryName", 0);
						if (queryName != null && !IsValidQueryablePropertyName(queryName))
						{
							var diagnostic = Diagnostic.Create(UserQueryableNameRule, attributeSyntax.GetLocation(), queryName);
							context.ReportDiagnostic(diagnostic);
						}
					}
					break;

				case "UserQueries.EmbeddedUserQueryableAttribute":
					{
						var queryName = GetAttributeStringArgument(attributeSyntax, context.SemanticModel, "queryName", 0);
						if (queryName != null && !IsValidQueryablePropertyName(queryName))
						{
							var diagnostic = Diagnostic.Create(EmbeddedUserQueryableNameRule, attributeSyntax.GetLocation(), queryName);
							context.ReportDiagnostic(diagnostic);
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
					EvaluateUserQueryableAttribute(context, semanticModel, classDecl, attribute);
				}
			}
		}

		private static void AnalyzeProperty(SyntaxNodeAnalysisContext context)
		{
			var propertyDecl = (PropertyDeclarationSyntax)context.Node;
			var semanticModel = context.SemanticModel;

			foreach (var attributeList in propertyDecl.AttributeLists)
			{
				foreach (var attribute in attributeList.Attributes)
				{
					EvaluateEmbeddedUserQueryableAttribute(context, semanticModel, propertyDecl, attribute);
				}
			}
		}

		private static void EvaluateUserQueryableAttribute(SyntaxNodeAnalysisContext context, SemanticModel semanticModel, ClassDeclarationSyntax classDecl, AttributeSyntax attribute)
		{
			var typeInfo = semanticModel.GetTypeInfo(attribute);
			var attributeType = typeInfo.Type;

			if (attributeType == null || attributeType.ToDisplayString() != "UserQueries.PrimaryUserQueryableAttribute")
				return;

			var propertyName = GetAttributeStringArgument(attribute, semanticModel, "propertyName", 0);
			if (propertyName == null)
				return;

			var classSymbol = semanticModel.GetDeclaredSymbol(classDecl);
			var propertySymbol = classSymbol
				.GetMembers()
				.OfType<IPropertySymbol>()
				.FirstOrDefault(p => p.Name == propertyName);

			if (propertySymbol == null)
			{
				var diag = Diagnostic.Create(
					PrimaryUserQueryablePropertyExistRule,
					attribute.GetLocation(),
					propertyName,
					classSymbol.Name);
				context.ReportDiagnostic(diag);
				return;
			}

			var hasRequiredAttribute = propertySymbol
				.GetAttributes()
				.Any(attr => attr.AttributeClass != null && attr.AttributeClass.ToDisplayString() == "UserQueries.UserQueryableAttribute");

			if (!hasRequiredAttribute)
			{
				var diag = Diagnostic.Create(
					PrimaryUserQueryablePropertyQueryableRule,
					attribute.GetLocation(),
					propertyName);
				context.ReportDiagnostic(diag);
			}
		}

		private static void EvaluateEmbeddedUserQueryableAttribute(SyntaxNodeAnalysisContext context, SemanticModel semanticModel, PropertyDeclarationSyntax propertyDecl, AttributeSyntax attribute)
		{
			var typeInfo = semanticModel.GetTypeInfo(attribute);
			var attributeType = typeInfo.Type;

			if (attributeType == null || attributeType.ToDisplayString() != "UserQueries.EmbeddedUserQueryableAttribute")
				return;

			var embeddedPropertyName = GetAttributeStringArgument(attribute, semanticModel, "embeddedPropertyName", 1);
			if (embeddedPropertyName == null)
				return;

			var propertySymbol = semanticModel.GetDeclaredSymbol(propertyDecl) as IPropertySymbol;
			if (propertySymbol == null)
				return;

			var embeddedPropertySymbol = propertySymbol.Type
				.GetMembers()
				.OfType<IPropertySymbol>()
				.FirstOrDefault(p => p.Name == embeddedPropertyName);

			if (embeddedPropertySymbol == null)
			{
				var diag = Diagnostic.Create(
					EmbeddedPropertyExistRule,
					attribute.GetLocation(),
					embeddedPropertyName,
					propertySymbol.Type.ToDisplayString());
				context.ReportDiagnostic(diag);
			}
		}

		private static string GetAttributeStringArgument(AttributeSyntax attribute, SemanticModel semanticModel, string parameterName, int fallbackPosition)
		{
			if (attribute.ArgumentList == null)
				return null;

			foreach (var argument in attribute.ArgumentList.Arguments)
			{
				var name = argument.NameEquals != null
					? argument.NameEquals.Name.Identifier.ValueText
					: argument.NameColon != null
						? argument.NameColon.Name.Identifier.ValueText
						: null;

				if (string.Equals(name, parameterName, StringComparison.OrdinalIgnoreCase))
					return GetStringValue(argument.Expression, semanticModel);
			}

			var methodSymbol = semanticModel.GetSymbolInfo(attribute).Symbol as IMethodSymbol;
			if (methodSymbol != null)
			{
				for (int i = 0; i < attribute.ArgumentList.Arguments.Count && i < methodSymbol.Parameters.Length; i++)
				{
					if (string.Equals(methodSymbol.Parameters[i].Name, parameterName, StringComparison.OrdinalIgnoreCase))
						return GetStringValue(attribute.ArgumentList.Arguments[i].Expression, semanticModel);
				}
			}

			if (fallbackPosition >= 0 && fallbackPosition < attribute.ArgumentList.Arguments.Count)
				return GetStringValue(attribute.ArgumentList.Arguments[fallbackPosition].Expression, semanticModel);

			return null;
		}

		private static string GetStringValue(ExpressionSyntax expression, SemanticModel semanticModel)
		{
			var constant = semanticModel.GetConstantValue(expression);
			return constant.HasValue && constant.Value is string
				? (string)constant.Value
				: null;
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

			if (new[] { "default", "orderby", "orderbydescending" }.Contains(propertyName, StringComparer.OrdinalIgnoreCase))
				return false;

			return true;
		}
	}
}
