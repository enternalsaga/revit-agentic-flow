using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace RevitMcpPlugin.AI.Roslyn;

/// <summary>
/// Roslyn-based static analyzer for Revit C# code.
/// Analyzers:
///   RMCP001: Missing Transaction wrapper for modification API calls
///   RMCP002: FilteredElementCollector not properly disposed
///   RMCP003: FilteredElementCollector missing ElementType filter (ghost object defense)
///   RMCP004: Deprecated Revit 2024+ API usage
///   RMCP005: Unsafe XYZ operations (divide by zero, normalize zero-length)
/// CodeFixProvider: Simple pattern-based automatic fixes.
/// </summary>
public class RoslynAnalyzerService
{
    private string? _revitVersion;

    public string RevitVersion
    {
        get => _revitVersion ?? "2025";
        set => _revitVersion = value;
    }

    private int MajorVersion
    {
        get
        {
            string ver = RevitVersion;
            if (ver.Contains('.'))
                ver = ver.Substring(0, ver.IndexOf('.'));
            int.TryParse(ver, out int major);
            return major;
        }
    }

    // Revit modification APIs that require a Transaction
    private static readonly HashSet<string> ModificationApis = new(StringComparer.OrdinalIgnoreCase)
    {
        "Create", "NewFamilyInstance", "NewWall", "NewFloor", "NewRoof",
        "Delete", "Move", "RotateElement", "MirrorElement", "Copy",
        "set_Parameter", "Set", "SetValueString",
        "Start", "Commit", "RollBack",
        "SetElementOverrides", "SetCategoryOverrides"
    };

    // Revit 2024+ removed/changed APIs
    private static readonly Dictionary<string, string> DeprecatedApiReplacements =
        new(StringComparer.OrdinalIgnoreCase)
    {
        { "IntegerValue", "Value" },
        { "LevelId", "get_Parameter(BuiltInParameter.WALL_BASE_CONSTRAINT)" },
        { "AsInteger", "AsValueString" }
    };

    // Known dangerous XYZ operations
    private static readonly HashSet<string> DangerousXyzOps = new(StringComparer.OrdinalIgnoreCase)
    {
        "Divide", "Normalize"
    };

    /// <summary>
    /// Run all RMCP analyzers on the given source code.
    /// Returns a list of diagnostics (warnings/errors).
    /// </summary>
    public AnalyzerReport Analyze(string sourceCode)
    {
        var report = new AnalyzerReport();

        try
        {
            var tree = CSharpSyntaxTree.ParseText(sourceCode,
                new CSharpParseOptions(LanguageVersion.Latest));
            var root = tree.GetRoot();

            report.Diagnostics.AddRange(CheckRMCP001_TransactionRequired(root));
            report.Diagnostics.AddRange(CheckRMCP002_CollectorDisposal(root));
            report.Diagnostics.AddRange(CheckRMCP003_GhostObjectFilter(root));
            report.Diagnostics.AddRange(CheckRMCP004_DeprecatedApi(root));
            report.Diagnostics.AddRange(CheckRMCP005_XyzSafety(root));
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[RoslynAnalyzerService] Analyzer error: {ex}");
            report.Diagnostics.Add(new AnalyzerDiagnostic
            {
                Id = "RMCP000",
                Message = $"Analyzer internal error: {ex.Message}",
                Severity = AnalyzerSeverity.Warning,
                Line = 0
            });
        }

        return report;
    }

    /// <summary>
    /// Apply automatic code fixes that don't require LLM.
    /// Returns the fixed source code and a list of applied fixes.
    /// </summary>
    public CodeFixResult ApplyAutoFixes(string sourceCode)
    {
        var result = new CodeFixResult { OriginalCode = sourceCode };
        string code = sourceCode;
        int major = MajorVersion;

        // Only apply 2024+ fixes when targeting Revit 2024+
        bool apply2024Fixes = major == 0 || major >= 2024;

        if (apply2024Fixes)
        {
            // Fix 1: IntegerValue -> Value
            if (code.Contains(".IntegerValue"))
            {
                code = code.Replace(".IntegerValue", ".Value");
                result.AppliedFixes.Add("RMCP004-FIX: IntegerValue -> Value");
            }

            // Fix 2: AsInteger() -> AsValueString() (common Revit 2024+ change)
            if (code.Contains(".AsInteger()"))
            {
                code = code.Replace(".AsInteger()", ".AsValueString()");
                result.AppliedFixes.Add("RMCP004-FIX: AsInteger() -> AsValueString()");
            }

            // Fix 3: element.LevelId -> element.get_Parameter(BuiltInParameter.WALL_BASE_CONSTRAINT).AsElementId()
            if (code.Contains(".LevelId") && !code.Contains("get_Parameter"))
            {
                code = Regex.Replace(
                    code,
                    @"(\w+)\.LevelId\b",
                    "$1.get_Parameter(Autodesk.Revit.DB.BuiltInParameter.WALL_BASE_CONSTRAINT).AsElementId()");
                result.AppliedFixes.Add("RMCP004-FIX: .LevelId -> get_Parameter(WALL_BASE_CONSTRAINT).AsElementId()");
            }

            // Fix 4: new CurveLoop(list) — flag only
            if (code.Contains("new CurveLoop(") && code.Contains("CurveLoop(") &&
                !code.Contains("new CurveLoop()"))
            {
                result.SuggestedFixes.Add(
                    "RMCP004-SUGGEST: CurveLoop constructor with list parameter removed in Revit 2024+. " +
                    "Use 'var loop = new CurveLoop(); foreach(var c in curves) loop.Append(c);'");
            }
        }

        result.FixedCode = code;
        result.HasChanges = !string.Equals(sourceCode, code, StringComparison.Ordinal);
        return result;
    }

