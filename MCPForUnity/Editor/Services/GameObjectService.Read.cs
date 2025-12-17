using System;
using System.Collections.Generic;
using System.Linq;
using MCPForUnity.Editor.Helpers;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace MCPForUnity.Editor.Services
{
    public static partial class GameObjectService
    {
        public static object FindGameObjects(JObject @params, JToken targetToken, string searchMethod)
        {
            bool findAll = @params["findAll"]?.ToObject<bool>() ?? false;
            List<GameObject> foundObjects = FindObjectsInternal(targetToken, searchMethod, findAll, @params);

            if (foundObjects.Count == 0)
            {
                return new SuccessResponse("No matching GameObjects found.", new List<object>());
            }

            var results = foundObjects.Select(go => GameObjectSerializer.GetGameObjectData(go)).ToList();
            return new SuccessResponse($"Found {results.Count} GameObject(s).", results);
        }

        public static object GetComponentsFromTarget(string target, string searchMethod, bool includeNonPublicSerialized = true)
        {
            GameObject targetGo = FindObjectInternal(new JValue(target), searchMethod);
            if (targetGo == null)
            {
                return new ErrorResponse($"Target GameObject ('{target}') not found using method '{searchMethod ?? "default"}'.");
            }

            try
            {
                Component[] originalComponents = targetGo.GetComponents<Component>();
                List<Component> componentsToIterate = new List<Component>(originalComponents ?? Array.Empty<Component>());
                
                var componentData = new List<object>();

                for (int i = componentsToIterate.Count - 1; i >= 0; i--)
                {
                    Component c = componentsToIterate[i];
                    if (c == null) continue;

                    try
                    {
                        var data = GameObjectSerializer.GetComponentData(c, includeNonPublicSerialized);
                        if (data != null) componentData.Insert(0, data);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[GetComponentsFromTarget] Error processing component {c.GetType().FullName}: {ex.Message}");
                        componentData.Insert(0, new JObject(
                            new JProperty("typeName", c.GetType().FullName + " (Serialization Error)"),
                            new JProperty("instanceID", c.GetInstanceID()),
                            new JProperty("error", ex.Message)
                        ));
                    }
                }

                return new SuccessResponse($"Retrieved {componentData.Count} components from '{targetGo.name}'.", componentData);
            }
            catch (Exception e)
            {
                return new ErrorResponse($"Error getting components from '{targetGo.name}': {e.Message}");
            }
        }

        public static object GetSingleComponentFromTarget(string target, string searchMethod, string componentName, bool includeNonPublicSerialized = true)
        {
            GameObject targetGo = FindObjectInternal(new JValue(target), searchMethod);
            if (targetGo == null)
            {
                return new ErrorResponse($"Target GameObject ('{target}') not found using method '{searchMethod ?? "default"}'.");
            }

            try
            {
                Component targetComponent = targetGo.GetComponent(componentName);
                if (targetComponent == null)
                {
                    Component[] allComponents = targetGo.GetComponents<Component>();
                    foreach (Component comp in allComponents)
                    {
                        if (comp != null)
                        {
                            string typeName = comp.GetType().Name;
                            string fullTypeName = comp.GetType().FullName;
                            if (typeName == componentName || fullTypeName == componentName)
                            {
                                targetComponent = comp;
                                break;
                            }
                        }
                    }
                }

                if (targetComponent == null)
                {
                    return new ErrorResponse($"Component '{componentName}' not found on GameObject '{targetGo.name}'.");
                }

                var componentData = GameObjectSerializer.GetComponentData(targetComponent, includeNonPublicSerialized);
                if (componentData == null)
                {
                    return new ErrorResponse($"Failed to serialize component '{componentName}' on GameObject '{targetGo.name}'.");
                }

                return new SuccessResponse($"Retrieved component '{componentName}' from '{targetGo.name}'.", componentData);
            }
            catch (Exception e)
            {
                return new ErrorResponse($"Error getting component '{componentName}' from '{targetGo.name}': {e.Message}");
            }
        }
    }
}
