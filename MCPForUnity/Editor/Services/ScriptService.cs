using System;
using System.IO;
using System.Security.Cryptography;
using UnityEngine;

namespace MCPForUnity.Editor.Services
{
    /// <summary>
    /// Service for handling script file I/O operations and path resolution.
    /// Ensures all operations are confined within the Assets directory.
    /// </summary>
    public static class ScriptService
    {
        public static string AssetsPath => MCPForUnity.Editor.Helpers.AssetPathUtility.AssetsPath;

        /// <summary>
        /// Validates that a path is within the Assets folder and resolves it to a full path.
        /// </summary>
        /// <summary>
        /// Validates that a path is within the Assets folder and resolves it to a full path.
        /// </summary>
        public static bool TryResolveUnderAssets(string relDir, out string fullPathDir, out string relPathSafe)
        {
            return MCPForUnity.Editor.Helpers.AssetPathUtility.TryResolveSecure(relDir, out fullPathDir, out relPathSafe);
        }

        public static bool FindScriptByGuid(string guid, out string fullPath)
        {
#if UNITY_EDITOR
            string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
            if (!string.IsNullOrEmpty(path))
            {
                fullPath = Path.GetFullPath(path).Replace('\\', '/');
                return true;
            }
#endif
            fullPath = null;
            return false;
        }

        // Just a helper to consolidate File.ReadAllText with error handling if needed, 
        // but simple System.IO calls are usually fine in the calling code if simple.
        // However, standardized error messages are good.
    }
}
