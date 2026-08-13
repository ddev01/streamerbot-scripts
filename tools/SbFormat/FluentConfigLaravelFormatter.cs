using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace SbFormat;
/// <summary>
/// After Roslyn NormalizeWhitespace (SB-compatible compact base), re-expand FluentConfig
/// fluent chains into Laravel-style line breaks. Non-fluent code stays collapsed.
/// </summary>
internal static class FluentConfigLaravelFormatter
{
    private const string Eol = "\r\n";
    private static readonly HashSet<string> UiMethods = new(StringComparer.Ordinal)
    {
        "Create",
        "Section",
        "Show",
        "ShowOrFocus",
        "Icon",
        "RepoUrl",
        "LogExistingSettings",
        "WithExtensionUpdateNotice",
    };
    private static readonly HashSet<string> ControlMethods = new(StringComparer.Ordinal)
    {
        "Button",
        "ColorPicker",
        "ConnectionStatus",
        "Dropdown",
        "DurationInput",
        "DynamicTextboxes",
        "Filepath",
        "Grid",
        "IntegerInput",
        "Intro",
        "NumberInput",
        "PillInput",
        "RepeatFor",
        "Row",
        "Separator",
        "Slider",
        "Textbox",
        "Title",
        "Toggle",
        "WithRepeatableRows",
        "WithVisibility",
        "WithVisibilityWhenOff",
    };
    private static readonly HashSet<string> OptionMethods = new(StringComparer.Ordinal)
    {
        "AllowDuplicates",
        "Color",
        "Default",
        "DefaultByValue",
        "DefaultIndex",
        "DefaultIndices",
        "Hint",
        "ItemTemplate",
        "MaxSelected",
        "Multiline",
        "OnClick",
        "OnPillAdded",
        "OnPillRemoved",
        "Options",
        "OptionsPairs",
        "Password",
        "Preset",
        "Range",
        "Refresh",
        "RefreshPairs",
        "ShowWhen",
        "Size",
        "Span",
        "Step",
        "Text",
        "WithExclusive",
        "WithPairValue",
        "WithPermanentOption",
        "WithStepper",
    };
    public static SyntaxNode Apply(SyntaxNode root)
    {
        var rewriter = new Rewriter();
        return rewriter.Visit(root) ?? root;
    }

    private sealed class Rewriter : CSharpSyntaxRewriter
    {
        public override SyntaxNode? VisitInvocationExpression(InvocationExpressionSyntax node)
        {
            // Nested lambda bodies (s => s.... / (row, i) => row....) are expanded via
            // FormatArgument when formatting the Create/Show root. Rewriting them here
            // too double-closes parentheses around WithVisibility + RepeatFor nests.
            if (node.Parent is SimpleLambdaExpressionSyntax simple && simple.ExpressionBody == node)
                return base.VisitInvocationExpression(node);
            if (node.Parent is ParenthesizedLambdaExpressionSyntax paren && paren.ExpressionBody == node)
                return base.VisitInvocationExpression(node);
            var rewritten = (InvocationExpressionSyntax)base.VisitInvocationExpression(node)!;
            if (!IsChainRoot(rewritten))
                return rewritten;
            if (!TryFlatten(rewritten, out var rootExpr, out var links))
                return rewritten;
            if (!IsFluentConfigChain(rootExpr, links))
                return rewritten;
            var stmtIndent = GetStatementIndent(rewritten);
            var formatted = FormatChain(rootExpr, links, stmtIndent, isLambdaParamRoot: false);
            return ParseExpr(formatted, rewritten);
        }
    }

    private static SyntaxNode ParseExpr(string formatted, SyntaxNode triviaSource)
    {
        var expr = SyntaxFactory.ParseExpression(formatted);
        return expr.WithLeadingTrivia(triviaSource.GetLeadingTrivia()).WithTrailingTrivia(triviaSource.GetTrailingTrivia());
    }

    private static bool IsChainRoot(InvocationExpressionSyntax node)
    {
        return node.Parent is not MemberAccessExpressionSyntax ma || ma.Parent is not InvocationExpressionSyntax || !ReferenceEquals(ma.Expression, node);
    }

    private static bool TryFlatten(ExpressionSyntax node, out ExpressionSyntax rootExpr, out List<(string Name, ArgumentListSyntax Args)> links)
    {
        links = [];
        ExpressionSyntax current = node;
        while (current is InvocationExpressionSyntax inv && inv.Expression is MemberAccessExpressionSyntax ma)
        {
            links.Insert(0, (ma.Name.Identifier.Text, inv.ArgumentList));
            current = ma.Expression;
        }

        rootExpr = current;
        return links.Count > 0;
    }

