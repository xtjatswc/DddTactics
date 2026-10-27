using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace DddTactics.Analyzers;

/// <summary>
/// DddTactics 战术层分析器集合：
/// <list type="bullet">
///   <item><description>TACT001（警告）：聚合根属性直接引用另一聚合根（仅同程序集）。</description></item>
///   <item><description>TACT002（信息）：领域事件类名不以过去式结尾（启发式）。</description></item>
///   <item><description>TACT003（警告）：ValueObject 子类含可写 set（非 init）。</description></item>
/// </list>
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class DddTacticsAnalyzer : DiagnosticAnalyzer
{
    private const string AggregateRoot = "DddTactics.Core.AggregateRoot`1";
    private const string ValueObject = "DddTactics.Core.ValueObject";
    private const string DomainEvent = "DddTactics.Core.IDomainEvent";

    public static readonly DiagnosticDescriptor Tact001 = new(
        id: "TACT001",
        title: "聚合根不应直接引用另一聚合根",
        messageFormat: "聚合根 '{0}' 的属性/字段 '{1}' 直接引用了另一聚合根 '{2}'。跨聚合应通过 ID 引用，而非对象引用。",
        category: "Design",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor Tact002 = new(
        id: "TACT002",
        title: "领域事件类名建议以过去式结尾",
        messageFormat: "领域事件 '{0}' 的类名未以过去式结尾（如 Shipped/Paid/Cancelled）。这是启发式检查。",
        category: "Naming",
        defaultSeverity: DiagnosticSeverity.Info,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor Tact003 = new(
        id: "TACT003",
        title: "ValueObject 不应含可写 set",
        messageFormat: "ValueObject '{0}' 的属性 '{1}' 含有可写 set（非 init）。值对象应为不可变。",
        category: "Design",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(Tact001, Tact002, Tact003);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeClass, SyntaxKind.ClassDeclaration);
    }

    private static void AnalyzeClass(SyntaxNodeAnalysisContext context)
    {
        var classDecl = (ClassDeclarationSyntax)context.Node;
        var semanticModel = context.SemanticModel;

        if (semanticModel.GetDeclaredSymbol(classDecl) is not INamedTypeSymbol symbol)
        {
            return;
        }

        var compilation = context.Compilation;
        var aggregateRoot = compilation.GetTypeByMetadataName(AggregateRoot);
        var valueObject = compilation.GetTypeByMetadataName(ValueObject);
        var domainEvent = compilation.GetTypeByMetadataName(DomainEvent);

        var isAggregateRoot = aggregateRoot is not null && InheritsFrom(symbol, aggregateRoot);
        var isValueObject = valueObject is not null && InheritsFrom(symbol, valueObject);
        var isDomainEvent = domainEvent is not null && symbol.AllInterfaces.Any(i => SymbolEqualityComparer.Default.Equals(i, domainEvent));

        // TACT001：聚合根属性直接引用同程序集的另一聚合根
        if (isAggregateRoot && aggregateRoot is not null)
        {
            foreach (var member in classDecl.Members)
            {
                ITypeSymbol? memberType = null;
                SyntaxToken? nameToken = null;

                if (member is PropertyDeclarationSyntax prop)
                {
                    memberType = semanticModel.GetTypeInfo(prop.Type).Type;
                    nameToken = prop.Identifier;
                }
                else if (member is FieldDeclarationSyntax field)
                {
                    memberType = semanticModel.GetTypeInfo(field.Declaration.Type).Type;
                    nameToken = field.Declaration.Variables.FirstOrDefault()?.Identifier;
                }

                if (memberType is null || nameToken is null)
                {
                    continue;
                }

                if (InheritsFrom(memberType, aggregateRoot) &&
                    SymbolEqualityComparer.Default.Equals(memberType.ContainingAssembly, symbol.ContainingAssembly))
                {
                    var diagnostic = Diagnostic.Create(
                        Tact001,
                        nameToken.Value.GetLocation(),
                        symbol.Name,
                        nameToken.Value.Text,
                        memberType.Name);
                    context.ReportDiagnostic(diagnostic);
                }
            }
        }

        // TACT002：领域事件类名过去式启发式
        if (isDomainEvent && !IsPastTense(symbol.Name))
        {
            context.ReportDiagnostic(Diagnostic.Create(Tact002, classDecl.Identifier.GetLocation(), symbol.Name));
        }

        // TACT003：ValueObject 子类含可写 set（非 init）
        if (isValueObject)
        {
            foreach (var member in classDecl.Members.OfType<PropertyDeclarationSyntax>())
            {
                var setAccessor = member.AccessorList?.Accessors
                    .FirstOrDefault(a => a.Kind() == SyntaxKind.SetAccessorDeclaration);
                if (setAccessor is null)
                {
                    continue;
                }

                var setMethod = semanticModel.GetDeclaredSymbol(member)?.SetMethod;
                if (setMethod is not null && !setMethod.IsInitOnly)
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        Tact003,
                        member.Identifier.GetLocation(),
                        symbol.Name,
                        member.Identifier.Text));
                }
            }
        }
    }

    private static bool InheritsFrom(ITypeSymbol type, INamedTypeSymbol baseType)
    {
        var current = type as INamedTypeSymbol;
        while (current is not null)
        {
            if (SymbolEqualityComparer.Default.Equals(current, baseType) ||
                (current.IsGenericType && SymbolEqualityComparer.Default.Equals(current.ConstructedFrom, baseType)))
            {
                return true;
            }

            current = current.BaseType;
        }

        return false;
    }

    private static bool IsPastTense(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return false;
        }

        // 启发式：过去式常以 ed / d / t / n 结尾（Shipped、Paid、Sent、Done…）。
        return name.EndsWith("ed", System.StringComparison.OrdinalIgnoreCase) ||
               name.EndsWith("d", System.StringComparison.OrdinalIgnoreCase) ||
               name.EndsWith("t", System.StringComparison.OrdinalIgnoreCase) ||
               name.EndsWith("n", System.StringComparison.OrdinalIgnoreCase);
    }
}
