using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;

#if USE_ROSLYN
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Formatting;
#endif

namespace MCPForUnity.Editor.Services
{
    /// <summary>
    /// Encapsulates all Roslyn compiler service interactions.
    /// Safely handles cases where USE_ROSLYN is not defined by returning fallback or empty results.
    /// </summary>
    public static class RoslynService
    {
        public class DiagnosticResult
        {
            public int Line { get; set; }
            public int Col { get; set; }
            public string Code { get; set; }
            public string Message { get; set; }
        }

        public static bool IsRoslynEnabled()
        {
#if USE_ROSLYN
            return true;
#else
            return false;
#endif
        }

        public static List<DiagnosticResult> GetSyntaxDiagnostics(string sourceCode, int limit = 3)
        {
            var results = new List<DiagnosticResult>();
#if USE_ROSLYN
            try
            {
                var tree = CSharpSyntaxTree.ParseText(sourceCode);
                var diagnostics = tree.GetDiagnostics()
                    .Where(d => d.Severity == DiagnosticSeverity.Error)
                    .Take(limit);

                foreach (var d in diagnostics)
                {
                    var span = d.Location.GetLineSpan();
                    results.Add(new DiagnosticResult
                    {
                        Line = span.StartLinePosition.Line + 1,
                        Col = span.StartLinePosition.Character + 1,
                        Code = d.Id,
                        Message = d.GetMessage()
                    });
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[RoslynService] Validation failed: {ex.Message}");
            }
#endif
            return results;
        }

        public static string FormatCode(string sourceCode)
        {
#if USE_ROSLYN
            try
            {
                var tree = CSharpSyntaxTree.ParseText(sourceCode);
                var root = tree.GetRoot();
                var workspace = new AdhocWorkspace();
                var formattedRoot = Formatter.Format(root, workspace);
                return formattedRoot.ToFullString();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[RoslynService] Formatting failed: {ex.Message}");
                return sourceCode;
            }
#else
            return sourceCode;
#endif
        }
    }
}
