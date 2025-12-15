using System;
using System.IO;
using UnityEditor;
using MCPForUnity.Editor.Constants;

namespace MCPForUnity.Editor.Helpers
{
    /// <summary>
    /// Factory for generating the MCP server execution command.
    /// Centralizes the logic for determining whether to use Node.js wrapper, UVX, HTTP vs Stdio, etc.
    /// Replaces scattered logic in ServerManagementService, ConfigJsonBuilder, etc.
    /// </summary>
    public static class McpServerCommandLoader
    {
        public struct ServerCommandInfo
        {
            public string Executable;
            public string Arguments;
            public string FullCommand; // Executable + Arguments
            public string Error;
            public bool IsNodeWrapper;
        }

        public static ServerCommandInfo GenerateCommand(bool useHttp)
        {
            var info = new ServerCommandInfo();

            if (!useHttp)
            {
                // PRIORITY 1: Node.js Wrapper (Stdio Mode)
                string wrapperPath = AssetPathUtility.GetWrapperJsPath();
                if (!string.IsNullOrEmpty(wrapperPath))
                {
                    string nodeCommand = "node";
                    string nodeOverride = EditorPrefs.GetString(EditorPrefKeys.NodePathOverride, "");
                    if (!string.IsNullOrEmpty(nodeOverride) && File.Exists(nodeOverride))
                    {
                        nodeCommand = nodeOverride;
                    }

                    string safeWrapperPath = wrapperPath.Contains(" ") ? $"\"{wrapperPath}\"" : wrapperPath;
                    
                    info.Executable = nodeCommand;
                    info.Arguments = safeWrapperPath;
                    info.FullCommand = $"{nodeCommand} {safeWrapperPath}";
                    info.IsNodeWrapper = true;
                    return info;
                }
            }

            // PRIORITY 2: UVX (HTTP Mode OR Stdio Fallback)
            var (uvxPath, fromUrl, packageName) = AssetPathUtility.GetUvxCommandParts();
            string port = HttpEndpointUtility.GetPort().ToString();
            
            // Validate availability
            // Note: We don't strictly validate 'uv' existence here to allow pure string generation for config files
            // generic 'Find' logic is handled by AssetPathUtility

            string transportArgs = useHttp 
                ? $"--transport sse --port {port}" 
                : "--transport stdio";

            string args;
            if (!string.IsNullOrEmpty(fromUrl))
            {
                args = $"--from {fromUrl} {packageName} {transportArgs}";
            }
            else
            {
                args = $"{packageName} {transportArgs}";
            }

            info.Executable = uvxPath;
            info.Arguments = args;
            info.FullCommand = $"{uvxPath} {args}";
            info.IsNodeWrapper = false;

            if (string.IsNullOrEmpty(uvxPath))
            {
                info.Error = "Could not locate 'uv' or 'uvx'. Please check Advanced Settings.";
            }

            return info;
        }
        
        /// <summary>
        /// Returns the arguments array for JSON config files (e.g. VSCode, Claude).
        /// </summary>
        public static string[] GenerateJsonArgs(bool useHttp)
        {
            if (!useHttp)
            {
                 string wrapperPath = AssetPathUtility.GetWrapperJsPath();
                 if (!string.IsNullOrEmpty(wrapperPath))
                 {
                     return new[] { wrapperPath };
                 }
            }
            
            // Fallback to UVX logic
            var (_, fromUrl, packageName) = AssetPathUtility.GetUvxCommandParts();
            string port = HttpEndpointUtility.GetPort().ToString();
            
            var argsList = new System.Collections.Generic.List<string>();
            
            if (!string.IsNullOrEmpty(fromUrl))
            {
                argsList.Add("--from");
                argsList.Add(fromUrl);
            }
            
            argsList.Add(packageName);
            
            if (useHttp)
            {
                argsList.Add("--transport");
                argsList.Add("sse");
                argsList.Add("--port");
                argsList.Add(port);
            }
            else
            {
                argsList.Add("--transport");
                argsList.Add("stdio");
            }
            
            return argsList.ToArray();
        }
    }
}
