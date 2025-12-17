using System;
using MCPForUnity.Editor.Services;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace MCPForUnity.Editor.Tools
{
    public static class ManageGameObject
    {
        public static object HandleCommand(string action, JObject @params)
        {
            if (@params == null)
            {
                return new ErrorResponse("Parameters are required.");
            }

            // Extract common parameters for finding the target GameObject
            JToken targetToken = @params["target"];
            string searchMethod = @params["searchMethod"]?.ToString();

            try
            {
                switch (action)
                {
                    // Read Actions
                    case "find":
                        return GameObjectService.FindGameObjects(@params, targetToken, searchMethod);
                    case "get_components":
                        // Requires 'target' (string usually, handled by FindObjectInternal inside Service)
                        // If targetToken is not a string, let Service handle it or convert to string if simpler
                        // Service expects JObject param, so we just pass @params implementation of GetComponentsFromTarget handles it.
                        // Actually GetComponentsFromTarget signature in Read.cs was: GetComponentsFromTarget(string target, string searchMethod...)
                        // Wait, I implemented: public static object GetComponentsFromTarget(string target, string searchMethod, bool includeNonPublicSerialized = true) in Read.cs
                        // BUT in my dispatcher plan I called GameObjectService.GetComponentsFromTarget(@params)!
                        // Verify definition in step 14.
                        // I wrote: public static object GetComponentsFromTarget(string target, string searchMethod, bool includeNonPublicSerialized = true)
                        
                        // So I need to parse params here. or overload the method in Service?
                        // I implemented GetComponentsFromTarget taking string.
                        // I should update the dispatcher to extract string.
                        return GameObjectService.GetComponentsFromTarget(targetToken?.ToString(), searchMethod);

                    case "get_component":
                         // Read.cs: public static object GetSingleComponentFromTarget(string target, string searchMethod, string componentName, bool includeNonPublicSerialized = true)
                        string cName = @params["componentName"]?.ToString() ?? @params["typeName"]?.ToString();
                        return GameObjectService.GetSingleComponentFromTarget(targetToken?.ToString(), searchMethod, cName);

                    // Write Actions
                    case "create":
                        return GameObjectService.CreateGameObject(@params); // Takes JObject
                    case "modify":
                        return GameObjectService.ModifyGameObject(@params, targetToken, searchMethod); // Takes JObject, JToken, string
                    case "delete":
                        return GameObjectService.DeleteGameObject(@params, targetToken, searchMethod); // Takes JObject, JToken, string
                    case "add_component":
                        return GameObjectService.AddComponentToTarget(@params, targetToken, searchMethod); // Takes JObject, JToken, string
                    case "remove_component":
                        return GameObjectService.RemoveComponentFromTarget(@params, targetToken, searchMethod); // Takes JObject, JToken, string
                    case "set_component_property":
                        return GameObjectService.SetComponentPropertyOnTarget(@params, targetToken, searchMethod); // Takes JObject, JToken, string
                    case "duplicate":
                         return GameObjectService.DuplicateGameObject(@params, targetToken, searchMethod); // Takes JObject, JToken, string
                    case "move_relative":
                        return GameObjectService.MoveRelativeToObject(@params, targetToken, searchMethod); // Takes JObject, JToken, string
                    
                    default:
                        return new ErrorResponse($"Unknown action: '{action}'.");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[ManageGameObject] Action '{action}' failed: {e}");
                return new ErrorResponse($"Internal error processing action '{action}': {e.Message}");
            }
        }
    }
}