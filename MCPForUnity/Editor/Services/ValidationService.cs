using System;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using UnityEngine;
using System.Collections.Generic;

#if USE_ROSLYN
using Microsoft.CodeAnalysis;
// RoslynService handles the details
#endif

namespace MCPForUnity.Editor.Services
{
    public enum ValidationLevel
    {
        Basic,
        Standard,
        Strict,
        Comprehensive
    }

    /// <summary>
    /// Service for validating C# script syntax and semantics.
    /// Delegates to RoslynService if available, otherwise uses basic structural checks and regex-based heuristics.
    /// </summary>
    public static class ValidationService
    {
        public static bool ValidateScriptSyntax(string scriptContent, ValidationLevel level, out string[] diagnostics)
        {
            var diagList = new List<string>();

            if (string.IsNullOrEmpty(scriptContent))
            {
                diagnostics = new[] { "ERROR: Script content is empty." };
                return false;
            }

            // LEVEL 1: Basic structural validation (always run)
            if (!ValidateBasicStructure(scriptContent, out string basicError))
            {
                diagList.Add(basicError);
                diagnostics = diagList.ToArray();
                return false;
            }

            // LEVEL 2: Roslyn Validation (if enabled)
            if (RoslynService.IsRoslynEnabled())
            {
                var roslynDiags = RoslynService.GetSyntaxDiagnostics(scriptContent);
                foreach (var d in roslynDiags)
                {
                    diagList.Add($"ERROR: {d.Message} (Line {d.Line})");
                }
            }

            // LEVEL 3: Unity & Semantic Checks (Regex-based)
            if (level >= ValidationLevel.Standard)
            {
                ValidateScriptSyntaxUnity(scriptContent, diagList);
                ValidateSemanticRules(scriptContent, diagList);
            }

            diagnostics = diagList.ToArray();
            return !diagList.Any(d => d.StartsWith("ERROR"));
        }

        private static bool ValidateBasicStructure(string content, out string error)
        {
            if (!CodeEditingService.CheckBalancedDelimiters(content, out int line, out char expected))
            {
                // Find context around the error
                var lines = content.Split('\n');
                int startLine = Math.Max(0, line - 2);
                int endLine = Math.Min(lines.Length - 1, line + 1);
                string snippet = string.Join("\n", lines.Skip(startLine).Take(endLine - startLine + 1));
                
                error = $"ERROR: Unbalanced delimiter '{expected}' expected near line {line}.\nContext:\n{snippet}";
                return false;
            }
            error = null;
            return true;
        }

        private static void ValidateScriptSyntaxUnity(string contents, List<string> errors)
        {
            // Check for common Unity anti-patterns
            if (contents.Contains("FindObjectOfType") && contents.Contains("Update()"))
            {
                errors.Add("WARNING: FindObjectOfType in Update() can cause performance issues");
            }

            if (contents.Contains("GameObject.Find") && contents.Contains("Update()"))
            {
                errors.Add("WARNING: GameObject.Find in Update() can cause performance issues");
            }

            // Check for proper MonoBehaviour usage
            if (contents.Contains(": MonoBehaviour") && !contents.Contains("using UnityEngine"))
            {
                errors.Add("WARNING: MonoBehaviour requires 'using UnityEngine;'");
            }

            // Check for SerializeField usage
            if (contents.Contains("[SerializeField]") && !contents.Contains("using UnityEngine"))
            {
                errors.Add("WARNING: SerializeField requires 'using UnityEngine;'");
            }

            // Check for proper coroutine usage
            if (contents.Contains("StartCoroutine") && !contents.Contains("IEnumerator"))
            {
                errors.Add("WARNING: StartCoroutine typically requires IEnumerator methods");
            }

            // Check for Update without FixedUpdate for physics
            if (contents.Contains("Rigidbody") && contents.Contains("Update()") && !contents.Contains("FixedUpdate()"))
            {
                errors.Add("WARNING: Consider using FixedUpdate() for Rigidbody operations");
            }

            // Check for missing null checks on Unity objects
            if (contents.Contains("GetComponent<") && !contents.Contains("!= null"))
            {
                errors.Add("WARNING: Consider null checking GetComponent results");
            }

            // Check for proper event function signatures
            if (contents.Contains("void Start(") && !contents.Contains("void Start()"))
            {
                errors.Add("WARNING: Start() should not have parameters");
            }

            if (contents.Contains("void Update(") && !contents.Contains("void Update()"))
            {
                errors.Add("WARNING: Update() should not have parameters");
            }

            // Check for inefficient string operations
            if (contents.Contains("Update()") && contents.Contains("\"") && contents.Contains("+"))
            {
                errors.Add("WARNING: String concatenation in Update() can cause garbage collection issues");
            }
        }

        private static void ValidateSemanticRules(string contents, List<string> errors)
        {
            // Check for potential memory leaks
            if (contents.Contains("new ") && contents.Contains("Update()"))
            {
                errors.Add("WARNING: Creating objects in Update() may cause memory issues");
            }

            // Check for magic numbers
            var magicNumberPattern = new Regex(@"\b\d+\.?\d*f?\b(?!\s*[;})\]])", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
            try {
                if (magicNumberPattern.IsMatch(contents)) {
                     // Too noisy to check every single number, just a heuristic count if needed
                     // Simplified for now to avoid false positives
                }
            } catch {}

            // Check for proper exception handling
            if (contents.Contains("catch") && contents.Contains("catch()"))
            {
                errors.Add("WARNING: Empty catch blocks should be avoided");
            }

            // Check for proper async/await usage
            if (contents.Contains("async ") && !contents.Contains("await"))
            {
                errors.Add("WARNING: Async method should contain await or return Task");
            }

            // Check for hardcoded tags and layers
            if (contents.Contains("\"Player\"") || contents.Contains("\"Enemy\""))
            {
                errors.Add("WARNING: Consider using constants for tags instead of hardcoded strings");
            }
        }
    }
}
