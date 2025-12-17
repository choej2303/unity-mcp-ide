using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using MCPForUnity.Editor.Helpers;

namespace MCPForUnity.Editor.Setup
{
    /// <summary>
    /// Helper class to check for Roslyn dependencies and toggle the USE_ROSLYN symbol.
    /// </summary>
    public static class RoslynSetup
    {
        private const string ROSLYN_SYMBOL = "USE_ROSLYN";
        // This type is available if Microsoft.CodeAnalysis.CSharp.dll is correctly loaded
        private const string ROSLYN_TYPE_NAME = "Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree, Microsoft.CodeAnalysis.CSharp";

        /// <summary>
        /// Checks if the Microsoft.CodeAnalysis.CSharp assembly is available in the project.
        /// </summary>
        public static bool IsRoslynAvailable()
        {
            try
            {
                var type = Type.GetType(ROSLYN_TYPE_NAME);
                return type != null;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Checks if the USE_ROSLYN symbol is currently defined for the active build target.
        /// </summary>
        public static bool IsRoslynEnabled()
        {
            string symbols = PlayerSettings.GetScriptingDefineSymbolsForGroup(EditorUserBuildSettings.selectedBuildTargetGroup);
            return symbols.Split(';').Contains(ROSLYN_SYMBOL);
        }

        /// <summary>
        /// Toggles the USE_ROSLYN symbol.
        /// </summary>
        public static void SetRoslynEnabled(bool enable)
        {
            var targetGroup = EditorUserBuildSettings.selectedBuildTargetGroup;
            string symbols = PlayerSettings.GetScriptingDefineSymbolsForGroup(targetGroup);
            var symbolList = symbols.Split(';').ToList();

            if (enable)
            {
                if (!symbolList.Contains(ROSLYN_SYMBOL))
                {
                    symbolList.Add(ROSLYN_SYMBOL);
                    McpLog.Info("Enabling USE_ROSLYN symbol...");
                }
            }
            else
            {
                if (symbolList.Contains(ROSLYN_SYMBOL))
                {
                    symbolList.Remove(ROSLYN_SYMBOL);
                    McpLog.Info("Disabling USE_ROSLYN symbol...");
                }
            }

            PlayerSettings.SetScriptingDefineSymbolsForGroup(targetGroup, string.Join(";", symbolList));
        }
    }
}