    #region RMCP001: Transaction Required

    /// <summary>
    /// RMCP001: Detect modification API calls outside a Transaction block.
    /// </summary>
    private List<AnalyzerDiagnostic> CheckRMCP001_TransactionRequired(SyntaxNode root)
    {
        var diagnostics = new List<AnalyzerDiagnostic>();

        var invocations = root.DescendantNodes().OfType<InvocationExpressionSyntax>();

        foreach (var invocation in invocations)
        {
            string methodName = GetMethodName(invocation) ?? "";
            if (methodName.Length == 0) continue;
            if (!ModificationApis.Contains(methodName)) continue;

            // Skip if it's Transaction.Start/Commit/RollBack itself
            if (methodName is "Start" or "Commit" or "RollBack")
            {
                string receiver = GetReceiverText(invocation);
                if (receiver.Contains("tx") || receiver.Contains("transaction") ||
                    receiver.Contains("Transaction"))
                    continue;
            }

            // Check if this invocation is inside a Transaction using block
            if (!IsInsideTransactionBlock(invocation))
            {
                var lineSpan = invocation.GetLocation().GetLineSpan();
                diagnostics.Add(new AnalyzerDiagnostic
                {
                    Id = "RMCP001",
                    Message = $"Modification API '{methodName}' called outside a Transaction block. " +
                              "Wrap in 'using (var tx = new Transaction(doc, \"name\")) { tx.Start(); ... tx.Commit(); }'",
                    Severity = AnalyzerSeverity.Error,
                    Line = lineSpan.StartLinePosition.Line + 1,
                    Column = lineSpan.StartLinePosition.Character + 1
                });
            }
        }

        return diagnostics;
    }

    private bool IsInsideTransactionBlock(SyntaxNode node)
    {
        var current = node.Parent;
        while (current != null)
        {
            if (current is UsingStatementSyntax usingStmt)
            {
                string usingText = usingStmt.Declaration?.ToString() ?? usingStmt.Expression?.ToString() ?? "";
                if (ContainsTransactionType(usingText))
                    return true;
            }

            if (current is LocalDeclarationStatementSyntax localDecl && localDecl.UsingKeyword.IsKind(SyntaxKind.UsingKeyword))
            {
                string declText = localDecl.ToString();
                if (ContainsTransactionType(declText))
                    return true;
            }

            current = current.Parent;
        }
        return false;
    }

    private static readonly Regex TransactionTypeRegex = new(
        @"\bTransaction\b(?!Group)",
        RegexOptions.Compiled);

    private static bool ContainsTransactionType(string text)
    {
        return !string.IsNullOrEmpty(text) && TransactionTypeRegex.IsMatch(text);
    }

    #endregion

    #region RMCP002: Collector Disposal

    /// <summary>
    /// RMCP002: Detect FilteredElementCollector not properly disposed.
    /// Collectors should be used inline or in a using statement.
    /// </summary>
    private List<AnalyzerDiagnostic> CheckRMCP002_CollectorDisposal(SyntaxNode root)
    {
        var diagnostics = new List<AnalyzerDiagnostic>();

        var declarations = root.DescendantNodes().OfType<VariableDeclarationSyntax>()
            .Where(d => d.Type.ToString().Contains("FilteredElementCollector"));

        foreach (var decl in declarations)
        {
            var parent = decl.Parent;

            // OK if in a using statement
            if (parent is UsingStatementSyntax) continue;
            if (parent is LocalDeclarationStatementSyntax localDecl &&
                localDecl.UsingKeyword.IsKind(SyntaxKind.UsingKeyword))
                continue;

            foreach (var variable in decl.Variables)
            {
                string varName = variable.Identifier.Text;

                var methodBody = decl.FirstAncestorOrSelf<BlockSyntax>();
                if (methodBody == null) continue;

                bool hasDispose = methodBody.DescendantNodes()
                    .OfType<InvocationExpressionSyntax>()
                    .Any(inv =>
                    {
                        string text = inv.ToString();
                        return text.Contains(varName + ".Dispose()");
                    });

                if (!hasDispose)
                {
                    var lineSpan = variable.GetLocation().GetLineSpan();
                    diagnostics.Add(new AnalyzerDiagnostic
                    {
                        Id = "RMCP002",
                        Message = $"FilteredElementCollector '{varName}' is not disposed. " +
                                  "Use 'using var' or call .Dispose() to prevent memory leaks.",
                        Severity = AnalyzerSeverity.Warning,
                        Line = lineSpan.StartLinePosition.Line + 1,
                        Column = lineSpan.StartLinePosition.Character + 1
                    });
                }
            }
        }

        return diagnostics;
    }

