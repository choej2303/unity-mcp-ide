using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json.Linq;

namespace MCPForUnity.Editor.Services
{
    /// <summary>
    /// Service for low-level code text manipulation, index conversion, and basic structural validation.
    /// Pure C# logic with no Unity API dependencies where possible.
    /// </summary>
    public static class CodeEditingService
    {
        public static string ComputeSha256(string contents)
        {
            using (var sha = SHA256.Create())
            {
                var bytes = Encoding.UTF8.GetBytes(contents);
                var hash = sha.ComputeHash(bytes);
                return BitConverter.ToString(hash).Replace("-", string.Empty).ToLowerInvariant();
            }
        }

        public static bool TryIndexFromLineCol(string text, int line1, int col1, out int index)
        {
            // 1-based line/col to absolute index (0-based)
            if (string.IsNullOrEmpty(text))
            {
                index = -1;
                return false;
            }

            int line = 1, col = 1;
            for (int i = 0; i < text.Length; i++) // Allow i == text.Length for end of file
            {
                if (line == line1 && col == col1)
                {
                    index = i;
                    return true;
                }
                if (i == text.Length) break;

                char c = text[i];
                if (c == '\r')
                {
                    if (i + 1 < text.Length && text[i + 1] == '\n')
                        i++;
                    line++;
                    col = 1;
                }
                else if (c == '\n')
                {
                    line++;
                    col = 1;
                }
                else
                {
                    col++;
                }
            }
            // Check if it's strictly at the EOF position
            if (line == line1 && col == col1)
            {
                index = text.Length;
                return true;
            }

            index = -1;
            return false;
        }

        public static bool CheckBalancedDelimiters(string text, out int line, out char expected)
        {
            var braceStack = new Stack<int>();
            var parenStack = new Stack<int>();
            var bracketStack = new Stack<int>();
            bool inString = false, inChar = false, inSingle = false, inMulti = false, escape = false;
            line = 1; expected = '\0';

            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                char next = i + 1 < text.Length ? text[i + 1] : '\0';

                if (c == '\n') { line++; if (inSingle) inSingle = false; }

                if (escape) { escape = false; continue; }

                if (inString)
                {
                    if (c == '\\') { escape = true; }
                    else if (c == '"') inString = false;
                    continue;
                }
                if (inChar)
                {
                    if (c == '\\') { escape = true; }
                    else if (c == '\'') inChar = false;
                    continue;
                }
                if (inSingle) continue;
                if (inMulti)
                {
                    if (c == '*' && next == '/') { inMulti = false; i++; }
                    continue;
                }

                if (c == '"') { inString = true; continue; }
                if (c == '\'') { inChar = true; continue; }
                if (c == '/' && next == '/') { inSingle = true; i++; continue; }
                if (c == '/' && next == '*') { inMulti = true; i++; continue; }

                switch (c)
                {
                    case '{': braceStack.Push(line); break;
                    case '}':
                        if (braceStack.Count == 0) { expected = '{'; return false; }
                        braceStack.Pop();
                        break;
                    case '(': parenStack.Push(line); break;
                    case ')':
                        if (parenStack.Count == 0) { expected = '('; return false; }
                        parenStack.Pop();
                        break;
                    case '[': bracketStack.Push(line); break;
                    case ']':
                        if (bracketStack.Count == 0) { expected = '['; return false; }
                        bracketStack.Pop();
                        break;
                }
            }

            if (braceStack.Count > 0) { line = braceStack.Peek(); expected = '}'; return false; }
            if (parenStack.Count > 0) { line = parenStack.Peek(); expected = ')'; return false; }
            if (bracketStack.Count > 0) { line = bracketStack.Peek(); expected = ']'; return false; }

            return true;
        }

        public static bool CheckScopedBalance(string text, int start, int end)
        {
            start = Math.Max(0, Math.Min(text.Length, start));
            end = Math.Max(start, Math.Min(text.Length, end));
            int brace = 0, paren = 0, bracket = 0;
            bool inStr = false, inChr = false, esc = false;
            for (int i = start; i < end; i++)
            {
                char c = text[i];
                char n = (i + 1 < end) ? text[i + 1] : '\0';
                if (inStr)
                {
                    if (!esc && c == '"') inStr = false; esc = (!esc && c == '\\'); continue;
                }
                if (inChr)
                {
                    if (!esc && c == '\'') inChr = false; esc = (!esc && c == '\\'); continue;
                }
                if (c == '"') { inStr = true; esc = false; continue; }
                if (c == '\'') { inChr = true; esc = false; continue; }
                if (c == '/' && n == '/') { while (i < end && text[i] != '\n') i++; continue; }
                if (c == '/' && n == '*') { i += 2; while (i + 1 < end && !(text[i] == '*' && text[i + 1] == '/')) i++; i++; continue; }
                if (c == '{') brace++;
                else if (c == '}') brace--;
                else if (c == '(') paren++;
                else if (c == ')') paren--;
                else if (c == '[') bracket++; else if (c == ']') bracket--;
            }
            return brace >= -3 && paren >= -3 && bracket >= -3;
        }

        public static string ApplyEditsPure(string original, List<(int start, int end, string text)> spans)
        {
            // Spans must be non-overlapping and sorted descending by start index
            string working = original;
            foreach (var sp in spans)
            {
                working = working.Remove(sp.start, sp.end - sp.start).Insert(sp.start, sp.text ?? string.Empty);
            }
            return working;
        }
    }
}
