using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using MCPForUnity.Editor.Helpers;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MCPForUnity.Editor.Services
{
    public static partial class GameObjectService
    {
        private static JsonSerializer InputSerializer => GameObjectSerializer.Serializer;

        // --- Find Helpers ---

        private static GameObject FindObjectInternal(JToken targetToken, string searchMethod, JObject findParams = null)
        {
            // If find_all is not explicitly false, we still want only one for most single-target operations.
            bool findAll = findParams?["findAll"]?.ToObject<bool>() ?? false;
            // If a specific target ID is given, always find just that one.
            if (targetToken?.Type == JTokenType.Integer || (searchMethod == "by_id" && int.TryParse(targetToken?.ToString(), out _)))
            {
                findAll = false;
            }
            List<GameObject> results = FindObjectsInternal(targetToken, searchMethod, findAll, findParams);
            return results.Count > 0 ? results[0] : null;
        }

        private static List<GameObject> FindObjectsInternal(JToken targetToken, string searchMethod, bool findAll, JObject findParams = null)
        {
            List<GameObject> results = new List<GameObject>();
            string searchTerm = findParams?["searchTerm"]?.ToString() ?? targetToken?.ToString();
            bool searchInChildren = findParams?["searchInChildren"]?.ToObject<bool>() ?? false;
            bool searchInactive = findParams?["searchInactive"]?.ToObject<bool>() ?? false;

            if (string.IsNullOrEmpty(searchMethod))
            {
                if (targetToken?.Type == JTokenType.Integer)
                    searchMethod = "by_id";
                else if (!string.IsNullOrEmpty(searchTerm) && searchTerm.Contains('/'))
                    searchMethod = "by_path";
                else
                    searchMethod = "by_name";
            }

            GameObject rootSearchObject = null;
            if (searchInChildren && targetToken != null)
            {
                rootSearchObject = FindObjectInternal(targetToken, "by_id_or_name_or_path");
                if (rootSearchObject == null)
                {
                    Debug.LogWarning($"[GameObjectService.Find] Root object '{targetToken}' for child search not found.");
                    return results;
                }
            }

            switch (searchMethod)
            {
                case "by_id":
                    if (int.TryParse(searchTerm, out int instanceId))
                    {
                        var allObjects = GetAllSceneObjects(searchInactive);
                        GameObject obj = allObjects.FirstOrDefault(go => go.GetInstanceID() == instanceId);
                        if (obj != null) results.Add(obj);
                    }
                    break;
                case "by_name":
                    var searchPoolName = rootSearchObject
                        ? rootSearchObject.GetComponentsInChildren<Transform>(searchInactive).Select(t => t.gameObject)
                        : GetAllSceneObjects(searchInactive);
                    results.AddRange(searchPoolName.Where(go => go.name == searchTerm));
                    break;
                case "by_path":
                    Transform foundTransform = rootSearchObject
                        ? rootSearchObject.transform.Find(searchTerm)
                        : GameObject.Find(searchTerm)?.transform;
                    if (foundTransform != null) results.Add(foundTransform.gameObject);
                    break;
                case "by_tag":
                    var searchPoolTag = rootSearchObject
                        ? rootSearchObject.GetComponentsInChildren<Transform>(searchInactive).Select(t => t.gameObject)
                        : GetAllSceneObjects(searchInactive);
                    results.AddRange(searchPoolTag.Where(go => go.CompareTag(searchTerm)));
                    break;
                case "by_layer":
                    var searchPoolLayer = rootSearchObject
                        ? rootSearchObject.GetComponentsInChildren<Transform>(searchInactive).Select(t => t.gameObject)
                        : GetAllSceneObjects(searchInactive);
                    if (int.TryParse(searchTerm, out int layerIndex))
                        results.AddRange(searchPoolLayer.Where(go => go.layer == layerIndex));
                    else
                    {
                        int namedLayer = LayerMask.NameToLayer(searchTerm);
                        if (namedLayer != -1) results.AddRange(searchPoolLayer.Where(go => go.layer == namedLayer));
                    }
                    break;
                case "by_component":
                    Type componentType = FindType(searchTerm);
                    if (componentType != null)
                    {
                        var searchPoolComp = rootSearchObject
                            ? rootSearchObject.GetComponentsInChildren(componentType, searchInactive).Select(c => (c as Component).gameObject)
                            : UnityEngine.Object.FindObjectsByType(componentType, searchInactive ? FindObjectsInactive.Include : FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                                .Select(c => (c as Component).gameObject);
                        results.AddRange(searchPoolComp.Where(go => go != null));
                    }
                    else
                    {
                        Debug.LogWarning($"[GameObjectService.Find] Component type not found: {searchTerm}");
                    }
                    break;
                case "by_id_or_name_or_path":
                    if (int.TryParse(searchTerm, out int id))
                    {
                        var allObjectsId = GetAllSceneObjects(true);
                        GameObject objById = allObjectsId.FirstOrDefault(go => go.GetInstanceID() == id);
                        if (objById != null) { results.Add(objById); break; }
                    }
                    GameObject objByPath = GameObject.Find(searchTerm);
                    if (objByPath != null) { results.Add(objByPath); break; }
                    var allObjectsName = GetAllSceneObjects(true);
                    results.AddRange(allObjectsName.Where(go => go.name == searchTerm));
                    break;
                default:
                    Debug.LogWarning($"[GameObjectService.Find] Unknown search method: {searchMethod}");
                    break;
            }

            if (!findAll && results.Count > 1) return new List<GameObject> { results[0] };
            return results.Distinct().ToList();
        }

        public static UnityEngine.Object FindObjectByInstruction(JObject instruction, Type targetType)
        {
            string findTerm = instruction["find"]?.ToString();
            string method = instruction["method"]?.ToString()?.ToLower();
            string componentName = instruction["component"]?.ToString();

            if (string.IsNullOrEmpty(findTerm))
            {
                Debug.LogWarning("Find instruction missing 'find' term.");
                return null;
            }

            string searchMethodToUse = string.IsNullOrEmpty(method) ? "by_id_or_name_or_path" : method;

            // Asset searching
            if (typeof(Material).IsAssignableFrom(targetType) ||
                typeof(Texture).IsAssignableFrom(targetType) ||
                typeof(ScriptableObject).IsAssignableFrom(targetType) ||
                targetType.FullName.StartsWith("UnityEngine.U2D") ||
                typeof(AudioClip).IsAssignableFrom(targetType) ||
                typeof(AnimationClip).IsAssignableFrom(targetType) ||
                typeof(Font).IsAssignableFrom(targetType) ||
                typeof(Shader).IsAssignableFrom(targetType) ||
                typeof(ComputeShader).IsAssignableFrom(targetType) ||
                (typeof(GameObject).IsAssignableFrom(targetType) && findTerm.StartsWith("Assets/")))
            {
                UnityEngine.Object asset = AssetDatabase.LoadAssetAtPath(findTerm, targetType);
                if (asset != null) return asset;
                asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(findTerm);
                if (asset != null && targetType.IsAssignableFrom(asset.GetType())) return asset;

                string searchFilter = $"t:{targetType.Name} {System.IO.Path.GetFileNameWithoutExtension(findTerm)}";
                string[] guids = AssetDatabase.FindAssets(searchFilter);
                if (guids.Length == 1)
                {
                    asset = AssetDatabase.LoadAssetAtPath(AssetDatabase.GUIDToAssetPath(guids[0]), targetType);
                    if (asset != null) return asset;
                }
            }

            // Scene searching
            GameObject foundGo = FindObjectInternal(new JValue(findTerm), searchMethodToUse);
            if (foundGo == null) return null;

            if (targetType == typeof(GameObject)) return foundGo;
            else if (typeof(Component).IsAssignableFrom(targetType))
            {
                Type componentToGetType = targetType;
                if (!string.IsNullOrEmpty(componentName))
                {
                    Type specificCompType = FindType(componentName);
                    if (specificCompType != null && typeof(Component).IsAssignableFrom(specificCompType))
                        componentToGetType = specificCompType;
                }
                return foundGo.GetComponent(componentToGetType);
            }
            return null;
        }

        private static IEnumerable<GameObject> GetAllSceneObjects(bool includeInactive)
        {
            var rootObjects = SceneManager.GetActiveScene().GetRootGameObjects();
            var allObjects = new List<GameObject>();
            foreach (var root in rootObjects)
            {
                allObjects.AddRange(root.GetComponentsInChildren<Transform>(includeInactive).Select(t => t.gameObject));
            }
            return allObjects;
        }

        private static Type FindType(string typeName)
        {
            if (ComponentResolver.TryResolve(typeName, out Type resolvedType, out string error))
                return resolvedType;
            if (!string.IsNullOrEmpty(error)) Debug.LogWarning($"[GameObjectService.FindType] {error}");
            return null;
        }

        // --- Property Helpers ---

        private static bool SetProperty(object target, string memberName, JToken value)
        {
            Type type = target.GetType();
            BindingFlags flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase;
            
            try
            {
                if (memberName.Contains('.') || memberName.Contains('['))
                {
                    return SetNestedProperty(target, memberName, value, InputSerializer);
                }

                PropertyInfo propInfo = type.GetProperty(memberName, flags);
                if (propInfo != null && propInfo.CanWrite)
                {
                    object convertedValue = ConvertJTokenToType(value, propInfo.PropertyType, InputSerializer);
                    if (convertedValue != null || value.Type == JTokenType.Null)
                    {
                        propInfo.SetValue(target, convertedValue);
                        return true;
                    }
                }
                else
                {
                    FieldInfo fieldInfo = type.GetField(memberName, flags);
                    if (fieldInfo != null)
                    {
                         object convertedValue = ConvertJTokenToType(value, fieldInfo.FieldType, InputSerializer);
                         if (convertedValue != null || value.Type == JTokenType.Null)
                         {
                             fieldInfo.SetValue(target, convertedValue);
                             return true;
                         }
                    }
                    else
                    {
                        var npField = type.GetField(memberName, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.IgnoreCase);
                        if (npField != null && npField.GetCustomAttribute<SerializeField>() != null)
                        {
                            object convertedValue = ConvertJTokenToType(value, npField.FieldType, InputSerializer);
                            if (convertedValue != null || value.Type == JTokenType.Null)
                            {
                                npField.SetValue(target, convertedValue);
                                return true;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SetProperty] Failed to set '{memberName}' on {type.Name}: {ex.Message}");
            }
            return false;
        }

        private static bool SetNestedProperty(object target, string path, JToken value, JsonSerializer inputSerializer)
        {
            try
            {
                string[] pathParts = SplitPropertyPath(path);
                if (pathParts.Length == 0) return false;

                object currentObject = target;
                Type currentType = currentObject.GetType();
                BindingFlags flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase;

                for (int i = 0; i < pathParts.Length - 1; i++)
                {
                    string part = pathParts[i];
                    bool isArray = false;
                    int arrayIndex = -1;

                    if (part.Contains("["))
                    {
                        int startBracket = part.IndexOf('[');
                        int endBracket = part.IndexOf(']');
                        if (startBracket > 0 && endBracket > startBracket)
                        {
                            if (int.TryParse(part.Substring(startBracket + 1, endBracket - startBracket - 1), out arrayIndex))
                            {
                                isArray = true;
                                part = part.Substring(0, startBracket);
                            }
                        }
                    }

                    PropertyInfo propInfo = currentType.GetProperty(part, flags);
                    FieldInfo fieldInfo = propInfo == null ? currentType.GetField(part, flags) : null;

                    if (propInfo == null && fieldInfo == null) return false;

                    currentObject = propInfo != null ? propInfo.GetValue(currentObject) : fieldInfo.GetValue(currentObject);
                    if (currentObject == null) return false;

                    if (isArray)
                    {
                        if (currentObject is Material[] materials)
                        {
                            if (arrayIndex < 0 || arrayIndex >= materials.Length) return false;
                            currentObject = materials[arrayIndex];
                        }
                        else if (currentObject is IList list)
                        {
                            if (arrayIndex < 0 || arrayIndex >= list.Count) return false;
                            currentObject = list[arrayIndex];
                        }
                        else return false;
                    }
                    currentType = currentObject.GetType();
                }

                string finalPart = pathParts[pathParts.Length - 1];
                if (currentObject is Material material && finalPart.StartsWith("_"))
                {
                    return MaterialOps.TrySetShaderProperty(material, finalPart, value, inputSerializer);
                }

                PropertyInfo finalPropInfo = currentType.GetProperty(finalPart, flags);
                if (finalPropInfo != null && finalPropInfo.CanWrite)
                {
                    object convertedValue = ConvertJTokenToType(value, finalPropInfo.PropertyType, inputSerializer);
                    if (convertedValue != null || value.Type == JTokenType.Null)
                    {
                        finalPropInfo.SetValue(currentObject, convertedValue);
                        return true;
                    }
                }
                else
                {
                    FieldInfo finalFieldInfo = currentType.GetField(finalPart, flags);
                    if (finalFieldInfo != null)
                    {
                        object convertedValue = ConvertJTokenToType(value, finalFieldInfo.FieldType, inputSerializer);
                        if (convertedValue != null || value.Type == JTokenType.Null)
                        {
                            finalFieldInfo.SetValue(currentObject, convertedValue);
                            return true;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SetNestedProperty] Error setting nested property '{path}': {ex.Message}");
            }
            return false;
        }

        private static string[] SplitPropertyPath(string path)
        {
            List<string> parts = new List<string>();
            int startIndex = 0;
            bool inBrackets = false;

            for (int i = 0; i < path.Length; i++)
            {
                if (path[i] == '[') inBrackets = true;
                else if (path[i] == ']') inBrackets = false;
                else if (path[i] == '.' && !inBrackets)
                {
                    parts.Add(path.Substring(startIndex, i - startIndex));
                    startIndex = i + 1;
                }
            }
            if (startIndex < path.Length) parts.Add(path.Substring(startIndex));
            return parts.ToArray();
        }

        private static object ConvertJTokenToType(JToken token, Type targetType, JsonSerializer inputSerializer)
        {
            if (token == null || token.Type == JTokenType.Null)
            {
                if (targetType.IsValueType && Nullable.GetUnderlyingType(targetType) == null)
                    return Activator.CreateInstance(targetType);
                return null;
            }

            try
            {
                return token.ToObject(targetType, inputSerializer);
            }
            catch
            {
                // Fallback handled by exception logic usually, simplified here
                throw;
            }
        }

        private static Vector3? ParseVector3(JArray array)
        {
            if (array != null && array.Count == 3)
            {
                try
                {
                    return new Vector3(array[0].ToObject<float>(), array[1].ToObject<float>(), array[2].ToObject<float>());
                }
                catch { }
            }
            return null;
        }

        private static Vector3 GetDirectionVector(string direction, Transform referenceTransform, bool useWorldSpace)
        {
            if (useWorldSpace)
            {
                switch (direction)
                {
                    case "right": return Vector3.right;
                    case "left": return Vector3.left;
                    case "up": return Vector3.up;
                    case "down": return Vector3.down;
                    case "forward": case "front": return Vector3.forward;
                    case "back": case "backward": case "behind": return Vector3.back;
                    default: return Vector3.forward;
                }
            }
            else
            {
                switch (direction)
                {
                    case "right": return referenceTransform.right;
                    case "left": return -referenceTransform.right;
                    case "up": return referenceTransform.up;
                    case "down": return -referenceTransform.up;
                    case "forward": case "front": return referenceTransform.forward;
                    case "back": case "backward": case "behind": return -referenceTransform.forward;
                    default: return referenceTransform.forward;
                }
            }
        }
    }
}