    #endregion

    #region RMCP003: Ghost Object Filter

    /// <summary>
    /// RMCP003: Detect FilteredElementCollector usage without ElementType filter.
    /// Ghost object defense at compile time.
    /// </summary>
    private List<AnalyzerDiagnostic> CheckRMCP003_GhostObjectFilter(SyntaxNode root)
    {
        var diagnostics = new List<AnalyzerDiagnostic>();

        var collectorCreations = root.DescendantNodes()
            .OfType<ObjectCreationExpressionSyntax>()
            .Where(o => o.Type.ToString().Contains("FilteredElementCollector"));

        foreach (var creation in collectorCreations)
        {
            var chainRoot = GetExpressionChainRoot(creation);
            string fullChain = chainRoot.ToString();

            bool hasTypeFilter =
                fullChain.Contains("WhereElementIsNotElementType") ||
                fullChain.Contains("WhereElementIsElementType") ||
                fullChain.Contains("OfClass") ||
                fullChain.Contains("OfCategoryId");

            if (!hasTypeFilter)
            {
                var lineSpan = creation.GetLocation().GetLineSpan();
                diagnostics.Add(new AnalyzerDiagnostic
                {
                    Id = "RMCP003",
                    Message = "FilteredElementCollector missing ElementType filter. " +
                              "Add .WhereElementIsNotElementType() to exclude ghost objects (ElementType, AnalyticalModel, etc.). " +
                              "This prevents runtime crashes from accessing system internal elements.",
                    Severity = AnalyzerSeverity.Warning,
                    Line = lineSpan.StartLinePosition.Line + 1,
                    Column = lineSpan.StartLinePosition.Character + 1
                });
            }
        }

        return diagnostics;
    }

    /// <summary>
    /// Walk up the syntax tree to find the root of a method chain expression.
    /// </summary>
    private static SyntaxNode GetExpressionChainRoot(SyntaxNode node)
    {
        var current = node;
        while (current.Parent is MemberAccessExpressionSyntax ||
               current.Parent is InvocationExpressionSyntax ||
               current.Parent is MemberBindingExpressionSyntax)
        {
            current = current.Parent;
        }

        // Also check if the collector is assigned to a variable and chained later
        if (current.Parent is EqualsValueClauseSyntax eq &&
            eq.Parent is VariableDeclaratorSyntax varDecl)
        {
            string varName = varDecl.Identifier.Text;
            var block = varDecl.FirstAncestorOrSelf<BlockSyntax>();
            if (block != null)
            {
                var usages = block.DescendantNodes()
                    .OfType<IdentifierNameSyntax>()
                    .Where(id => id.Identifier.Text == varName);

                foreach (var usage in usages)
                {
                    var usageChain = GetExpressionChainRoot(usage);
                    string usageText = usageChain.ToString();
                    if (usageText.Contains("WhereElementIsNotElementType") ||
                        usageText.Contains("WhereElementIsElementType") ||
                        usageText.Contains("OfClass") ||
                        usageText.Contains("OfCategoryId"))
                    {
                        return usageChain;
                    }
                }
            }
        }

        return current;
    }

    #endregion

    #region RMCP004: Deprecated API

