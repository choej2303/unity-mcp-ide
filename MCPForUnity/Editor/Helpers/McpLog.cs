using MCPForUnity.Editor.Constants;
using UnityEditor;
using UnityEngine;

namespace MCPForUnity.Editor.Helpers
{
    internal static class McpLog
    {
        private const string InfoPrefix = "<b><color=#2EA3FF>MCP-FOR-UNITY</color></b>:";
        private const string DebugPrefix = "<b><color=#6AA84F>MCP-FOR-UNITY</color></b>:";
        private const string WarnPrefix = "<b><color=#cc7a00>MCP-FOR-UNITY</color></b>:";
        private const string ErrorPrefix = "<b><color=#cc3333>MCP-FOR-UNITY</color></b>:";

        private static volatile bool _debugEnabled = ReadDebugPreference();
        private static readonly object _logLock = new object();
        private static string _logFilePath;

        static McpLog()
        {
            // Setup structured log path in Library (ephemeral)
            try
            {
                var projectDir = System.IO.Directory.GetParent(UnityEngine.Application.dataPath).FullName;
                var logDir = System.IO.Path.Combine(projectDir, "Library", "Logs", "MCP");
                if (!System.IO.Directory.Exists(logDir))
                    System.IO.Directory.CreateDirectory(logDir);
                
                _logFilePath = System.IO.Path.Combine(logDir, "mcp_events.jsonl");
            }
            catch { /* best effort */ }
        }

        private static bool IsDebugEnabled() => _debugEnabled;

        private static bool ReadDebugPreference()
        {
            try { return EditorPrefs.GetBool(EditorPrefKeys.DebugLogs, false); }
            catch { return false; }
        }

        public static void SetDebugLoggingEnabled(bool enabled)
        {
            _debugEnabled = enabled;
            try { EditorPrefs.SetBool(EditorPrefKeys.DebugLogs, enabled); }
            catch { }
        }

        public static void Debug(string message)
        {
            if (!IsDebugEnabled()) return;
            UnityEngine.Debug.Log($"{DebugPrefix} {message}");
            LogStructured("DEBUG", message);
        }

        public static void Info(string message, bool always = true)
        {
            if (!always && !IsDebugEnabled()) return;
            UnityEngine.Debug.Log($"{InfoPrefix} {message}");
            LogStructured("INFO", message);
        }

        public static void Warn(string message)
        {
            UnityEngine.Debug.LogWarning($"{WarnPrefix} {message}");
            LogStructured("WARN", message);
        }

        public static void Error(string message)
        {
            UnityEngine.Debug.LogError($"{ErrorPrefix} {message}");
            LogStructured("ERROR", message);
        }

        private static void LogStructured(string level, string message)
        {
            if (_logFilePath == null) return;
            
            try
            {
                var logEntry = new
                {
                    timestamp = System.DateTime.UtcNow.ToString("o"),
                    level = level,
                    message = message
                };

                // Simple JSON serialization to avoid Newtonsoft dependency inside this helper if feasible, 
                // but we have Newtonsoft available in the project.
                // Using manual string concat for speed/simplicity in logger? No, use JObject or simple formatting.
                // Or just string interpolation for this simple structure.
                string json = $"{{\"timestamp\":\"{logEntry.timestamp}\",\"level\":\"{logEntry.level}\",\"message\":\"{EscapeJson(message)}\"}}";
                
                lock (_logLock)
                {
                    System.IO.File.AppendAllText(_logFilePath, json + System.Environment.NewLine);
                }
            }
            catch { /* ignore logging errors */ }
        }

        private static string EscapeJson(string s)
        {
            if (s == null) return "";
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "").Replace("\n", "\\n");
        }
    }
}