    private static bool IsFluentConfigChain(ExpressionSyntax rootExpr, List<(string Name, ArgumentListSyntax Args)> links)
    {
        if (rootExpr.ToString().Contains("FluentConfigUi", StringComparison.Ordinal))
            return true;
        foreach (var(name, _)in links)
        {
            if (UiMethods.Contains(name) || ControlMethods.Contains(name) || OptionMethods.Contains(name))
                return true;
        }

        return false;
    }

    private static bool IsIdentifierRoot(ExpressionSyntax expr) => expr is IdentifierNameSyntax;
    private static string FormatChain(ExpressionSyntax rootExpr, List<(string Name, ArgumentListSyntax Args)> links, int stmtIndentCols, bool isLambdaParamRoot)
    {
        var sb = new StringBuilder();
        sb.Append(StripOuterTrivia(rootExpr));
        for (var i = 0; i < links.Count; i++)
        {
            var(name, args) = links[i];
            var isFirst = i == 0;
            // Type.Create(...) stays on one line; break before every later link.
            // Lambda roots (s => s.Foo) break before the first .Foo as well.
            if (!isFirst || isLambdaParamRoot)
            {
                sb.Append(Eol);
                sb.Append(new string (' ', ContinuationIndent(name, stmtIndentCols, isLambdaParamRoot)));
            }

            sb.Append('.');
            sb.Append(name);
            sb.Append(FormatArgumentList(args, ContinuationIndent(name, stmtIndentCols, isLambdaParamRoot)));
        }

        return sb.ToString();
    }

    private static int ContinuationIndent(string methodName, int stmtIndentCols, bool isLambdaParamRoot)
    {
        // unformat.cs shape:
        //   stmt:        Create(...)
        //   stmt+4:          .Section(..., s => s
        //   stmt+8:              .Toggle(...)
        //   stmt+12:                 .Hint(...))
        //   stmt+4:          .Show();
        if (OptionMethods.Contains(methodName))
            return stmtIndentCols + (isLambdaParamRoot ? 8 : 8);
        if (ControlMethods.Contains(methodName))
            return stmtIndentCols + (isLambdaParamRoot ? 4 : 4);
        return stmtIndentCols + 4;
    }

    private static string FormatArgumentList(ArgumentListSyntax args, int chainIndentCols)
    {
        if (args.Arguments.Count == 0)
            return "()";
        // Keep argument lists compact (SB-style). Fluent newlines live inside lambda bodies only,
        // e.g. .Section("A", "B", s => s\r\n    .Toggle()...)
        var parts = new List<string>(args.Arguments.Count);
        foreach (var arg in args.Arguments)
            parts.Add(FormatArgument(arg, chainIndentCols));
        return "(" + string.Join(", ", parts) + ")";
    }

    private static string FormatArgument(ArgumentSyntax arg, int chainIndentCols)
    {
        if (arg.Expression is SimpleLambdaExpressionSyntax { ExpressionBody: not null } lambda)
        {
            var param = lambda.Parameter.Identifier.Text;
            if (lambda.ExpressionBody is InvocationExpressionSyntax body && TryFlatten(body, out var rootExpr, out var links) && IsFluentConfigChain(rootExpr, links))
            {
                var bodyText = FormatChain(rootExpr, links, chainIndentCols, isLambdaParamRoot: true);
                return param + " => " + bodyText;
            }
        }

        if (arg.Expression is ParenthesizedLambdaExpressionSyntax { ExpressionBody: not null } parenLambda)
        {
            var parms = string.Join(", ", parenLambda.ParameterList.Parameters.Select(p => p.Identifier.Text));
            if (parenLambda.ExpressionBody is InvocationExpressionSyntax body && TryFlatten(body, out var rootExpr, out var links) && IsFluentConfigChain(rootExpr, links))
            {
                var bodyText = FormatChain(rootExpr, links, chainIndentCols, isLambdaParamRoot: true);
                return "(" + parms + ") => " + bodyText;
            }
        }

        return StripOuterTrivia(arg);
    }

    private static string StripOuterTrivia(SyntaxNode node) => node.WithoutTrivia().ToFullString().Trim();
    private static int GetStatementIndent(SyntaxNode node)
    {
        // Prefer the containing statement's indent when available.
        var target = node.AncestorsAndSelf().FirstOrDefault(a => a is StatementSyntax or MemberDeclarationSyntax or EqualsValueClauseSyntax) ?? node;
        var tree = target.SyntaxTree;
        if (tree is null)
            return 0;
        var lineSpan = tree.GetLineSpan(target.Span);
        var text = tree.GetText();
        var line = text.Lines[lineSpan.StartLinePosition.Line];
        var lineText = text.ToString(line.Span);
        var cols = 0;
        foreach (var ch in lineText)
        {
            if (ch == ' ')
                cols++;
            else if (ch == '\t')
                cols += 4;
            else
                break;
        }

        return cols;
    }
}
