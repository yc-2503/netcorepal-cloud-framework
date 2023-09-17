using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Domain.CodeGenerators;

[Generator]
public class EntityIdCodeGenerators : ISourceGenerator
{
    public void Initialize(GeneratorInitializationContext context)
    {
        // Method intentionally left empty.
    }

    public void Execute(GeneratorExecutionContext context)
    {
        var compilation = context.Compilation;
        foreach (var syntaxTree in compilation.SyntaxTrees)
        {
            if (syntaxTree.TryGetText(out var sourceText) &&
                !sourceText.ToString().Contains("StronglyTypedId"))
            {
                continue;
            }

            var semanticModel = compilation.GetSemanticModel(syntaxTree);
            if (semanticModel == null)
            {
                continue;
            }

            var typeDeclarationSyntaxs = syntaxTree.GetRoot().DescendantNodesAndSelf().OfType<TypeDeclarationSyntax>();
            foreach (var tds in typeDeclarationSyntaxs)
            {
                Generate(context, semanticModel, tds, SourceType.Int64);
                Generate(context, semanticModel, tds, SourceType.Int32);
                Generate(context, semanticModel, tds, SourceType.String);
                Generate(context, semanticModel, tds, SourceType.Guid);
            }
        }
    }

    private void Generate(GeneratorExecutionContext context, SemanticModel semanticModel,
        TypeDeclarationSyntax classDef, SourceType sourceType)
    {
        var symbol = semanticModel.GetDeclaredSymbol(classDef);
        if (symbol is not INamedTypeSymbol) return;
        INamedTypeSymbol namedTypeSymbol = (INamedTypeSymbol)symbol;
        var isEntityId = namedTypeSymbol.Interfaces
            .SingleOrDefault(t => t.Name.StartsWith($"I{sourceType}StronglyTypedId"));
        if (isEntityId == null) return;
        string ns = namedTypeSymbol.ContainingNamespace.ToString();
        string className = namedTypeSymbol.Name;

        string source = $@"// <auto-generated/>
using NetCorePal.Extensions.Domain;
using System;
using System.ComponentModel;
namespace {ns}
{{
    [TypeConverter(typeof(EntityIdTypeConverter<{className}, {sourceType}>))]
    public partial record {className}({sourceType} Id) : I{sourceType}StronglyTypedId
    {{
        public static implicit operator {sourceType}({className} id) => id.Id;
        public static implicit operator {className}({sourceType} id) => new {className}(id);
        public override string ToString()
        {{
            return Id.ToString();
        }}
    }}
}}
";
        context.AddSource($"{className}.g.cs", source);
    }

    enum SourceType
    {
        String,
        Int64,
        Int32,
        Guid
    }
}