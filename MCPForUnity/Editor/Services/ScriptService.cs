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
        public static MCPForUnity.Editor.Services.Abstractions.IFileSystem FileSystem { get; set; } = new MCPForUnity.Editor.Services.Infrastructure.UnityFileSystem();

        public static Func<string> AssetsPathProvider = () => Application.dataPath.Replace('\\', '/');
        public static string AssetsPath => AssetsPathProvider();

        /// <summary>
        /// Validates that a path is within the Assets folder and resolves it to a full path.
        /// </summary>
        public static bool TryResolveUnderAssets(string relDir, out string fullPathDir, out string relPathSafe)
        {
            fullPathDir = null;
            relPathSafe = null;

            if (string.IsNullOrEmpty(relDir))
            {
                fullPathDir = AssetsPath;
                relPathSafe = "";
                return true;
            }

            // Normalize
            string r = relDir.Replace('\\', '/').Trim();
            
            // Remove typical prefixes if present
            if (r.StartsWith("unity://path/", StringComparison.OrdinalIgnoreCase)) 
                r = r.Substring("unity://path/".Length);
            while (r.StartsWith("Assets/Assets/", StringComparison.OrdinalIgnoreCase))
                r = r.Substring("Assets/".Length);
            if (r.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
                r = r.Substring("Assets/".Length);
            
            r = r.TrimStart('/');

            // Check for directory traversal
            if (r.Contains(".."))
                return false;

            string full = Path.GetFullPath(Path.Combine(AssetsPath, r)).Replace('\\', '/');

            // Must start with Assets path
            if (!full.StartsWith(AssetsPath, StringComparison.OrdinalIgnoreCase))
                return false;

            // Check for symlinks that might break out
            string checkPath = full;
            string rootPath = AssetsPath;
            try
            {
                while (checkPath != null && checkPath.Length >= rootPath.Length)
                {
                    if (FileSystem.IsSymlink(checkPath))
                         return false; // Symbolic link detected
                    
                    checkPath = Path.GetDirectoryName(checkPath);
                }
            }
            catch { /* best effort */ }

            fullPathDir = full;
            relPathSafe = r;
            return true;
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