    /// <summary>
    /// RMCP004: Detect Revit 2024+ removed/changed API usage.
    /// Uses hardcoded patterns only (MVP — no XML index).
    /// </summary>
    private List<AnalyzerDiagnostic> CheckRMCP004_DeprecatedApi(SyntaxNode root)
    {
        var diagnostics = new List<AnalyzerDiagnostic>();
        int major = MajorVersion;

        bool check2024Deprecations = major == 0 || major >= 2024;

        var memberAccesses = root.DescendantNodes().OfType<MemberAccessExpressionSyntax>();

        foreach (var access in memberAccesses)
        {
            string memberName = access.Name.Identifier.Text;

            if (check2024Deprecations && DeprecatedApiReplacements.TryGetValue(memberName, out string? replacement))
            {
                var lineSpan = access.GetLocation().GetLineSpan();
                diagnostics.Add(new AnalyzerDiagnostic
                {
                    Id = "RMCP004",
                    Message = $"'{memberName}' is removed/changed in Revit 2024+. Use '{replacement}' instead.",
                    Severity = major >= 2024 ? AnalyzerSeverity.Error : AnalyzerSeverity.Warning,
                    Line = lineSpan.StartLinePosition.Line + 1,
                    Column = lineSpan.StartLinePosition.Character + 1,
                    SuggestedFix = replacement
                });
            }
        }

        // Check CurveLoop constructor with list parameter (2024+ removal)
        if (check2024Deprecations)
        {
            var curveLoopCreations = root.DescendantNodes()
                .OfType<ObjectCreationExpressionSyntax>()
                .Where(o => o.Type.ToString().Contains("CurveLoop") &&
                            o.ArgumentList != null &&
                            o.ArgumentList.Arguments.Count > 0);

            foreach (var creation in curveLoopCreations)
            {
                var lineSpan = creation.GetLocation().GetLineSpan();
                diagnostics.Add(new AnalyzerDiagnostic
                {
                    Id = "RMCP004",
                    Message = "CurveLoop constructor with list parameter removed in Revit 2024+. " +
                              "Use 'var loop = new CurveLoop(); foreach(var c in curves) loop.Append(c);'",
                    Severity = major >= 2024 ? AnalyzerSeverity.Error : AnalyzerSeverity.Warning,
                    Line = lineSpan.StartLinePosition.Line + 1,
                    Column = lineSpan.StartLinePosition.Character + 1
                });
            }
        }

        return diagnostics;
    }

    #endregion

    #region RMCP005: XYZ Safety

    /// <summary>
    /// RMCP005: Detect potentially unsafe XYZ operations.
    /// Division by zero or normalization of zero-length vectors.
    /// </summary>
    private List<AnalyzerDiagnostic> CheckRMCP005_XyzSafety(SyntaxNode root)
    {
        var diagnostics = new List<AnalyzerDiagnostic>();

        var invocations = root.DescendantNodes().OfType<InvocationExpressionSyntax>();

        foreach (var invocation in invocations)
        {
            string methodName = GetMethodName(invocation) ?? "";
            if (methodName.Length == 0) continue;

            if (DangerousXyzOps.Contains(methodName))
            {
                bool hasGuard = HasSafetyGuard(invocation);

                if (!hasGuard)
                {
                    var lineSpan = invocation.GetLocation().GetLineSpan();
                    diagnostics.Add(new AnalyzerDiagnostic
                    {
                        Id = "RMCP005",
                        Message = $"XYZ.{methodName}() can throw if vector length is zero. " +
                                  "Add a length check: 'if (vector.GetLength() > 1e-9)' before calling.",
                        Severity = AnalyzerSeverity.Warning,
                        Line = lineSpan.StartLinePosition.Line + 1,
                        Column = lineSpan.StartLinePosition.Character + 1
                    });
                }
            }
        }

        return diagnostics;
    }

    private static bool HasSafetyGuard(InvocationExpressionSyntax invocation)
    {
        // Check if the invocation is inside a try-catch
        var current = invocation.Parent;
        while (current != null)
        {
            if (current is TryStatementSyntax) return true;

            if (current is IfStatementSyntax ifStmt)
            {
                string condition = ifStmt.Condition.ToString().ToLowerInvariant();
                if (condition.Contains("getlength") || condition.Contains("length") ||
                    condition.Contains("iszerolength") || condition.Contains("normalize"))
                    return true;
            }

            if (current is MethodDeclarationSyntax) break;
            current = current.Parent;
        }

        // Check if there's an if-guard in the same block before this invocation
        var block = invocation.FirstAncestorOrSelf<BlockSyntax>();
        if (block == null) return false;

        var ifStatements = block.DescendantNodes()
            .OfType<IfStatementSyntax>()
            .Where(ifs => ifs.SpanStart < invocation.SpanStart);

        foreach (var ifStmt in ifStatements)
        {
            string condition = ifStmt.Condition.ToString().ToLowerInvariant();
            if (condition.Contains("getlength") || condition.Contains("length") ||
                condition.Contains("iszerolength") || condition.Contains("normalize"))
                return true;
        }

        return false;
    }

    #endregion

    #region Helpers

    private static string? GetMethodName(InvocationExpressionSyntax invocation)
    {
        if (invocation.Expression is MemberAccessExpressionSyntax memberAccess)
            return memberAccess.Name.Identifier.Text;
        if (invocation.Expression is IdentifierNameSyntax identifier)
            return identifier.Identifier.Text;
        return null;
    }

    private static string GetReceiverText(InvocationExpressionSyntax invocation)
    {
        if (invocation.Expression is MemberAccessExpressionSyntax memberAccess)
            return memberAccess.Expression.ToString();
        return string.Empty;
    }

    #endregion
}
