using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;
using MCPForUnity.Editor.Helpers; 
using MCPForUnity.Editor.Services; // Add reference to Services

namespace MCPForUnity.Editor.Tools
{
    /// <summary>
    /// Handles asset management operations within the Unity project.
    /// Acts as a dispatcher to the AssetService.
    /// </summary>
    [McpForUnityTool("manage_asset", AutoRegister = false)]
    public static class ManageAsset
    {
        // --- Main Handler ---

        // Define the list of valid actions
        private static readonly List<string> ValidActions = new List<string>
        {
            "import",
            "create",
            "modify",
            "delete",
            "duplicate",
            "move",
            "rename",
            "search",
            "get_info",
            "create_folder",
            "get_components",
        };

        public static object HandleCommand(JObject @params)
        {
            string action = @params["action"]?.ToString().ToLower();
            if (string.IsNullOrEmpty(action))
            {
                return new ErrorResponse("Action parameter is required.");
            }

            // Check if the action is valid before switching
            if (!ValidActions.Contains(action))
            {
                string validActionsList = string.Join(", ", ValidActions);
                return new ErrorResponse(
                    $"Unknown action: '{action}'. Valid actions are: {validActionsList}"
                );
            }

            // Common parameters
            string path = @params["path"]?.ToString();

            // Coerce string JSON to JObject for 'properties' if provided as a JSON string
            var propertiesToken = @params["properties"];
            if (propertiesToken != null && propertiesToken.Type == JTokenType.String)
            {
                try
                {
                    var parsed = JObject.Parse(propertiesToken.ToString());
                    @params["properties"] = parsed;
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[ManageAsset] Could not parse 'properties' JSON string: {e.Message}");
                }
            }

            try
            {
                switch (action)
                {
                    case "import":
                        return AssetService.ReimportAsset(path, @params["properties"] as JObject);
                    case "create":
                        return AssetService.CreateAsset(@params);
                    case "modify":
                        var properties = @params["properties"] as JObject;
                        return AssetService.ModifyAsset(path, properties);
                    case "delete":
                        return AssetService.DeleteAsset(path);
                    case "duplicate":
                        return AssetService.DuplicateAsset(path, @params["destination"]?.ToString());
                    case "move": 
                    case "rename":
                        return AssetService.MoveOrRenameAsset(path, @params["destination"]?.ToString());
                    case "search":
                        return AssetService.SearchAssets(@params);
                    case "get_info":
                        return AssetService.GetAssetInfo(
                            path,
                            @params["generatePreview"]?.ToObject<bool>() ?? false
                        );
                    case "create_folder": 
                        return AssetService.CreateFolder(path);
                    case "get_components":
                        return AssetService.GetComponentsFromAsset(path);

                    default:
                        string validActionsListDefault = string.Join(", ", ValidActions);
                        return new ErrorResponse(
                            $"Unknown action: '{action}'. Valid actions are: {validActionsListDefault}"
                        );
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[ManageAsset] Action '{action}' failed for path '{path}': {e}");
                return new ErrorResponse(
                    $"Internal error processing action '{action}' on '{path}': {e.Message}"
                );
            }
        }
    }
}
